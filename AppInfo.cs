using System.Reflection;

namespace BetterStickyNotes;

public static class AppInfo
{
    public static string Version { get; } = typeof(App).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(App).Assembly.GetName().Version?.ToString(3) ?? "inconnue";
    public static string DisplayName => $"Better Sticky Notes v{Version}";
    public static string ExecutablePath => Environment.ProcessPath ?? "Chemin indisponible";
}
