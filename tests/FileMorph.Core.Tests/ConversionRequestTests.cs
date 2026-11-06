using FileMorph.Core.Models;

namespace FileMorph.Core.Tests;

public sealed class ConversionRequestTests
{
    [Fact]
    public void Constructor_NormalizesPathsAndOutputFormat()
    {
        var source = Path.Combine(Path.GetTempPath(), "sample.png");
        var destination = Path.Combine(Path.GetTempPath(), "sample.webp");
        var request = new ConversionRequest(source, destination, ConversionOperation.Convert, ".WEBP");
        Assert.Equal(Path.GetFullPath(source), request.SourcePath);
        Assert.Equal(Path.GetFullPath(destination), request.DestinationPath);
        Assert.Equal("webp", request.OutputFormat);
    }

    [Fact]
    public void Constructor_RejectsSameSourceAndDestination()
    {
        var source = Path.Combine(Path.GetTempPath(), "same.png");
        Assert.Throws<ArgumentException>(() => new ConversionRequest(source, source, ConversionOperation.Convert));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Constructor_RejectsQualityOutsideRange(int quality)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConversionRequest(
            Path.Combine(Path.GetTempPath(), "source.png"),
            Path.Combine(Path.GetTempPath(), "destination.jpg"),
            ConversionOperation.Convert,
            quality: quality));
    }

    [Fact]
    public void Constructor_PreservesQuality()
    {
        var request = new ConversionRequest(
            Path.Combine(Path.GetTempPath(), "source.png"),
            Path.Combine(Path.GetTempPath(), "destination.jpg"),
            ConversionOperation.Convert,
            quality: 65);
        Assert.Equal(65, request.Quality);
    }

    [Fact]
    public void Constructor_PreservesResizeDimensions()
    {
        var request = new ConversionRequest(
            Path.Combine(Path.GetTempPath(), "source.png"),
            Path.Combine(Path.GetTempPath(), "destination.png"),
            ConversionOperation.Resize,
            targetWidth: 800,
            targetHeight: 600);
        Assert.Equal(800, request.TargetWidth);
        Assert.Equal(600, request.TargetHeight);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(20001, null)]
    [InlineData(null, 0)]
    [InlineData(null, 20001)]
    public void Constructor_RejectsInvalidResizeDimensions(int? width, int? height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ConversionRequest(
            Path.Combine(Path.GetTempPath(), "source.png"),
            Path.Combine(Path.GetTempPath(), "destination.png"),
            ConversionOperation.Convert,
            targetWidth: width,
            targetHeight: height));
    }

    [Fact]
    public void Constructor_ResizeRequiresAtLeastOneDimension()
    {
        Assert.Throws<ArgumentException>(() => new ConversionRequest(
            Path.Combine(Path.GetTempPath(), "source.png"),
            Path.Combine(Path.GetTempPath(), "destination.png"),
            ConversionOperation.Resize));
    }

    [Fact]
    public void Constructor_DerivesOutputFormatFromDestination()
    {
        var request = new ConversionRequest(Path.Combine(Path.GetTempPath(), "sample.png"), Path.Combine(Path.GetTempPath(), "sample.jpg"), ConversionOperation.Convert);
        Assert.Equal("jpg", request.OutputFormat);
    }
}
