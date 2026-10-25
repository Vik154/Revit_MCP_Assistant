using RevitMcp.McpServer.Client;
using RevitMcp.Shared.Contracts;
using RevitMcp.Shared.Enums;
using System.Text.Json;

namespace RevitMcp.McpServer;

/// <summary>
/// Временная точка входа для интеграционного теста (task_006).
/// Подключается к Revit, вызывает get_revit_status, печатает результат.
/// В task_007 будет заменена на полноценный MCP Server с stdio транспортом.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("=== RevitMcp Integration Test (task_006) ===");
        Console.WriteLine("Убедитесь, что Revit 2025 запущен и плагин загружен.");
        Console.WriteLine();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        await using var client = new PipeClient();

        try
        {
            // 1. Подключаемся к Revit (с retry, если Revit не запущен).
            Console.WriteLine("[1/3] Подключение к Revit...");
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            connectCts.CancelAfter(TimeSpan.FromSeconds(60));

            await client.ConnectWithRetryAsync(connectCts.Token).ConfigureAwait(false);
            Console.WriteLine("✅ Подключено к Revit.");
            Console.WriteLine();

            // 2. Вызываем get_revit_status.
            Console.WriteLine("[2/3] Вызов get_revit_status...");
            var response = await client.CallToolAsync(ToolNames.GetRevitStatus, ct: cts.Token).ConfigureAwait(false);

            if (!response.Success)
            {
                Console.WriteLine($"❌ Ошибка: {response.ErrorCode} — {response.ErrorMessage}");
                return 1;
            }

            // 3. Десериализуем и печатаем результат.
            Console.WriteLine("[3/3] Результат:");
            if (response.Result.HasValue)
            {
                var result = response.Result.Value.Deserialize<GetRevitStatusResult>();
                if (result is not null)
                {
                    Console.WriteLine($"  Версия Revit:     {result.RevitVersion}");
                    Console.WriteLine($"  Документ:         {(result.HasDocument ? result.DocumentTitle : "(не открыт)")}");
                    Console.WriteLine($"  Элементов:        {result.ElementCount}");
                }
                else
                {
                    Console.WriteLine("  (не удалось десериализовать результат)");
                }
            }
            else
            {
                Console.WriteLine("  (пустой результат)");
            }

            Console.WriteLine();
            Console.WriteLine("✅ Интеграционный тест пройден.");
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("❌ Отменено пользователем.");
            return 1;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Ошибка: {ex.Message}");
            return 1;
        }
    }
}
