using System.IO;

namespace Flow.Infrastructure.Translation.Offline;

public class OpusCatOptions
{
    public int Port { get; set; } = 8500;
    public string ExecutablePath { get; set; } = Path.Combine("OpusCat", "OpusCat.Engine.exe");
    public int StartupTimeoutSeconds { get; set; } = 10;
    public string? ModelPath { get; set; }

    public string BaseUrl => $"http://localhost:{Port}";
}
