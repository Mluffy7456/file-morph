namespace FileMorph.Core.Models;

public sealed record ConversionRequest
{
    public ConversionRequest(
        string sourcePath,
        string destinationPath,
        ConversionOperation operation,
        string? outputFormat = null,
        int quality = 90,
        int? targetWidth = null,
        int? targetHeight = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100.");
        if (targetWidth is <= 0 or > 20000)
            throw new ArgumentOutOfRangeException(nameof(targetWidth), "Target width must be between 1 and 20000 pixels.");
        if (targetHeight is <= 0 or > 20000)
            throw new ArgumentOutOfRangeException(nameof(targetHeight), "Target height must be between 1 and 20000 pixels.");
        if (targetWidth is null && targetHeight is null && operation == ConversionOperation.Resize)
            throw new ArgumentException("At least one target dimension is required for resizing.", nameof(targetWidth));
        if (targetWidth.HasValue && targetHeight.HasValue && (long)targetWidth.Value * targetHeight.Value > 100_000_000)
            throw new ArgumentException("The target image cannot exceed 100 million pixels.", nameof(targetWidth));

        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        if (string.Equals(source, destination, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The destination must not overwrite the source file.", nameof(destinationPath));

        SourcePath = source;
        DestinationPath = destination;
        Operation = operation;
        OutputFormat = string.IsNullOrWhiteSpace(outputFormat)
            ? Path.GetExtension(destination).TrimStart('.').ToLowerInvariant()
            : outputFormat.Trim().TrimStart('.').ToLowerInvariant();
        Quality = quality;
        TargetWidth = targetWidth;
        TargetHeight = targetHeight;
    }

    public string SourcePath { get; }
    public string DestinationPath { get; }
    public ConversionOperation Operation { get; }
    public string OutputFormat { get; }
    /// <summary>Lossy encoder quality from 1 (smallest) to 100 (highest).</summary>
    public int Quality { get; }
    public int? TargetWidth { get; }
    public int? TargetHeight { get; }
}
