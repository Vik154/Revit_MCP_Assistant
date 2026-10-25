using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitMcp.McpServer.Mcp;

/// <summary>Коды ошибок JSON-RPC 2.0.</summary>
public static class JsonRpcErrorCodes
{
    /// <summary>Не удалось распарсить JSON.</summary>
    public const int ParseError = -32700;

    /// <summary>Некорректный JSON-RPC запрос.</summary>
    public const int InvalidRequest = -32600;

    /// <summary>Метод не найден.</summary>
    public const int MethodNotFound = -32601;

    /// <summary>Некорректные параметры.</summary>
    public const int InvalidParams = -32602;

    /// <summary>Внутренняя ошибка сервера.</summary>
    public const int InternalError = -32603;
}

/// <summary>Входящий JSON-RPC 2.0 запрос или уведомление.</summary>
public sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>Id запроса (число или строка). Для уведомлений отсутствует.</summary>
    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    [JsonPropertyName("method")]
    public string Method { get; init; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; init; }

    /// <summary>True, если это уведомление (нет id — ответ не нужен).</summary>
    [JsonIgnore]
    public bool IsNotification => Id.ValueKind == JsonValueKind.Undefined;
}

/// <summary>Исходящий JSON-RPC 2.0 ответ.</summary>
public sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    public JsonElement Id { get; init; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; init; }

    [JsonPropertyName("error")]
    public JsonRpcError? Error { get; init; }
}

/// <summary>Объект ошибки JSON-RPC 2.0.</summary>
public sealed class JsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("data")]
    public JsonElement? Data { get; init; }
}