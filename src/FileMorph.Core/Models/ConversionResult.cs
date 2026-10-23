namespace FileMorph.Core.Models;

public sealed record ConversionResult(bool Succeeded, string? OutputPath, string? ErrorCode, string? ErrorMessage, TimeSpan Duration)
{
    public static ConversionResult Success(string outputPath, TimeSpan duration) =>
        new(true, outputPath, null, null, duration);

    public static ConversionResult Failure(string errorCode, string errorMessage, TimeSpan duration) =>
        new(false, null, errorCode, errorMessage, duration);
}
