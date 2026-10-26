using RevitMcp.McpServer.Client;
using RevitMcp.McpServer.Mcp;

namespace RevitMcp.McpServer;

/// <summary>
/// Точка входа MCP-сервера (stdio-транспорт для Cline/Cursor).
/// В фоне поддерживает соединение с Revit, в основном потоке — JSON-RPC на stdin/stdout.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Все логи — в stderr: stdout зарезервирован под JSON-RPC (MCP stdio).
        Console.Error.WriteLine("[McpServer] Запуск MCP Server (stdio)...");

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await using var pipe = new PipeClient();
        pipe.StatusChanged += (_, status) =>
            Console.Error.WriteLine($"[McpServer] Pipe status: {status}");

        // Фон: поддержание соединения с Revit (connect + reconnect).
        _ = Task.Run(() => MaintainConnectionAsync(pipe, cts.Token), cts.Token);

        // Фон: health check Ping/Pong.
        _ = Task.Run(() => pipe.StartHealthCheckAsync(TimeSpan.FromSeconds(5), cts.Token), cts.Token);

        // Основной цикл: MCP stdio.
        var registry = new ToolRegistry(pipe);
        var host = new McpServerHost(registry);
        await host.RunAsync(cts.Token).ConfigureAwait(false);

        cts.Cancel();
        Console.Error.WriteLine("[McpServer] Остановлен.");
        return 0;
    }

    /// <summary>
    /// Держит соединение с Revit живым: подключается при обрыве,
    /// ретраи с backoff внутри ConnectWithRetryAsync. Не бросает — крутится до отмены.
    /// </summary>
    private static async Task MaintainConnectionAsync(PipeClient pipe, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!pipe.IsConnected)
            {
                try
                {
                    await pipe.ConnectWithRetryAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[McpServer] Ошибка подключения: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    await Task.Delay(1_000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}