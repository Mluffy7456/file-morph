using System.IO;
using System.Text.Json;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace FileMorph.App;

public partial class App : System.Windows.Application
{
    private sealed record UserSettings(string Theme);

    public static bool LoadThemePreference()
    {
        try
        {
            var path = GetSettingsPath();
            if (!File.Exists(path)) return false;
            var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path));
            return string.Equals(settings?.Theme, "light", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return false; }
    }

    public static void SaveThemePreference(bool isLight)
    {
        try
        {
            var path = GetSettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new UserSettings(isLight ? "light" : "dark")));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
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
        UpdateBrush("CanvasBrush", (MediaColor)resources["CanvasColor"]);
        UpdateBrush("SurfaceBrush", (MediaColor)resources["SurfaceColor"]);
        UpdateBrush("BorderBrushSoft", (MediaColor)resources["BorderColor"]);
        UpdateBrush("PrimaryTextBrush", (MediaColor)resources["PrimaryTextColor"]);
        UpdateBrush("SecondaryTextBrush", (MediaColor)resources["SecondaryTextColor"]);
    }

    private static void UpdateBrush(string key, MediaColor color)
    {
        if (Current.Resources[key] is MediaSolidColorBrush brush) brush.Color = color;
    }

    private static MediaColor ColorFromHex(string value) => (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(value)!;
    private static string GetSettingsPath() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileMorph", "settings.json");
}