using System.Text.Json;
using System.Text.Json.Serialization;

namespace RevitMcp.Shared.Serialization;

/// <summary>
/// Общие настройки сериализации JSON для всего проекта.
/// Единые настройки гарантируют, что MCP Server и Revit Addin
/// одинаково понимают формат сообщений.
/// </summary>
public static class SharedJson
{
    /// <summary>Настройки сериализации по умолчанию.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            // camelCase для имён свойств (стандарт для JSON API).
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            // Компактный вывод без отступов: это транспорт, а не чтение человеком.
            WriteIndented = false,
            // Не сериализуем поля со значением null — экономим трафик.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Перечисления сериализуем строками ("toolCall"), а не числами:
        // читабельнее и устойчивее к добавлению новых значений.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        return options;
    }
}