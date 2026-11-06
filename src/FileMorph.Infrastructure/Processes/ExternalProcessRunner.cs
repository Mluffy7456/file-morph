using System.Diagnostics;
using System.Text;

namespace FileMorph.Infrastructure.Processes;

public sealed record ExternalProcessResult(int ExitCode, string StandardOutput, string StandardError, TimeSpan Duration);

/// <summary>Runs a local engine without shell interpretation and bounds captured diagnostics.</summary>
public sealed class ExternalProcessRunner
{
    private const int MaxCapturedCharacters = 64 * 1024;

    public async Task<ExternalProcessResult> RunAsync(
        string executablePath,
        IEnumerable<string> arguments,
        string? workingDirectory = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.GetFullPath(executablePath),
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : Path.GetFullPath(workingDirectory),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("The conversion engine process could not be started.");

            using var timeoutSource = timeout is { } duration
                ? new CancellationTokenSource(duration)
                : new CancellationTokenSource();
            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

            var stdoutTask = ReadBoundedAsync(process.StandardOutput, linkedSource.Token);
            var stderrTask = ReadBoundedAsync(process.StandardError, linkedSource.Token);

            try
            {
                await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);
            return new ExternalProcessResult(process.ExitCode, stdout, stderr, stopwatch.Elapsed);
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var captured = new StringBuilder();
        var truncated = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;

            var remaining = MaxCapturedCharacters - captured.Length;
            if (remaining > 0)
                captured.Append(buffer, 0, Math.Min(read, remaining));
            if (read > remaining)
                truncated = true;
        }

        if (truncated)
            captured.AppendLine().Append("[output truncated]");

        return captured.ToString();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}
