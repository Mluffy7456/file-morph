using FileMorph.Core.Abstractions;
using FileMorph.Core.Models;
using FileMorph.Infrastructure.Conversion;

namespace FileMorph.Core.Tests;

public sealed class ConversionQueueServiceTests
{
    [Fact]
    public async Task RunAsync_ProcessesQueuedItemsSequentiallyAndTracksResults()
    {
        using var files = new TemporaryFiles();
        var firstSource = files.CreateFile("first.png");
        var secondSource = files.CreateFile("second.png");
        var conversion = new FakeConversionService();
        var queue = new ConversionQueueService(conversion);

        var firstId = queue.Enqueue(new ConversionRequest(
            firstSource, files.PathFor("first-out.png"), ConversionOperation.Convert));
        var secondId = queue.Enqueue(new ConversionRequest(
            secondSource, files.PathFor("second-out.png"), ConversionOperation.Convert));

        await queue.RunAsync();

        var items = queue.GetItems();
        Assert.Equal(2, items.Count);
        Assert.Equal(ConversionQueueStatus.Succeeded, items.Single(item => item.Id == firstId).Status);
        Assert.Equal(ConversionQueueStatus.Succeeded, items.Single(item => item.Id == secondId).Status);
        Assert.Equal(2, conversion.Calls);
        Assert.True(File.Exists(files.PathFor("first-out.png")));
        Assert.True(File.Exists(files.PathFor("second-out.png")));
    }

    [Fact]
    public async Task RunAsync_ContinuesAfterFailedItem()
    {
        using var files = new TemporaryFiles();
        var firstSource = files.CreateFile("first.png");
        var secondSource = files.CreateFile("second.png");
        var conversion = new FakeConversionService { FailFirst = true };
        var queue = new ConversionQueueService(conversion);

        var failedId = queue.Enqueue(new ConversionRequest(
            firstSource, files.PathFor("first-out.png"), ConversionOperation.Convert));
        var successfulId = queue.Enqueue(new ConversionRequest(
            secondSource, files.PathFor("second-out.png"), ConversionOperation.Convert));

        await queue.RunAsync();

        var items = queue.GetItems();
        Assert.Equal(ConversionQueueStatus.Failed, items.Single(item => item.Id == failedId).Status);
        Assert.Equal(ConversionQueueStatus.Succeeded, items.Single(item => item.Id == successfulId).Status);
    }

    [Fact]
    public async Task Remove_OnlyRemovesQueuedItems()
    {
        using var files = new TemporaryFiles();
        var source = files.CreateFile("input.png");
        var queue = new ConversionQueueService(new FakeConversionService());
        var id = queue.Enqueue(new ConversionRequest(
            source, files.PathFor("output.png"), ConversionOperation.Convert));

        Assert.True(queue.Remove(id));
        Assert.Empty(queue.GetItems());
        await queue.RunAsync();
    }

    private sealed class FakeConversionService : IConversionService
    {
        public int Calls { get; private set; }
        public bool FailFirst { get; init; }

        public IReadOnlyCollection<FormatCapabilities> GetCapabilities() => [];

        public Task<ConversionResult> ConvertAsync(
            ConversionRequest request,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            if (FailFirst && Calls == 1)
                return Task.FromResult(ConversionResult.Failure(
                    "conversion_failed", "Expected test failure.", TimeSpan.Zero));

            Directory.CreateDirectory(Path.GetDirectoryName(request.DestinationPath)!);
            File.WriteAllText(request.DestinationPath, "converted");
            progress?.Report(100);
            return Task.FromResult(ConversionResult.Success(request.DestinationPath, TimeSpan.Zero));
        }
    }

    private sealed class TemporaryFiles : IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(), "FileMorph-queue-tests-" + Guid.NewGuid().ToString("N"));

        public TemporaryFiles() => Directory.CreateDirectory(_directory);

        public string PathFor(string name) => Path.Combine(_directory, name);

        public string CreateFile(string name)
        {
            var path = PathFor(name);
            File.WriteAllText(path, "source");
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }
    }
}
