namespace Flow.Infrastructure.Translation.Offline;

public class OpusCatException : Exception
{
    public OpusCatException(string message) : base(message) { }
    public OpusCatException(string message, Exception innerException) : base(message, innerException) { }
}

public class OpusCatExecutableNotFoundException : OpusCatException
{
    public string ExecutablePath { get; }

    public OpusCatExecutableNotFoundException(string path)
        : base($"Компоненты офлайн-перевода не найдены по пути '{path}'. Проверьте установку.")
    {
        ExecutablePath = path;
    }
}

public class OpusCatPortInUseException : OpusCatException
{
    public int Port { get; }

    public OpusCatPortInUseException(int port)
        : base($"Локальный порт {port} уже занят другим приложением.")
    {
        Port = port;
    }
}

public class OpusCatStartupTimeoutException : OpusCatException
{
    public int TimeoutSeconds { get; }

    public OpusCatStartupTimeoutException(int timeoutSeconds)
        : base($"Превышено время ожидания запуска офлайн-движка ({timeoutSeconds} сек).")
    {
        TimeoutSeconds = timeoutSeconds;
    }
}

public class OpusCatProcessCrashedException : OpusCatException
{
    public int ExitCode { get; }

    public OpusCatProcessCrashedException(int exitCode)
        : base($"Офлайн-движок завершился с ошибкой (код выхода: {exitCode}).")
    {
        ExitCode = exitCode;
    }
}
