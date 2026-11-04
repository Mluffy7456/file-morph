namespace FileMorph.Core.Models;

public sealed record ConversionRequest
{
    public ConversionRequest(
        string sourcePath,
        string destinationPath,
        ConversionOperation operation,
        string? outputFormat = null,
        int quality = 90)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (quality is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(quality), "Quality must be between 1 and 100.");

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
    }

    public string SourcePath { get; }
    public string DestinationPath { get; }
    public ConversionOperation Operation { get; }
    public string OutputFormat { get; }
    /// <summary>Lossy encoder quality from 1 (smallest) to 100 (highest).</summary>
    public int Quality { get; }
}
