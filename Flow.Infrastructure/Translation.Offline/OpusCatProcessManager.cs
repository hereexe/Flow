using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Translation.Offline;

public class OpusCatProcessManager : IOpusCatProcessManager
{
    private readonly OpusCatOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpusCatProcessManager>? _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private Process? _process;
    private bool _isDisposed;
    private bool _isStarted;

    public bool IsRunning => _isStarted && (_process == null || !_process.HasExited);

    public OpusCatProcessManager(
        OpusCatOptions options,
        HttpClient httpClient,
        ILogger<OpusCatProcessManager>? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (_isStarted && await CheckHealthAsync(ct))
        {
            return;
        }

        await _semaphore.WaitAsync(ct);
        try
        {
            if (_isStarted && await CheckHealthAsync(ct))
            {
                return;
            }

            _logger?.LogInformation("Starting OPUS-CAT offline translation sidecar process...");

            string fullPath = ResolveExecutablePath(_options.ExecutablePath);
            if (!File.Exists(fullPath))
            {
                _logger?.LogError("OPUS-CAT executable not found at path: {Path}", fullPath);
                throw new OpusCatExecutableNotFoundException(fullPath);
            }

            if (IsPortInUse(_options.Port))
            {
                // Check if existing listener is healthy
                if (await CheckHealthAsync(ct))
                {
                    _logger?.LogInformation("Found running OPUS-CAT instance listening on port {Port}", _options.Port);
                    _isStarted = true;
                    return;
                }

                _logger?.LogError("Port {Port} is in use by another non-OPUS-CAT process", _options.Port);
                throw new OpusCatPortInUseException(_options.Port);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = fullPath,
                Arguments = $"--port {_options.Port}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            if (!string.IsNullOrEmpty(_options.ModelPath))
            {
                startInfo.Arguments += $" --model-dir \"{_options.ModelPath}\"";
            }

            try
            {
                _process = StartProcess(startInfo);
            }
            catch (Exception ex) when (ex is not OpusCatException)
            {
                _logger?.LogError(ex, "Failed to launch OPUS-CAT process");
                throw new OpusCatException($"Не удалось запустить процесс офлайн-переводчика: {ex.Message}", ex);
            }

            // Wait for process readiness
            var timeout = TimeSpan.FromSeconds(_options.StartupTimeoutSeconds);
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeout)
            {
                ct.ThrowIfCancellationRequested();

                if (_process.HasExited)
                {
                    int exitCode = _process.ExitCode;
                    _logger?.LogError("OPUS-CAT sidecar process exited prematurely with code {ExitCode}", exitCode);
                    throw new OpusCatProcessCrashedException(exitCode);
                }

                if (await CheckHealthAsync(ct))
                {
                    _isStarted = true;
                    _logger?.LogInformation("OPUS-CAT sidecar process successfully initialized on port {Port}", _options.Port);
                    return;
                }

                await Task.Delay(250, ct);
            }

            // Timed out
            Stop();
            _logger?.LogError("OPUS-CAT sidecar process startup timed out after {Timeout} seconds", _options.StartupTimeoutSeconds);
            throw new OpusCatStartupTimeoutException(_options.StartupTimeoutSeconds);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<bool> CheckHealthAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_options.BaseUrl}/health", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public void Stop()
    {
        _isStarted = false;
        if (_process != null && !_process.HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error while stopping OPUS-CAT process");
            }
            finally
            {
                _process.Dispose();
                _process = null;
            }
        }
    }

    protected virtual Process StartProcess(ProcessStartInfo startInfo)
    {
        return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start process.");
    }

    private static string ResolveExecutablePath(string executablePath)
    {
        if (Path.IsPathRooted(executablePath))
        {
            return executablePath;
        }
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, executablePath);
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
            return tcpListeners.Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Stop();
        _semaphore.Dispose();
    }
}
