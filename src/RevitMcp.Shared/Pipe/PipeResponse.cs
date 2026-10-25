using System.Text.Json;
using RevitMcp.Shared.Enums;

namespace RevitMcp.Shared.Pipe;

/// <summary>
/// Ответ, отправляемый Revit Addin → MCP Server через Named Pipe.
/// </summary>
public sealed class PipeResponse
{
    /// <summary>Идентификатор запроса, на который это ответ.</summary>
    public string RequestId { get; init; } = string.Empty;

    /// <summary>Успешно ли выполнена операция.</summary>
    public bool Success { get; init; }

    /// <summary>Код ошибки (None при успехе).</summary>
    public ErrorCodes ErrorCode { get; init; } = ErrorCodes.None;

    /// <summary>Человекочитаемое сообщение об ошибке (null при успехе).</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Результат выполнения в произвольном JSON-виде.</summary>
    public JsonElement? Result { get; init; }
}