namespace RevitMcp.Shared.Enums;

/// <summary>
/// Тип сообщения, передаваемого через Named Pipe между MCP Server и Revit Addin.
/// </summary>
public enum PipeMessageType
{
    /// <summary>Неизвестный тип (значение по умолчанию).</summary>
    Unknown = 0,

    /// <summary>Вызов инструмента: MCP Server запрашивает выполнение инструмента в Revit.</summary>
    ToolCall = 1,

    /// <summary>Запрос статуса Revit без диспетчеризации инструментов.</summary>
    Status = 2,

    /// <summary>Проверка живости соединения (клиент → сервер).</summary>
    Ping = 3,

    /// <summary>Ответ на проверку живости (сервер → клиент).</summary>
    Pong = 4
}