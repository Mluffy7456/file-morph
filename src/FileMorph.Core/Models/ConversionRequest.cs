namespace FileMorph.Core.Models;

public sealed record ConversionRequest
{
    public ConversionRequest(string sourcePath, string destinationPath, ConversionOperation operation, string? outputFormat = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

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
    }

    public string SourcePath { get; }
    public string DestinationPath { get; }
    public ConversionOperation Operation { get; }
    public string OutputFormat { get; }
}
