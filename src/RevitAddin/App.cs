using Autodesk.Revit.UI;

namespace RevitMcp.RevitAddin;

/// <summary>
/// Точка входа плагина. Вызывается при старте и закрытии Revit.
/// В следующих задачах здесь будет инициализация Pipe-сервера.
/// </summary>
public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        // TODO(task_004): запуск Named Pipe сервера
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        // TODO(task_004): остановка Named Pipe сервера
        return Result.Failed;
    }
}
