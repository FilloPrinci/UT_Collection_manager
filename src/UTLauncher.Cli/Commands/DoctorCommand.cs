using Microsoft.Extensions.Logging;
using UTLauncher.Core.Platform;
using UTLauncher.Core.Processes;

namespace UTLauncher.Cli.Commands;

public static class DoctorCommand
{
    public static async Task<int> RunAsync(ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        Console.WriteLine("System check");
        Console.WriteLine("============");
        Console.WriteLine();

        var processRunner = new ProcessRunner(loggerFactory.CreateLogger<ProcessRunner>());
        var locator = new SystemLibraryLocator(processRunner);
        var checker = new SystemChecker(locator);

        var results = await checker.RunAsync(cancellationToken).ConfigureAwait(false);

        foreach (var result in results)
        {
            var marker = result.IsOk ? "[OK]" : "[--]";
            Console.WriteLine($"{marker} {result.Name}: {result.Message}");
            if (result.Hint is not null)
            {
                Console.WriteLine($"     Install it with: {result.Hint}");
            }
        }

        return 0;
    }
}
