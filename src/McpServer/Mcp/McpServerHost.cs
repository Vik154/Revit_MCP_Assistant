using System.Text.Json;
using RevitMcp.Shared.Serialization;

namespace RevitMcp.McpServer.Mcp;

/// <summary>
/// Хост MCP-сервера: stdio-транспорт (JSON-RPC 2.0, построчные сообщения).
/// КРИТИЧНО: stdout содержит ТОЛЬКО JSON-RPC. Все логи — в stderr.
/// </summary>
public sealed class McpServerHost
{
    /// <summary>Id=null для ошибок парсинга (id запроса неизвестен).</summary>
    private static readonly JsonElement NullId = JsonDocument.Parse("null").RootElement.Clone();

    private readonly ToolRegistry _tools;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public McpServerHost(ToolRegistry tools)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
    }

    /// <summary>Главный цикл: читает stdin построчно до закрытия потока.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        Log("старт, жду JSON-RPC на stdin...");
        using var reader = new StreamReader(Console.OpenStandardInput());

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (line is null)
            {
                break; // stdin закрыт — клиент вышел
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await HandleLineAsync(line, ct).ConfigureAwait(false);
        }

        Log("stdin закрыт, остановка.");
    }

    private async Task HandleLineAsync(string line, CancellationToken ct)
    {
        JsonRpcRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<JsonRpcRequest>(line, SharedJson.Options);
        }
        catch (JsonException ex)
        {
            await SendAsync(MakeError(NullId, JsonRpcErrorCodes.ParseError, $"Parse error: {ex.Message}"), ct).ConfigureAwait(false);
            return;
        }

        if (request is null || string.IsNullOrEmpty(request.Method))
        {
            await SendAsync(MakeError(request?.Id ?? NullId, JsonRpcErrorCodes.InvalidRequest, "Invalid Request"), ct).ConfigureAwait(false);
            return;
        }

        // Уведомления (без id) — ответ не отправляем.
        if (request.IsNotification)
        {
            Log($"notification: {request.Method}");
            return;
        }

        JsonRpcResponse response;
        try
        {
            response = await DispatchAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"ошибка dispatch {request.Method}: {ex}");
            response = MakeError(request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }

        await SendAsync(response, ct).ConfigureAwait(false);
    }

    private async Task<JsonRpcResponse> DispatchAsync(JsonRpcRequest request, CancellationToken ct)
    {
        switch (request.Method)
        {
            case "initialize":
                return Ok(request.Id, InitializeResult(request));

            case "ping":
                return Ok(request.Id, JsonSerializer.SerializeToElement(new { }, SharedJson.Options));

            case "tools/list":
                return Ok(request.Id, JsonSerializer.SerializeToElement(new { tools = _tools.Tools }, SharedJson.Options));

            case "tools/call":
                return await ToolsCallAsync(request, ct).ConfigureAwait(false);

            default:
                return MakeError(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Method not found: {request.Method}");
        }
    }

    private async Task<JsonRpcResponse> ToolsCallAsync(JsonRpcRequest request, CancellationToken ct)
    {
        if (request.Params?.ValueKind != JsonValueKind.Object ||
            !request.Params.Value.TryGetProperty("name", out var nameEl) ||
            nameEl.ValueKind != JsonValueKind.String)
        {
            return MakeError(request.Id, JsonRpcErrorCodes.InvalidParams, "tools/call: ожидается params.name (string)");
        }

        var name = nameEl.GetString()!;

        JsonElement? arguments = null;
        if (request.Params.Value.TryGetProperty("arguments", out var argsEl) &&
            argsEl.ValueKind != JsonValueKind.Null)
        {
            arguments = argsEl;
        }

        if (!_tools.TryGet(name, out _))
        {
            return MakeError(request.Id, JsonRpcErrorCodes.MethodNotFound, $"Неизвестный инструмент: {name}");
        }

        var result = await _tools.CallAsync(name, arguments, ct).ConfigureAwait(false);
        return Ok(request.Id, result);
    }

    /// <summary>Handshake: возвращаем protocolVersion клиента, возможности и инфо о сервере.</summary>
    private static JsonElement InitializeResult(JsonRpcRequest request)
    {
        string protocolVersion = "2025-06-18";
        if (request.Params?.ValueKind == JsonValueKind.Object &&
            request.Params.Value.TryGetProperty("protocolVersion", out var pv) &&
            pv.ValueKind == JsonValueKind.String)
        {
            protocolVersion = pv.GetString() ?? protocolVersion;
        }

        return JsonSerializer.SerializeToElement(new
        {
            protocolVersion,
            capabilities = new { tools = new { } },
            serverInfo = new { name = "revit-mcp-assistant", version = "0.1.0" }
        }, SharedJson.Options);
    }

    private static JsonRpcResponse Ok(JsonElement id, JsonElement result) =>
        new() { Id = id, Result = result };

    private static JsonRpcResponse MakeError(JsonElement id, int code, string message) =>
        new() { Id = id, Error = new JsonRpcError { Code = code, Message = message } };

    /// <summary>Пишет ОДНУ JSON-RPC строку в stdout (поток синхронизирован).</summary>
    private async Task SendAsync(JsonRpcResponse response, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(response, SharedJson.Options);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await Console.Out.WriteLineAsync(json).ConfigureAwait(false);
            await Console.Out.FlushAsync().ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static void Log(string message) =>
        Console.Error.WriteLine($"[McpServer] {message}");
}