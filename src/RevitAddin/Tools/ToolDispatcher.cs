using Autodesk.Revit.UI;
using RevitMcp.RevitAddin.Pipe;
using RevitMcp.Shared.Enums;
using RevitMcp.Shared.Pipe;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Tools;

/// <summary>
/// Диспетчер инструментов. Маппит имя инструмента из PipeRequest на IToolHandler.
/// Все операции — в главном потоке Revit (вызывается из McpExternalEventHandler.Execute).
/// </summary>
internal sealed class ToolDispatcher
{
    private readonly Dictionary<string, IToolHandler> _handlers;

    public ToolDispatcher(IEnumerable<IToolHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.ToolName, StringComparer.Ordinal);
        DebugLogger.Info($"ToolDispatcher: зарегистрировано {_handlers.Count} инструментов: {string.Join(", ", _handlers.Keys)}");
    }

    /// <summary>
    /// Диспетчеризировать запрос. Возвращает PipeResponse, готовый к отправке.
    /// </summary>
    public PipeResponse Dispatch(UIApplication app, PipeRequest request)
    {
        // Ping/Pong — без диспетчеризации в инструменты.
        if (request.Type == PipeMessageType.Ping)
        {
            return new PipeResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Type = PipeMessageType.Pong
            };
        }

        // Status — базовый пинг живости без диспетчеризации.
        if (request.Type == PipeMessageType.Status)
        {
            return new PipeResponse
            {
                RequestId = request.RequestId,
                Success = true,
                Result = JsonSerializer.SerializeToElement(new { alive = true })
            };
        }

        if (!_handlers.TryGetValue(request.ToolName, out var handler))
        {
            return new PipeResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorCode = ErrorCodes.UnknownTool,
                ErrorMessage = $"Неизвестный инструмент: {request.ToolName}"
            };
        }

        try
        {
            var result = handler.Execute(app, request.Arguments);
            return new PipeResponse
            {
                RequestId = request.RequestId,
                Success = true,
                // Сериализуем результат в JsonElement для транспорта.
                Result = result is null ? null : JsonSerializer.SerializeToElement(result)
            };
        }
        catch (Exception ex)
        {
            DebugLogger.Error($"Ошибка при выполнении инструмента '{request.ToolName}'", ex);
            return new PipeResponse
            {
                RequestId = request.RequestId,
                Success = false,
                ErrorCode = ErrorCodes.InternalError,
                ErrorMessage = ex.Message
            };
        }
    }
}