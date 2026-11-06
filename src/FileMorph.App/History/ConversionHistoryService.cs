using System.IO;
using System.Text.Json;

namespace FileMorph.App.History;

public sealed record ConversionHistoryEntry(DateTimeOffset CompletedAt, string SourcePath, string DestinationPath, bool Succeeded, string? ErrorMessage)
{
    public string DisplayText => $"{CompletedAt.LocalDateTime:dd.MM.yyyy HH:mm} · {Path.GetFileName(SourcePath)} → {Path.GetFileName(DestinationPath)} · {(Succeeded ? "Готово" : "Ошибка")}";
    public string DetailText => Succeeded ? DestinationPath : ErrorMessage ?? "Неизвестная ошибка";
}

public sealed class ConversionHistoryService
{
    private const int MaximumEntries = 100;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _sync = new();
    private readonly string _filePath;
    private List<ConversionHistoryEntry> _entries;

    public ConversionHistoryService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FileMorph", "history.json");
        _entries = Load();
    }

    public IReadOnlyList<ConversionHistoryEntry> GetEntries()
    {
        lock (_sync) return _entries.ToArray();
    }

    public void Add(ConversionHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_sync)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > MaximumEntries) _entries.RemoveRange(MaximumEntries, _entries.Count - MaximumEntries);
            Save();
        }
    }

    public void Clear()
    {
        lock (_sync) { _entries.Clear(); Save(); }
    }

    private List<ConversionHistoryEntry> Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return [];
            return JsonSerializer.Deserialize<List<ConversionHistoryEntry>>(File.ReadAllText(_filePath), JsonOptions)?.Take(MaximumEntries).ToList() ?? [];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException) { return []; }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (string.IsNullOrWhiteSpace(directory)) return;
            Directory.CreateDirectory(directory);
            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_entries, JsonOptions));
            File.Move(temporaryPath, _filePath, true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // History is best-effort and must never interrupt a conversion.
        }
    }
}