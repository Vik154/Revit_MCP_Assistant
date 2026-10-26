namespace RevitMcp.Shared.Pipe;

/// <summary>
/// Общие константы для Named Pipe моста между MCP Server и Revit Addin.
/// Используются и сервером (в Revit), и клиентом (в MCP Server).
/// </summary>
public static class PipeConstants
{
    /// <summary>Имя Named Pipe. Для MVP — фиксированное.</summary>
    public const string PipeName = "RevitMcpBridge";

    /// <summary>Версия протокола. При несовместимых изменениях увеличиваем.</summary>
    public const string ProtocolVersion = "1.0";

    /// <summary>
    /// Таймаут подключения для клиента (мсек).
    /// Используется на стороне MCP Server при ConnectAsync.
    /// </summary>
    public const int ConnectTimeoutMs = 5000;
}