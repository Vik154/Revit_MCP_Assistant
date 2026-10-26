using Autodesk.Revit.UI;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Tools;

/// <summary>
/// Интерфейс обработчика инструмента.
/// Вызывается из IExternalEventHandler.Execute в главном потоке Revit.
/// </summary>
internal interface IToolHandler
{
    /// <summary>Каноническое имя инструмента (см. ToolNames в Shared).</summary>
    string ToolName { get; }

    /// <summary>
    /// Выполнить инструмент. Аргументы уже десериализованы в JsonElement.
    /// Возвращает объект-результат, который будет сериализован в PipeResponse.Result.
    /// </summary>
    object? Execute(UIApplication app, JsonElement? arguments);
}