using FileMorph.Core.Abstractions;
using FileMorph.Core.Models;

namespace FileMorph.Infrastructure.Conversion;

/// <summary>
/// Runs queued conversions sequentially. Queue state is kept in memory only;
/// callers may persist completed items separately if they need history.
/// </summary>
public sealed class ConversionQueueService(IConversionService conversionService)
{
    private readonly object _sync = new();
    private readonly List<ConversionQueueItem> _items = [];
    private readonly SemaphoreSlim _runnerLock = new(1, 1);
    private CancellationTokenSource? _currentCancellation;

    public event Action<ConversionQueueItem>? ItemChanged;

    public IReadOnlyList<ConversionQueueItem> GetItems()
    {
        lock (_sync)
            return _items.ToArray();
    }

    public Guid Enqueue(ConversionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var item = new ConversionQueueItem(
            Guid.NewGuid(), request, ConversionQueueStatus.Queued, 0);

        lock (_sync)
            _items.Add(item);

        ItemChanged?.Invoke(item);
        return item.Id;
    }

    public bool Remove(Guid id)
    {
        ConversionQueueItem? removed;
        lock (_sync)
        {
            removed = _items.FirstOrDefault(item => item.Id == id);
            if (removed is null || removed.Status != ConversionQueueStatus.Queued)
                return false;

            _items.Remove(removed);
        }

        ItemChanged?.Invoke(removed with { Status = ConversionQueueStatus.Cancelled });
        return true;
    }

    public bool Retry(Guid id)
    {
        ConversionQueueItem? retry;
        lock (_sync)
        {
            var original = _items.FirstOrDefault(item => item.Id == id);
            if (original is null || original.Status is not (ConversionQueueStatus.Failed or ConversionQueueStatus.Cancelled))
                return false;

            retry = new ConversionQueueItem(Guid.NewGuid(), original.Request, ConversionQueueStatus.Queued, 0);
            _items.Add(retry);
        }

        ItemChanged?.Invoke(retry);
        return true;
    }

    public int ClearCompleted()
    {
        ConversionQueueItem[] removed;
        lock (_sync)
        {
            removed = _items.Where(item => item.IsTerminal).ToArray();
            foreach (var item in removed)
                _items.Remove(item);
        }

        foreach (var item in removed)
            ItemChanged?.Invoke(item);

        return removed.Length;
    }

    public void CancelCurrent()
    {
        lock (_sync)
            _currentCancellation?.Cancel();
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await _runnerLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ConversionQueueItem? next;
                lock (_sync)
                {
                    next = _items.FirstOrDefault(item => item.Status == ConversionQueueStatus.Queued);
                    if (next is null)
                        return;

                    next = next with { Status = ConversionQueueStatus.Running, Progress = 0 };
                    ReplaceItem(next);
                    _currentCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }

                ItemChanged?.Invoke(next);
                ConversionResult result;
                try
                {
                    var progress = new Progress<double>(value =>
                    {
                        var bounded = Math.Clamp(value, 0d, 100d);
                        ConversionQueueItem? updated;
                        lock (_sync)
                        {
                            var current = _items.FirstOrDefault(item => item.Id == next.Id);
                            if (current is null || current.Status != ConversionQueueStatus.Running)
                                return;

                            updated = current with { Progress = bounded };
                            ReplaceItem(updated);
                        }

                        ItemChanged?.Invoke(updated);
                    });

                    result = await conversionService.ConvertAsync(
                        next.Request, progress, _currentCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    result = ConversionResult.Failure("cancelled", "The operation was cancelled.", TimeSpan.Zero);
                }
                finally
                {
                    lock (_sync)
                    {
                        _currentCancellation?.Dispose();
                        _currentCancellation = null;
                    }
                }

                ConversionQueueItem completed;
                lock (_sync)
                {
                    var current = _items.First(item => item.Id == next.Id);
                    completed = result.Succeeded
                        ? current with
                        {
                            Status = ConversionQueueStatus.Succeeded,
                            Progress = 100,
                            OutputPath = result.OutputPath,
                            ErrorCode = null,
                            ErrorMessage = null
                        }
                        : current with
                        {
                            Status = result.ErrorCode == "cancelled"
                                ? ConversionQueueStatus.Cancelled
                                : ConversionQueueStatus.Failed,
                            ErrorCode = result.ErrorCode,
                            ErrorMessage = result.ErrorMessage
                        };

                    ReplaceItem(completed);
                }

                ItemChanged?.Invoke(completed);
            }
        }
        finally
        {
            _runnerLock.Release();
        }
    }

    private void ReplaceItem(ConversionQueueItem item)
    {
        var index = _items.FindIndex(existing => existing.Id == item.Id);
        if (index >= 0)
            _items[index] = item;
    }
}
