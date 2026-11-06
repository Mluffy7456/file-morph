using FileMorph.Core.Models;

namespace FileMorph.Core.Abstractions;

public interface IFormatHandler
{
    FormatCapabilities Capabilities { get; }

    /// <summary>Executes the operation and reports progress as a percentage from 0 to 100.</summary>
    Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
