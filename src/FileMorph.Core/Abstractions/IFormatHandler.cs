using FileMorph.Core.Models;

namespace FileMorph.Core.Abstractions;

public interface IFormatHandler
{
    FormatCapabilities Capabilities { get; }

    Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<double>? progress,
        CancellationToken cancellationToken);
}
