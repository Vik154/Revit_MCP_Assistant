using Autodesk.Revit.UI;
using RevitMcp.RevitAddin.Pipe;
using RevitMcp.RevitAddin.Tools;
using RevitMcp.Shared.Pipe;
using System.Collections.Concurrent;

namespace RevitMcp.RevitAddin.Handlers;

/// <summary>
/// Обработчик, выполняющий запросы от Named Pipe в главном потоке Revit.
/// КРИТИЧНО: Revit API не потокобезопасен. ВСЕ вызовы API — только через этот класс.
/// </summary>
internal sealed class McpExternalEventHandler : IExternalEventHandler
{
    // Очередь запросов. Pipe-поток кладёт сюда, Execute() в главном потоке достаёт.
    private readonly ConcurrentQueue<PipeRequest> _queue = new();

    // Callbacks для отправки ответа обратно в Pipe-поток.
    // RequestId → TaskCompletionSource, который Pipe-поток ждёт.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<PipeResponse>> _pending
        = new();

    private readonly ToolDispatcher _dispatcher;
    private ExternalEvent? _externalEvent;

    public McpExternalEventHandler(ToolDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>
    /// Создаёт ExternalEvent. Вызывается один раз при старте приложения.
    /// </summary>
    public ExternalEvent Initialize()
    {
        _externalEvent = ExternalEvent.Create(this);
        DebugLogger.Info("McpExternalEventHandler: ExternalEvent создан.");
        return _externalEvent;
    }

    /// <summary>
    /// Ставит запрос в очередь и возвращает Task, который завершится,
    /// когда Execute() обработает запрос и отправит ответ.
    /// Безопасно вызывать из любого потока.
    /// </summary>
    public Task<PipeResponse> EnqueueAsync(PipeRequest request)
    {
        var tcs = new TaskCompletionSource<PipeResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[request.RequestId] = tcs;
        _queue.Enqueue(request);
        _externalEvent?.Raise();
        return tcs.Task;
    }

    /// <summary>
    /// Выполняется Revit'ом в ГЛАВНОМ потоке. Здесь можно безопасно работать с API.
    /// </summary>
    public void Execute(UIApplication app)
    {
        while (_queue.TryDequeue(out var request))
        {
            if (!_pending.TryRemove(request.RequestId, out var tcs))
            {
                continue; // Запрос был отменён или уже обработан.
            }

            try
            {
                var response = _dispatcher.Dispatch(app, request);
                tcs.TrySetResult(response);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Execute: необработанное исключение для request '{request.RequestId}'", ex);
                tcs.TrySetException(ex);
            }
        }
    }

    public string GetName() => "RevitMcpBridge";
}