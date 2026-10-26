using FileMorph.Core.Abstractions;
using FileMorph.Core.Models;
using FileMorph.Infrastructure.Conversion;

namespace FileMorph.Core.Tests;

public sealed class ConversionServiceTests
{
    [Fact]
    public async Task ConvertAsync_ReturnsSourceNotFound_WhenSourceDoesNotExist()
    {
        var service = new ConversionService([new FakeHandler()]);
        var request = Request(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png"));

        var result = await service.ConvertAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("source_not_found", result.ErrorCode);
    }

    [Fact]
    public async Task ConvertAsync_ReturnsUnsupportedOperation_WhenNoHandlerMatches()
    {
        using var files = new TemporaryFiles();
        var source = files.CreateFile("input.png");
        var service = new ConversionService([]);
        var request = new ConversionRequest(source, files.PathFor("output.webp"), ConversionOperation.Convert, "webp");

        var result = await service.ConvertAsync(request);

        Assert.False(result.Succeeded);
        Assert.Equal("unsupported_operation", result.ErrorCode);
    }

    [Fact]
    public async Task ConvertAsync_DoesNotOverwriteExistingDestination()
    {
        using var files = new TemporaryFiles();
        var source = files.CreateFile("input.png");
        var destination = files.CreateFile("output.png", "keep me");
        var handler = new FakeHandler();
        var service = new ConversionService([handler]);

        var result = await service.ConvertAsync(new ConversionRequest(source, destination, ConversionOperation.Convert));

        Assert.False(result.Succeeded);
        Assert.Equal("destination_exists", result.ErrorCode);
        Assert.Equal("keep me", await File.ReadAllTextAsync(destination));
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task ConvertAsync_RejectsSuccessWhenHandlerDidNotCreateOutput()
    {
        using var files = new TemporaryFiles();
        var source = files.CreateFile("input.png");
        var destination = files.PathFor("output.png");
        var handler = new FakeHandler
        {
            ResultFactory = (_, _) => ConversionResult.Success(destination, TimeSpan.FromMilliseconds(1))
        };
        var service = new ConversionService([handler]);

        var result = await service.ConvertAsync(new ConversionRequest(source, destination, ConversionOperation.Convert));

        Assert.False(result.Succeeded);
        Assert.Equal("output_missing", result.ErrorCode);
    }

    private static ConversionRequest Request(string sourcePath) =>
        new(sourcePath, Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png"), ConversionOperation.Convert);

    private sealed class FakeHandler : IFormatHandler
    {
        public FormatCapabilities Capabilities { get; } = new(
            "test-handler", ["png"], ["png"], [ConversionOperation.Convert]);

        public bool WasCalled { get; private set; }

        public Func<ConversionRequest, CancellationToken, ConversionResult> ResultFactory { get; set; } =
            (request, _) =>
            {
                File.WriteAllText(request.DestinationPath, "converted");
                return ConversionResult.Success(request.DestinationPath, TimeSpan.FromMilliseconds(1));
            };

        public Task<ConversionResult> ExecuteAsync(
            ConversionRequest request,
            IProgress<double>? progress,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(ResultFactory(request, cancellationToken));
        }
    }

    private sealed class TemporaryFiles : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "FileMorph-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryFiles() => Directory.CreateDirectory(_directory);

        public string PathFor(string fileName) => System.IO.Path.Combine(_directory, fileName);

        public string CreateFile(string fileName, string contents = "source")
        {
            var path = PathFor(fileName);
            File.WriteAllText(path, contents);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}