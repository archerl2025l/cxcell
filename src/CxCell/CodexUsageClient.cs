using System.Diagnostics;
using System.Text.Json;

namespace CxCell;

public sealed class CodexUsageClient : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Process? _process;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private int _nextId;

    public async Task<QuotaSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureStartedAsync(cancellationToken);
            var result = await RequestAsync(
                "account/rateLimits/read",
                new { excludeResetCreditDetails = true },
                cancellationToken);

            return QuotaParser.Parse(result);
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureStartedAsync(CancellationToken cancellationToken)
    {
        if (_process is { HasExited: false } && _stdin is not null && _stdout is not null)
            return;

        var psi = new ProcessStartInfo
        {
            FileName = "codex",
            Arguments = "app-server --listen stdio://",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _process = Process.Start(psi)
            ?? throw new InvalidOperationException("Unable to start the Codex CLI.");

        _stdin = _process.StandardInput;
        _stdout = _process.StandardOutput;

        _ = Task.Run(async () =>
        {
            try
            {
                while (!_process.HasExited)
                {
                    _ = await _process.StandardError.ReadLineAsync();
                }
            }
            catch
            {
                // Best-effort drain only.
            }
        }, CancellationToken.None);

        _ = await RequestAsync(
            "initialize",
            new
            {
                clientInfo = new
                {
                    name = "cxcell",
                    title = "CxCell",
                    version = "0.1.0"
                },
                capabilities = new
                {
                    experimentalApi = false
                }
            },
            cancellationToken);

        await WriteAsync(new { method = "initialized" }, cancellationToken);
    }

    private async Task<JsonElement> RequestAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        if (_stdout is null)
            throw new InvalidOperationException("Codex app-server is not running.");

        var id = Interlocked.Increment(ref _nextId);
        await WriteAsync(new { id, method, @params = parameters }, cancellationToken);

        while (true)
        {
            var line = await _stdout.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new IOException("Codex app-server closed stdout.");

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;

            if (!root.TryGetProperty("id", out var responseId) ||
                responseId.ValueKind != JsonValueKind.Number ||
                responseId.GetInt32() != id)
            {
                continue;
            }

            if (root.TryGetProperty("error", out var error))
                throw new InvalidOperationException($"Codex app-server error: {error}");

            if (!root.TryGetProperty("result", out var result))
                throw new InvalidOperationException("Codex app-server response did not include result.");

            return result.Clone();
        }
    }

    private async Task WriteAsync(object message, CancellationToken cancellationToken)
    {
        if (_stdin is null)
            throw new InvalidOperationException("Codex app-server is not running.");

        var json = JsonSerializer.Serialize(message);
        await _stdin.WriteLineAsync(json.AsMemory(), cancellationToken);
        await _stdin.FlushAsync(cancellationToken);
    }

    private Task ResetAsync()
    {
        if (_process is not null)
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore shutdown races.
            }

            _process.Dispose();
        }

        _process = null;
        _stdin = null;
        _stdout = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            await ResetAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
