using FileMorph.Core.Models;

namespace FileMorph.Infrastructure.Conversion;

public enum ConversionQueueStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled
}

public sealed record ConversionQueueItem(
    Guid Id,
    ConversionRequest Request,
    ConversionQueueStatus Status,
    double Progress,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    string? OutputPath = null)
{
    public bool IsTerminal => Status is ConversionQueueStatus.Succeeded
        or ConversionQueueStatus.Failed
        or ConversionQueueStatus.Cancelled;
}
