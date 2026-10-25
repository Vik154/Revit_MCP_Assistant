using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMcp.RevitAddin.Pipe;
using RevitMcp.Shared.Contracts;
using RevitMcp.Shared.Enums;
using System.Text.Json;

namespace RevitMcp.RevitAddin.Tools;

/// <summary>
/// Инструмент "get_revit_status". Возвращает базовую информацию о Revit и открытом документе.
/// Вызывается в главном потоке Revit через IExternalEventHandler.
/// </summary>
internal sealed class GetRevitStatusTool : IToolHandler
{
    public string ToolName => ToolNames.GetRevitStatus;

    public object? Execute(UIApplication app, JsonElement? arguments)
    {
        // app.ControlledApplication.VersionNumber — например, "2025"
        var revitVersion = app.Application.VersionNumber;

        var doc = app.ActiveUIDocument?.Document;
        bool hasDoc = doc is not null;
        string docTitle = doc?.Title ?? string.Empty;

        // Считаем элементы, только если документ открыт.
        // WhereElementIsNotElementType() — считаем экземпляры, не типы.
        int elementCount = 0;
        if (hasDoc)
        {
            try
            {
                elementCount = new FilteredElementCollector(doc!)
                    .WhereElementIsNotElementType()
                    .GetElementCount();
            }
            catch (Exception ex)
            {
                DebugLogger.Error("Не удалось посчитать элементы", ex);
            }
        }

        DebugLogger.Info($"GetRevitStatus: Revit={revitVersion}, Doc='{docTitle}', Elements={elementCount}");

        return new GetRevitStatusResult
        {
            RevitVersion = revitVersion,
            DocumentTitle = docTitle,
            HasDocument = hasDoc,
            ElementCount = elementCount
        };
    }
}