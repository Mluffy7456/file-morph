using FileMorph.Core.Models;

namespace FileMorph.Core.Abstractions;

public interface IConversionService
{
    IReadOnlyCollection<FormatCapabilities> GetCapabilities();

    Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
