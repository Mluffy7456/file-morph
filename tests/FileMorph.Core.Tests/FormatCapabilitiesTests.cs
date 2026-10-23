using FileMorph.Core.Models;

namespace FileMorph.Core.Tests;

public sealed class FormatCapabilitiesTests
{
    [Fact]
    public void Extensions_AreNormalizedAndComparedCaseInsensitively()
    {
        var capabilities = new FormatCapabilities("image-handler", ["PNG", ".JpG"], ["webp"], [ConversionOperation.Convert, ConversionOperation.Resize]);
        Assert.True(capabilities.SupportsInput(".png"));
        Assert.True(capabilities.SupportsInput("JPG"));
        Assert.True(capabilities.SupportsOutput(".WEBP"));
        Assert.True(capabilities.Supports(ConversionOperation.Resize));
        Assert.False(capabilities.Supports(ConversionOperation.MergePdf));
    }

    [Fact]
    public void Constructor_RejectsEmptyOperations()
    {
        Assert.Throws<ArgumentException>(() => new FormatCapabilities("handler", ["png"], ["webp"], []));
    }
}
