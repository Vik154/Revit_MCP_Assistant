using System.Text.Json;
using RevitMcp.McpServer.Client;
using RevitMcp.Shared.Enums;
using RevitMcp.Shared.Serialization;

namespace RevitMcp.McpServer.Mcp;

/// <summary>Определение MCP-инструмента (для tools/list).</summary>
public sealed class McpToolDefinition
{
    /// <summary>Каноническое имя инструмента.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание для LLM-клиента.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>JSON Schema входных аргументов.</summary>
    public JsonElement InputSchema { get; init; }
}

/// <summary>
/// Реестр MCP-инструментов: определения для tools/list и
/// исполнение tools/call через PipeClient (проброс в Revit).
/// </summary>
public sealed class ToolRegistry
{
    private readonly PipeClient _pipe;
    private readonly Dictionary<string, McpToolDefinition> _tools = new(StringComparer.Ordinal);

    public ToolRegistry(PipeClient pipe)
    {
        _pipe = pipe ?? throw new ArgumentNullException(nameof(pipe));
        RegisterBuiltInTools();
    }

    /// <summary>Все зарегистрированные инструменты.</summary>
    public IReadOnlyCollection<McpToolDefinition> Tools => _tools.Values;

    /// <summary>Проверить существование инструмента.</summary>
    public bool TryGet(string name, out McpToolDefinition? tool) => _tools.TryGetValue(name, out tool);

    /// <summary>
    /// Выполнить инструмент через Revit и вернуть payload результата tools/call
    /// в формате MCP: { content: [{type:"text", text}], isError }.
    /// </summary>
    public async Task<JsonElement> CallAsync(string name, JsonElement? arguments, CancellationToken ct)
    {
        if (!_tools.ContainsKey(name))
        {
            return MakeResult($"Неизвестный инструмент: {name}", isError: true);
        }

        // Revit не подключен — не падаем, возвращаем понятную ошибку инструменту.
        if (!_pipe.IsConnected)
        {
            return MakeResult($"Revit не подключён. Инструмент '{name}' сейчас недоступен.", isError: true);
        }

        var response = await _pipe.CallToolAsync(name, arguments, ct).ConfigureAwait(false);

        if (!response.Success)
        {
            return MakeResult($"Ошибка Revit: {response.ErrorCode} — {response.ErrorMessage}", isError: true);
        }

        var text = response.Result.HasValue
            ? response.Result.Value.GetRawText()
            : "(пустой результат)";

        return MakeResult(text, isError: false);
    }

    /// <summary>Регистрация встроенных инструментов. Новые добавлять здесь.</summary>
    private void RegisterBuiltInTools()
    {
        _tools[ToolNames.GetRevitStatus] = new McpToolDefinition
        {
            Name = ToolNames.GetRevitStatus,
            Description = "Возвращает статус Revit: версия, открытый документ, количество элементов модели.",
            InputSchema = ParseSchema("""{"type":"object","properties":{},"additionalProperties":false}""")
        };
    }

    private static JsonElement ParseSchema(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static JsonElement MakeResult(string text, bool isError)
    {
        var payload = new
        {
            content = new[] { new { type = "text", text } },
            isError
        };
        return JsonSerializer.SerializeToElement(payload, SharedJson.Options);
    }
}