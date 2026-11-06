using FileMorph.App.History;
using System.Text.Json;

namespace FileMorph.Core.Tests;

public sealed class ConversionHistoryServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FileMorphTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "history.json");

    [Fact]
    public void Add_PersistsEntriesAndNewestAppearsFirst()
    {
        var service = new ConversionHistoryService(FilePath);
        var older = new ConversionHistoryEntry(DateTimeOffset.UtcNow.AddMinutes(-1), "old.png", "old.jpg", true, null);
        var newer = new ConversionHistoryEntry(DateTimeOffset.UtcNow, "new.png", "new.webp", false, "codec error");

        service.Add(older);
        service.Add(newer);

        var loaded = new ConversionHistoryService(FilePath).GetEntries();
        Assert.Equal(2, loaded.Count);
        Assert.Equal("new.png", loaded[0].SourcePath);
        Assert.Equal("old.png", loaded[1].SourcePath);
        Assert.Equal("codec error", loaded[0].ErrorMessage);
    }

    [Fact]
    public void Add_KeepsOnlyLatestOneHundredEntries()
    {
        var service = new ConversionHistoryService(FilePath);
        for (var i = 0; i < 105; i++)
            service.Add(new ConversionHistoryEntry(DateTimeOffset.UtcNow.AddMinutes(i), $"{i}.png", $"{i}.jpg", true, null));

        var entries = service.GetEntries();
        Assert.Equal(100, entries.Count);
        Assert.Equal("104.png", entries[0].SourcePath);
        Assert.Equal("5.png", entries[^1].SourcePath);
    }

    [Fact]
    public void Clear_RemovesEntriesFromMemoryAndDisk()
    {
        var service = new ConversionHistoryService(FilePath);
        service.Add(new ConversionHistoryEntry(DateTimeOffset.UtcNow, "a.png", "a.jpg", true, null));

        service.Clear();

        Assert.Empty(service.GetEntries());
        Assert.Empty(new ConversionHistoryService(FilePath).GetEntries());
    }

    [Fact]
    public void Constructor_HandlesMalformedHistoryFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "{ invalid json");

        var service = new ConversionHistoryService(FilePath);

        Assert.Empty(service.GetEntries());
    }

    [Fact]
    public void GetEntries_ReturnsSnapshotNotMutableBackingCollection()
    {
        var service = new ConversionHistoryService(FilePath);
        service.Add(new ConversionHistoryEntry(DateTimeOffset.UtcNow, "a.png", "a.jpg", true, null));
        var snapshot = service.GetEntries();

        Assert.IsAssignableFrom<IReadOnlyList<ConversionHistoryEntry>>(snapshot);
        Assert.Single(snapshot);
        Assert.Single(service.GetEntries());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}