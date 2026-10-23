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

    [Fact]
    public void Constructor_DerivesOutputFormatFromDestination()
    {
        var request = new ConversionRequest(Path.Combine(Path.GetTempPath(), "sample.png"), Path.Combine(Path.GetTempPath(), "sample.jpg"), ConversionOperation.Convert);
        Assert.Equal("jpg", request.OutputFormat);
    }
}
