using FileMorph.Core.Abstractions;
using FileMorph.Core.Models;

namespace FileMorph.Infrastructure.Conversion;

public sealed class ConversionService(IEnumerable<IFormatHandler> handlers) : IConversionService
{
    private readonly IReadOnlyList<IFormatHandler> _handlers = handlers.ToArray();

    public IReadOnlyCollection<FormatCapabilities> GetCapabilities() =>
        _handlers.Select(handler => handler.Capabilities).ToArray();

    public async Task<ConversionResult> ConvertAsync(
        ConversionRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(request.SourcePath))
            return ConversionResult.Failure("source_not_found", "The source file does not exist.", TimeSpan.Zero);

        var inputExtension = Path.GetExtension(request.SourcePath);
        var outputExtension = Path.GetExtension(request.DestinationPath);
        var handler = _handlers.FirstOrDefault(candidate =>
            candidate.Capabilities.SupportsInput(inputExtension) &&
            candidate.Capabilities.Supports(request.Operation) &&
            (request.Operation != ConversionOperation.Convert ||
             candidate.Capabilities.SupportsOutput(outputExtension)));

        if (handler is null)
            return ConversionResult.Failure("unsupported_operation", "No installed handler supports this input, output, and operation combination.", TimeSpan.Zero);

        if (File.Exists(request.DestinationPath))
            return ConversionResult.Failure("destination_exists", "The destination already exists. Choose another output path.", TimeSpan.Zero);

        try
        {
            var result = await handler.ExecuteAsync(request, progress, cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
                return result;

            if (string.IsNullOrWhiteSpace(result.OutputPath) || !File.Exists(result.OutputPath))
                return ConversionResult.Failure("output_missing", "The handler reported success, but the output file was not found.", result.Duration);

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ConversionResult.Failure("cancelled", "The operation was cancelled.", TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return ConversionResult.Failure("conversion_failed", exception.Message, TimeSpan.Zero);
        }
    }
}
