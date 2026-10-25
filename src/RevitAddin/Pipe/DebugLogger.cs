using System.Diagnostics;

namespace RevitMcp.RevitAddin.Pipe;

/// <summary>
/// Минимальный логгер. Пишет в System.Diagnostics.Debug — эти сообщения видны
/// в Output окне Visual Studio и могут быть считаны WPF через DebugListener.
/// В будущем можно заменить на Microsoft.Extensions.Logging.
/// </summary>
internal static class DebugLogger
{
    private const string Prefix = "[RevitMcp]";

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);
    public static void Error(string message, Exception ex) => Write("ERROR", $"{message}: {ex}");

/*     private static void Write(string level, string message)
    {
        Debug.WriteLine($"{Prefix} [{level}] {message}");
    } */

    private static readonly string LogPath = Path.Combine(
    Path.GetTempPath(), "RevitMcp.log");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
        Debug.WriteLine($"{Prefix} {line}");
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* игнорим ошибки записи */ }
    }
}