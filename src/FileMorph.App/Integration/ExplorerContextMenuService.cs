using Microsoft.Win32;
using System.IO;

namespace FileMorph.App.Integration;

/// <summary>
/// Adds an opt-in, per-user entry to the classic Windows Explorer image context menu.
/// No administrator privileges or machine-wide registry changes are required.
/// </summary>
public sealed class ExplorerContextMenuService
{
    private const string MenuKeyPath = @"Software\Classes\SystemFileAssociations\image\shell\FileMorph.Convert";

    public bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(MenuKeyPath);
        var command = key?.OpenSubKey("command")?.GetValue(null) as string;
        return "\"" + executable + "\" \"%1\"";
    }

    public void Register()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Explorer integration is available only on Windows.");

        using var key = Registry.CurrentUser.CreateSubKey(MenuKeyPath, writable: true)
            ?? throw new InvalidOperationException("Could not create the current-user Explorer menu entry.");
        key.SetValue(null, "Convert with FileMorph");
        key.SetValue("Icon", Path.Combine(AppContext.BaseDirectory, "FileMorph.exe"));
        using var command = key.CreateSubKey("command", writable: true)
            ?? throw new InvalidOperationException("Could not create the Explorer command.");
        command.SetValue(null, BuildCommand());
    }

    public void Unregister()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Explorer integration is available only on Windows.");
        Registry.CurrentUser.DeleteSubKeyTree(MenuKeyPath, throwOnMissingSubKey: false);
    }

    private static string BuildCommand()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "FileMorph.exe");
        return $"\\"{executable}\\" \\"%1\\"";
    }
}
