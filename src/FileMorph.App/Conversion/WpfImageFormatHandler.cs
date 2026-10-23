using System.Diagnostics;
using System.IO;
using System.Windows.Media.Imaging;
using FileMorph.Core.Abstractions;
using FileMorph.Core.Models;

namespace FileMorph.App.Conversion;

/// <summary>
/// Converts common raster formats using the Windows Imaging Component available through WPF.
/// This handler is intentionally limited to formats supported by the built-in WPF encoders.
/// </summary>
public sealed class WpfImageFormatHandler : IFormatHandler
{
    private static readonly IReadOnlyDictionary<string, Func<BitmapEncoder>> Encoders =
        new Dictionary<string, Func<BitmapEncoder>>(StringComparer.OrdinalIgnoreCase)
        {
            ["png"] = static () => new PngBitmapEncoder(),
            ["jpg"] = static () => new JpegBitmapEncoder { QualityLevel = 92 },
            ["jpeg"] = static () => new JpegBitmapEncoder { QualityLevel = 92 },
            ["bmp"] = static () => new BmpBitmapEncoder(),
            ["gif"] = static () => new GifBitmapEncoder(),
            ["tif"] = static () => new TiffBitmapEncoder(),
            ["tiff"] = static () => new TiffBitmapEncoder()
        };

    public FormatCapabilities Capabilities { get; } = new(
        "builtin-wic-raster",
        ["png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff"],
        ["png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff"],
        [ConversionOperation.Convert]);

    public async Task<ConversionResult> ExecuteAsync(
        ConversionRequest request,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stopwatch = Stopwatch.StartNew();

        if (!Encoders.TryGetValue(request.OutputFormat, out var createEncoder))
            return ConversionResult.Failure(
                "unsupported_output_format",
                $"The built-in image engine cannot write '{request.OutputFormat}'.",
                stopwatch.Elapsed);

        if (!File.Exists(request.SourcePath))
            return ConversionResult.Failure("source_not_found", "The source file does not exist.", stopwatch.Elapsed);

        var destinationDirectory = Path.GetDirectoryName(request.DestinationPath);
        if (string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(destinationDirectory))
            return ConversionResult.Failure("destination_directory_missing", "The destination folder does not exist.", stopwatch.Elapsed);

        if (File.Exists(request.DestinationPath))
            return ConversionResult.Failure("destination_exists", "The destination already exists. Choose another output path.", stopwatch.Elapsed);

        var temporaryPath = Path.Combine(
            destinationDirectory,
            $".filemorph-{Guid.NewGuid():N}.tmp");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.05);

            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var sourceStream = new FileStream(
                    request.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 64 * 1024, FileOptions.SequentialScan);
                var decoder = BitmapDecoder.Create(
                    sourceStream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);

                if (decoder.Frames.Count == 0)
                    throw new InvalidDataException("The source does not contain a readable image frame.");

                var encoder = createEncoder();
                encoder.Frames.Add(BitmapFrame.Create(decoder.Frames[0]));

                using var outputStream = new FileStream(
                    temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 64 * 1024, FileOptions.SequentialScan);
                encoder.Save(outputStream);
                outputStream.Flush(flushToDisk: true);
            }, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(0.9);

            // File.Move without overwrite is the final guard against destination races.
            File.Move(temporaryPath, request.DestinationPath);
            progress?.Report(1);
            stopwatch.Stop();
            return ConversionResult.Success(request.DestinationPath, stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return ConversionResult.Failure("cancelled", "The operation was cancelled.", stopwatch.Elapsed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException)
        {
            stopwatch.Stop();
            return ConversionResult.Failure("image_conversion_failed", exception.Message, stopwatch.Elapsed);
        }
        finally
        {
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Cleanup is best-effort; never replace the original conversion error.
            }
            catch (UnauthorizedAccessException)
            {
                // Cleanup is best-effort; never replace the original conversion error.
            }
        }
    }
}