using Autodesk.Revit.UI;
using RevitMcp.RevitAddin.Handlers;
using RevitMcp.RevitAddin.Pipe;
using RevitMcp.RevitAddin.Tools;
using System.Collections.Generic;

namespace RevitMcp.RevitAddin;

/// <summary>
/// Точка входа плагина. Управляет жизненным циклом Pipe-сервера.
/// </summary>
public class App : IExternalApplication
{
    private CancellationTokenSource? _cts;
    private Task? _serverTask;

    public Result OnStartup(UIControlledApplication application)
    {
        DebugLogger.Info("OnStartup: старт плагина.");

        // 1. Регистрируем инструменты.
        var tools = new List<IToolHandler>
        {
            new GetRevitStatusTool()
            // Здесь будем добавлять новые инструменты в следующих задачах.
        };
        var dispatcher = new ToolDispatcher(tools);

        // 2. Создаём ExternalEventHandler и ExternalEvent.
        var handler = new McpExternalEventHandler(dispatcher);
        handler.Initialize();

        // 3. Запускаем Pipe-сервер в фоновой задаче.
        _cts = new CancellationTokenSource();
        var server = new PipeServer(handler);
        _serverTask = Task.Run(() => server.RunAsync(_cts.Token));

        DebugLogger.Info("OnStartup: Pipe-сервер запущен.");
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        DebugLogger.Info("OnShutdown: остановка плагина.");

        try
        {
            // Посылаем сигнал остановки серверу.
            _cts?.Cancel();

            // Ждём завершения серверной задачи (с таймаутом на всякий случай).
            if (_serverTask is not null)
            {
                if (!_serverTask.Wait(TimeSpan.FromSeconds(5)))
                {
                    DebugLogger.Warn("OnShutdown: сервер не завершился за 5 сек.");
                }
            }

            _cts?.Dispose();
            DebugLogger.Info("OnShutdown: плагин остановлен.");
        }
        catch (Exception ex)
        {
            DebugLogger.Error("OnShutdown: ошибка при остановке", ex);
        }

        return Result.Succeeded;
    }
}
