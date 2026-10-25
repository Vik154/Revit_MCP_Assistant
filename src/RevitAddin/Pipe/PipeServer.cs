using RevitMcp.RevitAddin.Handlers;
using RevitMcp.Shared.Pipe;
using System.IO.Pipes;

namespace RevitMcp.RevitAddin.Pipe;

/// <summary>
/// Named Pipe Server внутри Revit. Работает в фоновом потоке.
/// Принимает подключения от MCP Server, обрабатывает запросы через
/// McpExternalEventHandler (тред-сейфность), возвращает ответы.
/// </summary>
internal sealed class PipeServer
{
    private readonly McpExternalEventHandler _handler;

    public PipeServer(McpExternalEventHandler handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Основной цикл сервера. Запускается из OnStartup как фоновая Task.
    /// При отмене (OnShutdown) выходит корректно.
    /// При disconnect клиента — продолжает слушать следующего.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        DebugLogger.Info($"PipeServer: запуск, имя pipe = '{PipeConstants.PipeName}'.");

        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    PipeConstants.PipeName,
                    PipeDirection.InOut,
                    1,                    // maxNumberOfServerInstances
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                DebugLogger.Info("PipeServer: ожидание подключения клиента...");
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
                DebugLogger.Info("PipeServer: клиент подключён.");

                await HandleClientAsync(server, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                DebugLogger.Info("PipeServer: получена отмена, выходим.");
                break;
            }
            catch (IOException ex)
            {
                // Клиент отвалился — это не ошибка сервера.
                DebugLogger.Warn($"PipeServer: клиент отключился ({ex.Message}). Продолжаем слушать.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error("PipeServer: необработанная ошибка в цикле", ex);
                // Небольшая пауза, чтобы не спамить логами при повторяющейся ошибке.
                try { await Task.Delay(1000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                if (server is not null)
                {
                    try { server.Dispose(); }
                    catch { /* игнорим ошибки Dispose */ }
                }
            }
        }

        DebugLogger.Info("PipeServer: остановлен.");
    }

    private async Task HandleClientAsync(NamedPipeServerStream server, CancellationToken ct)
    {
        // Цикл обмена сообщениями с подключённым клиентом.
        while (server.IsConnected && !ct.IsCancellationRequested)
        {
            PipeRequest? request;
            try
            {
                request = await PipeFraming.ReadMessageAsync<PipeRequest>(server, ct).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                // Повреждённый кадр (некорректная длина).
                DebugLogger.Error("PipeServer: повреждённый кадр от клиента", ex);
                break;
            }

            if (request is null)
            {
                // Клиент закрыл соединение.
                DebugLogger.Info("PipeServer: клиент закрыл соединение.");
                break;
            }

            DebugLogger.Info($"PipeServer: получен запрос id={request.RequestId}, type={request.Type}, tool={request.ToolName}");

            // Отправляем запрос в главный поток Revit через ExternalEventHandler.
            PipeResponse response;
            try
            {
                response = await _handler.EnqueueAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                DebugLogger.Error("PipeServer: ошибка в EnqueueAsync", ex);
                response = new PipeResponse
                {
                    RequestId = request.RequestId,
                    Success = false,
                    ErrorCode = RevitMcp.Shared.Enums.ErrorCodes.InternalError,
                    ErrorMessage = $"Внутренняя ошибка сервера: {ex.Message}"
                };
            }

            await PipeFraming.WriteMessageAsync(server, response, ct).ConfigureAwait(false);
        }
    }
}