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
logger.LogDebug("CLI command: {Command} (verbose={Verbose})", command, verbose);

try
{
    return command switch
    {
        "list" => await ListCommand.RunAsync(logger, cts.Token),
        "hash" when positional.Length >= 2 => await HashCommand.RunAsync(positional[1], logger, cts.Token),
        "hash" => Fail("Usage: utlauncher hash <file|url>"),
        "install" => await InstallCommand.RunAsync(positional[1..], platform, loggingSession.Factory, cts.Token),
        "verify" when positional.Length >= 2 => await VerifyCommand.RunAsync(positional[1], platform, loggingSession.Factory, cts.Token),
        "verify" => Fail("Usage: utlauncher verify <game>"),
        "doctor" => await DoctorCommand.RunAsync(loggingSession.Factory, cts.Token),
        "launch" when positional.Length >= 2 => await LaunchCommand.RunAsync(positional[1], platform, loggingSession.Factory, cts.Token),
        "launch" => Fail("Usage: utlauncher launch <game>"),
        "uninstall" => NotYetImplemented(command),
        _ => Fail($"Unknown command: '{command}'."),
    };
}
catch (OperationCanceledException)
{
    logger.LogWarning("Operation cancelled by the user");
    Console.Error.WriteLine("Cancelled.");
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
    Console.Error.WriteLine($"Command '{cmd}' not implemented yet (coming in a later step of the plan).");
    return 1;
}

void PrintUsage()
{
    Console.WriteLine("""
        utlauncher list
        utlauncher install <ut99|ut2004|ut4> [--dest <path>] [--source-url <url>] [--source-file <path>] [--verbose]
        utlauncher verify  <game>
        utlauncher launch  <game>
        utlauncher uninstall <game>
        utlauncher doctor
        utlauncher hash <file|url>
        """);
}
