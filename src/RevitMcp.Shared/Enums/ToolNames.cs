namespace RevitMcp.Shared.Enums;

/// <summary>
/// Канонические имена инструментов (MCP Tools).
/// Используются и в MCP (tools/list), и в Named Pipe (поле ToolName).
/// </summary>
public static class ToolNames
{
    /// <summary>Получить статус Revit: версия, имя документа, число элементов.</summary>
    public const string GetRevitStatus = "get_revit_status";

    /// <summary>Получить список элементов модели по категории.</summary>
    public const string ListElements = "list_elements";
}