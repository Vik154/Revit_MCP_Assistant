using RevitMcp.Shared.Enums;
using RevitMcp.Shared.Pipe;
using System.IO.Pipes;
using System.Text.Json;

namespace RevitMcp.McpServer.Client;

/// <summary>
/// Named Pipe Client — подключается к PipeServer, запущенному в Revit.
/// Обеспечивает отказоустойчивость: reconnection с exponential backoff,
/// health check через Ping/Pong, корректная обработка "Revit не запущен".
/// </summary>
public sealed class PipeClient : IAsyncDisposable
{
    // --- Конфигурация ---

    /// <summary>Таймаут на получение ответа от Revit (мсек).</summary>
    public int ResponseTimeoutMs { get; init; } = 30_000;

    /// <summary>Минимальная задержка при reconnection (мсек).</summary>
    public int MinReconnectDelayMs { get; init; } = 1_000;

    /// <summary>Максимальная задержка при reconnection (мсек).</summary>
    public int MaxReconnectDelayMs { get; init; } = 30_000;

    // --- Состояние ---

    private NamedPipeClientStream? _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>Текущий статус подключения.</summary>
    public PipeClientStatus Status { get; private set; } = PipeClientStatus.Disconnected;

    /// <summary>Удобное булево свойство: подключен ли клиент к Revit.</summary>
    public bool IsConnected => Status == PipeClientStatus.Connected;

    /// <summary>Событие изменения статуса (для уведомлений UI/логов).</summary>
    public event EventHandler<PipeClientStatus>? StatusChanged;

    // --- Подключение ---

    /// <summary>
    /// Подключается к PipeServer в Revit.
    /// Если Revit не запущен — бросает IOException (не падает сам клиент).
    /// </summary>
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        SetStatus(PipeClientStatus.Connecting);

        try
        {
            _stream?.Dispose();
            _stream = new NamedPipeClientStream(
                ".",
                PipeConstants.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(PipeConstants.ConnectTimeoutMs);

            await _stream.ConnectAsync(cts.Token).ConfigureAwait(false);

            SetStatus(PipeClientStatus.Connected);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Наш внутренний таймаут — это не отмена пользователем.
            SetStatus(PipeClientStatus.Disconnected);
            throw new IOException($"Таймаут подключения к Revit ({PipeConstants.ConnectTimeoutMs}мс). Проверьте, запущен ли Revit.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
        {
            SetStatus(PipeClientStatus.Disconnected);
            throw; // Пробрасываем, чтобы вызывающий код мог решить, что делать.
        }
    }

    /// <summary>
    /// Подключается с попытками и exponential backoff.
    /// Если Revit не запущен — продолжает пытаться, пока не будет отмена.
    /// </summary>
    public async Task ConnectWithRetryAsync(CancellationToken ct = default)
    {
        int attempt = 0;
        int delay = MinReconnectDelayMs;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConnectAsync(ct).ConfigureAwait(false);
                return; // Успех
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                SetStatus(PipeClientStatus.Failed);
                throw;
            }
            catch (Exception ex)
            {
                attempt++;
                Console.WriteLine($"[PipeClient] Попытка {attempt}: {ex.Message}. Повтор через {delay}мс.");

                try
                {
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    SetStatus(PipeClientStatus.Failed);
                    throw;
                }

                // Экспоненциальный рост задержки с cap.
                delay = Math.Min(delay * 2, MaxReconnectDelayMs);
            }
        }

        SetStatus(PipeClientStatus.Failed);
        ct.ThrowIfCancellationRequested();
    }

    // --- Обмен сообщениями ---

    /// <summary>
    /// Отправить запрос в Revit и дождаться ответа.
    /// Корреляция — по RequestId.
    /// </summary>
    public async Task<PipeResponse> SendAsync(PipeRequest request, CancellationToken ct = default)
    {
        if (_stream is null || Status != PipeClientStatus.Connected)
        {
            throw new InvalidOperationException("PipeClient не подключен. Вызовите ConnectAsync.");
        }

        // Сериализуем обмен сообщениями — NamedPipeServerStream не потокобезопасен.
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ResponseTimeoutMs);
            var linkedToken = timeoutCts.Token;

            // Отправляем запрос.
            await PipeFraming.WriteMessageAsync(_stream, request, linkedToken).ConfigureAwait(false);

            // Читаем ответ.
            var response = await PipeFraming.ReadMessageAsync<PipeResponse>(_stream, linkedToken).ConfigureAwait(false);

            if (response is null)
            {
                // Соединение закрыто.
                SetStatus(PipeClientStatus.Disconnected);
                throw new IOException("Revit закрыл соединение.");
            }

            // Проверка корреляции: ответ должен быть на наш RequestId.
            if (response.RequestId != request.RequestId)
            {
                throw new InvalidOperationException(
                    $"Рассинхронизация RequestId: ожидался '{request.RequestId}', получен '{response.RequestId}'.");
            }

            return response;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Наш таймаут ResponseTimeoutMs.
            SetStatus(PipeClientStatus.Disconnected);
            throw new TimeoutException($"Revit не ответил за {ResponseTimeoutMs}мс.");
        }
        catch (IOException)
        {
            // Потеря связи.
            SetStatus(PipeClientStatus.Disconnected);
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Отправить инструмент (tool call) и получить ответ. Удобная обёртка над SendAsync.
    /// </summary>
    public Task<PipeResponse> CallToolAsync(string toolName, object? arguments = null, CancellationToken ct = default)
    {
        var request = new PipeRequest
        {
            Type = PipeMessageType.ToolCall,
            ToolName = toolName,
            Arguments = arguments is null
                ? null
                : JsonSerializer.SerializeToElement(arguments)
        };
        return SendAsync(request, ct);
    }

    // --- Health Check ---

    /// <summary>
    /// Фоновый health check: периодически шлёт Ping и ждёт Pong.
    /// Если не пришёл Pong — помечает статус как Disconnected.
    /// Запускается один раз, работает до отмены.
    /// </summary>
    public async Task StartHealthCheckAsync(TimeSpan interval, CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (Status != PipeClientStatus.Connected)
            {
                continue; // Не пытаемся пинговать, пока не подключены.
            }

            try
            {
                var pingRequest = new PipeRequest
                {
                    Type = PipeMessageType.Ping,
                    ToolName = string.Empty
                };
                var response = await SendAsync(pingRequest, ct).ConfigureAwait(false);

                if (response.Type != PipeMessageType.Pong || !response.Success)
                {
                    Console.WriteLine($"[PipeClient] Health check: получен неожиданный ответ type={response.Type}, success={response.Success}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PipeClient] Health check провалился: {ex.Message}");
                SetStatus(PipeClientStatus.Disconnected);
            }
        }
    }

    // --- Жизненный цикл ---

    private void SetStatus(PipeClientStatus newStatus)
    {
        if (Status == newStatus) return;
        Status = newStatus;
        StatusChanged?.Invoke(this, newStatus);
    }

    /// <summary>
    /// Закрыть соединение.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_stream is not null)
        {
            try
            {
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
            catch { /* игнорим */ }
            _stream = null;
        }

        _sendLock.Dispose();
        SetStatus(PipeClientStatus.Disconnected);
    }
}
