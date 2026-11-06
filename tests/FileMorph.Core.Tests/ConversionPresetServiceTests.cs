using FileMorph.App.Presets;

namespace FileMorph.Core.Tests;

public sealed class ConversionPresetServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "FileMorphPresetTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_directory, "presets.json");

    [Fact]
    public void Save_PersistsPresetAndReplacesNameCaseInsensitively()
    {
        var service = new ConversionPresetService(FilePath);
        service.Save("Web Optimized", "webp", 82);
        service.Save("web optimized", "jpg", 75);

        var loaded = new ConversionPresetService(FilePath).GetAll();
        var preset = Assert.Single(loaded);
        Assert.Equal("web optimized", preset.Name);
        Assert.Equal("jpg", preset.OutputFormat);
        Assert.Equal(75, preset.Quality);
    }

    [Fact]
    public void Save_RejectsInvalidFormatAndQuality()
    {
        var service = new ConversionPresetService(FilePath);
        Assert.Throws<ArgumentException>(() => service.Save("Unsupported", "avif", 90));
        Assert.Throws<ArgumentOutOfRangeException>(() => service.Save("Bad quality", "jpg", 0));
        Assert.Empty(service.GetAll());
    }

    [Fact]
    public void Delete_RemovesPresetFromDisk()
    {
        var service = new ConversionPresetService(FilePath);
        service.Save("JPEG", "jpg", 90);

        Assert.True(service.Delete("jpeg"));
        Assert.Empty(new ConversionPresetService(FilePath).GetAll());
        Assert.False(service.Delete("missing"));
    }

    [Fact]
    public void Load_IgnoresMalformedJson()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, "not-json");

        Assert.Empty(new ConversionPresetService(FilePath).GetAll());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}