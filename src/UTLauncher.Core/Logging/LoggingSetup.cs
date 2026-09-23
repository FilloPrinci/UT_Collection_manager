using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using UTLauncher.Core.Platform;
using ILogger = Serilog.ILogger;

namespace UTLauncher.Core.Logging;

public sealed record LoggingSession(ILoggerFactory Factory, InMemorySink MemorySink, ILogger SerilogLogger) : IDisposable
{
    public void Dispose()
    {
        Factory.Dispose();
        (SerilogLogger as IDisposable)?.Dispose();
    }
}

public static class LoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    public static LoggingSession Create(IPlatform platform, bool verbose, Action<LoggerConfiguration>? configureAdditionalSinks = null)
    {
        var memorySink = new InMemorySink();
        var minimumLevel = verbose ? LogEventLevel.Debug : LogEventLevel.Information;

        var logDirectory = platform.GetLogDirectory();
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, "utlauncher-.log");

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Is(minimumLevel)
            .Enrich.FromLogContext()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 5,
                outputTemplate: OutputTemplate)
            .WriteTo.Sink(memorySink);

        configureAdditionalSinks?.Invoke(configuration);

        var serilogLogger = configuration.CreateLogger();
        var factory = new SerilogLoggerFactory(serilogLogger, dispose: false);

        return new LoggingSession(factory, memorySink, serilogLogger);
    }
}
