using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using UTLauncher.Cli.Commands;
using UTLauncher.Core.Logging;
using UTLauncher.Core.Platform;

var verbose = args.Contains("--verbose");
var positional = args.Where(a => a != "--verbose").ToArray();

var platform = PlatformResolver.Resolve(new SystemEnvironmentInfo());

using var loggingSession = LoggingSetup.Create(platform, verbose, cfg =>
    cfg.WriteTo.Console(restrictedToMinimumLevel: verbose ? LogEventLevel.Debug : LogEventLevel.Information));

var logger = loggingSession.Factory.CreateLogger("UTLauncher.Cli");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cts.Cancel();
};

if (positional.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = positional[0];
logger.LogDebug("Comando CLI: {Command} (verbose={Verbose})", command, verbose);

try
{
    return command switch
    {
        "list" => await ListCommand.RunAsync(logger, cts.Token),
        "hash" when positional.Length >= 2 => await HashCommand.RunAsync(positional[1], logger, cts.Token),
        "hash" => Fail("Uso: utlauncher hash <file|url>"),
        "doctor" or "install" or "verify" or "launch" or "uninstall" => NotYetImplemented(command),
        _ => Fail($"Comando sconosciuto: '{command}'."),
    };
}
catch (OperationCanceledException)
{
    logger.LogWarning("Operazione annullata dall'utente");
    Console.Error.WriteLine("Annullato.");
    return 130;
}

int Fail(string message)
{
    Console.Error.WriteLine(message);
    PrintUsage();
    return 1;
}

int NotYetImplemented(string cmd)
{
    Console.Error.WriteLine($"Comando '{cmd}' non ancora implementato (arriverà in un passo successivo del piano).");
    return 1;
}

void PrintUsage()
{
    Console.WriteLine("""
        utlauncher list
        utlauncher install <ut99|ut2004|ut4> [--dest <path>] [--source-url <url>] [--source-file <path>] [--verbose]
        utlauncher verify  <gioco>
        utlauncher launch  <gioco>
        utlauncher uninstall <gioco>
        utlauncher doctor
        utlauncher hash <file|url>
        """);
}
