using System.IO;
using System.Windows.Media;

namespace FileMorph.App;

public partial class App : System.Windows.Application
{
    public static bool LoadThemePreference()
    {
        try
        {
            var path = GetSettingsPath();
            return File.Exists(path) &&
                string.Equals(File.ReadAllText(path).Trim(), "light", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void SaveThemePreference(bool isLight)
    {
        try
        {
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, isLight ? "light" : "dark");
        }
        catch (IOException)
        {
            // Theme persistence is optional; a read-only profile must not block the UI.
        }
        catch (UnauthorizedAccessException)
        {
            // Theme persistence is optional; a read-only profile must not block the UI.
        }
    }

    public static void ApplyTheme(bool isLight)
    {
        var resources = Current.Resources;
        resources["CanvasColor"] = ColorFromHex(isLight ? "#FFF5F6F8" : "#FF111214");
        resources["SurfaceColor"] = ColorFromHex(isLight ? "#FFFFFFFF" : "#FF1B1D21");
        resources["BorderColor"] = ColorFromHex(isLight ? "#FFE0E3E8" : "#FF303238");
        resources["PrimaryTextColor"] = ColorFromHex(isLight ? "#FF202329" : "#FFF1F1F3");
        resources["SecondaryTextColor"] = ColorFromHex(isLight ? "#FF68707C" : "#FFA6A8AE");

        UpdateBrush("CanvasBrush", (Color)resources["CanvasColor"]);
        UpdateBrush("SurfaceBrush", (Color)resources["SurfaceColor"]);
        UpdateBrush("BorderBrushSoft", (Color)resources["BorderColor"]);
        UpdateBrush("PrimaryTextBrush", (Color)resources["PrimaryTextColor"]);
        UpdateBrush("SecondaryTextBrush", (Color)resources["SecondaryTextColor"]);
    }

    private static void UpdateBrush(string key, Color color)
    {
        if (Current.Resources[key] is SolidColorBrush brush)
            brush.Color = color;
    }

    private static Color ColorFromHex(string value) =>
        (Color)ColorConverter.ConvertFromString(value)!;

    private static string GetSettingsPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileMorph", "settings.json");
}
