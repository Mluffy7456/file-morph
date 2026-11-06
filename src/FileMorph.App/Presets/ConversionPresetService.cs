using System.IO;
using System.Text.Json;

namespace FileMorph.App.Presets;

public sealed record ConversionPreset(string Name, string OutputFormat, int Quality);

public sealed class ConversionPresetService
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
        { "png", "jpg", "bmp", "tiff", "gif", "webp" };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly string _filePath;
    private List<ConversionPreset> _presets;

    public ConversionPresetService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileMorph", "presets.json");
        _presets = Load();
    }

    public IReadOnlyList<ConversionPreset> GetAll()
    {
        lock (_sync) return _presets.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public void Save(string name, string outputFormat, int quality)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is < 1 or > 40)
            throw new ArgumentException("Название пресета должно содержать от 1 до 40 символов.", nameof(name));
        var normalizedFormat = outputFormat?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedFormats.Contains(normalizedFormat))
            throw new ArgumentException("Выбранный формат не поддерживается.", nameof(outputFormat));
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), "Качество должно быть от 1 до 100.");

        lock (_sync)
        {
            var existing = _presets.FindIndex(p => string.Equals(p.Name, normalizedName, StringComparison.OrdinalIgnoreCase));
            var preset = new ConversionPreset(normalizedName, normalizedFormat, quality);
            if (existing >= 0) _presets[existing] = preset;
            else _presets.Add(preset);
            Persist();
        }
    }

    public bool Delete(string name)
    {
        lock (_sync)
        {
            var removed = _presets.RemoveAll(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) Persist();
            return removed;
        }
    }

    private List<ConversionPreset> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            var entries = JsonSerializer.Deserialize<List<ConversionPreset>>(File.ReadAllText(_filePath), JsonOptions) ?? [];
            return entries.Where(p => !string.IsNullOrWhiteSpace(p.Name) && p.Name.Length <= 40 &&
                SupportedFormats.Contains(p.OutputFormat) && p.Quality is >= 1 and <= 100)
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(g => g.Last()).ToList();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    private void Persist()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_presets, JsonOptions));
            File.Move(temporaryPath, _filePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Presets are optional and must not interrupt conversion.
        }
    }
}