namespace RevitMcp.Shared.Contracts;

/// <summary>
/// Результат инструмента "get_revit_status" (см. ToolNames.GetRevitStatus).
/// </summary>
public sealed class GetRevitStatusResult
{
    /// <summary>Версия Revit (например, "2025.2").</summary>
    public string RevitVersion { get; init; } = string.Empty;

    /// <summary>Заголовок открытого документа (пусто, если документ не открыт).</summary>
    public string DocumentTitle { get; init; } = string.Empty;

    /// <summary>Открыт ли документ.</summary>
    public bool HasDocument { get; init; }

    /// <summary>Количество элементов в модели (только экземпляры, без типов).</summary>
    public int ElementCount { get; init; }
}