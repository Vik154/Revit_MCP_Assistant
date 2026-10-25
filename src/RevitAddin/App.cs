using Autodesk.Revit.UI;

namespace RevitMcp.RevitAddin;

/// <summary>
/// Точка входа плагина. Вызывается при старте и закрытии Revit.
/// </summary>
public class App : IExternalApplication
{
    /// <summary>Вызывается при старте Revit.</summary>
    public Result OnStartup(UIControlledApplication application)
    {
        // Временная проверка загрузки плагина (задача 003).
        // В задаче 004 здесь будет запуск Named Pipe сервера.
        TaskDialog.Show(
            "Revit MCP Assistant",
            "Плагин Revit MCP Assistant успешно загружен.\nПрототип, задача 003.");

        return Result.Succeeded;
    }

    /// <summary>Вызывается при закрытии Revit.</summary>
    public Result OnShutdown(UIControlledApplication application)
    {
        return Result.Succeeded;
    }
}
