namespace RevitMcp.Shared.Enums;

/// <summary>
/// Коды ошибок для обмена между MCP Server и Revit Addin.
/// </summary>
public enum ErrorCodes
{
    /// <summary>Операция успешна.</summary>
    None = 0,

    /// <summary>Revit не запущен или недоступен.</summary>
    RevitNotAvailable = 1001,

    /// <summary>Таймаут ожидания ответа от Revit.</summary>
    Timeout = 1002,

    /// <summary>Неизвестный или неподдерживаемый инструмент.</summary>
    UnknownTool = 2001,

    /// <summary>Некорректные параметры инструмента.</summary>
    InvalidParameters = 2002,

    /// <summary>Внутренняя ошибка при выполнении операции.</summary>
    InternalError = 5000
}
