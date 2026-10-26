namespace RevitMcp.McpServer.Client;

/// <summary>
/// Статус подключения PipeClient к Revit.
/// </summary>
public enum PipeClientStatus
{
    /// <summary>Не подключен (начальное состояние или после disconnect).</summary>
    Disconnected = 0,

    /// <summary>В процессе подключения или переподключения.</summary>
    Connecting = 1,

    /// <summary>Подключен, готов к обмену сообщениями.</summary>
    Connected = 2,

    /// <summary>Неустранимая ошибка (например, отмена через CancellationToken).</summary>
    Failed = 3
}
