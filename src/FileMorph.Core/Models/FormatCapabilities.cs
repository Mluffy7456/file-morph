using System.Collections.Immutable;

namespace FileMorph.Core.Models;

public sealed record FormatCapabilities
{
    public FormatCapabilities(
        string handlerId,
        IEnumerable<string> inputExtensions,
        IEnumerable<string> outputExtensions,
        IEnumerable<ConversionOperation> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerId);
        ArgumentNullException.ThrowIfNull(inputExtensions);
        ArgumentNullException.ThrowIfNull(outputExtensions);
        ArgumentNullException.ThrowIfNull(operations);

        HandlerId = handlerId.Trim();
        InputExtensions = NormalizeExtensions(inputExtensions);
        OutputExtensions = NormalizeExtensions(outputExtensions);
        Operations = operations.ToImmutableHashSet();

        if (InputExtensions.Count == 0)
            throw new ArgumentException("At least one input extension is required.", nameof(inputExtensions));
        if (Operations.Count == 0)
            throw new ArgumentException("At least one operation is required.", nameof(operations));
    }

    public string HandlerId { get; }
    public ImmutableHashSet<string> InputExtensions { get; }
    public ImmutableHashSet<string> OutputExtensions { get; }
    public ImmutableHashSet<ConversionOperation> Operations { get; }

    public bool SupportsInput(string extension) => InputExtensions.Contains(NormalizeExtension(extension));
    public bool SupportsOutput(string extension) => OutputExtensions.Contains(NormalizeExtension(extension));
    public bool Supports(ConversionOperation operation) => Operations.Contains(operation);

    private static ImmutableHashSet<string> NormalizeExtensions(IEnumerable<string> extensions) =>
        extensions.Where(extension => !string.IsNullOrWhiteSpace(extension))
            .Select(NormalizeExtension)
            .ToImmutableHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeExtension(string extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        var normalized = extension.Trim();
        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }
}