using System.Text.Json;
using RevitMcp.Shared.Enums;

namespace RevitMcp.Shared.Pipe;

/// <summary>
/// Запрос, отправляемый MCP Server → Revit Addin через Named Pipe.
/// </summary>
public sealed class PipeRequest
{
    /// <summary>Уникальный идентификатор запроса для сопоставления с ответом.</summary>
    public string RequestId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Тип сообщения.</summary>
    public PipeMessageType Type { get; init; } = PipeMessageType.ToolCall;

    /// <summary>Имя вызываемого инструмента (см. ToolNames). Актуально при Type == ToolCall.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Аргументы инструмента в произвольном JSON-виде.
    /// Каждый инструмент сам десериализует нужную ему структуру.
    /// </summary>
    public JsonElement? Arguments { get; init; }
}