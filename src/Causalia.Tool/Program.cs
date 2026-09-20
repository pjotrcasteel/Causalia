using Causalia.Tool.Initialization;
using Causalia.Tool.Inspection;
using Causalia.Tool.Reporting;

namespace Causalia.Tool;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            var options = CommandLineOptions.Parse(args);

            if (options.ShowHelp)
            {
                await WriteHelpAsync(Console.Out, CancellationToken.None);
                return 0;
            }

            return await ExecuteAsync(options, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            await WriteErrorAsync("Operation cancelled.", CancellationToken.None);
            return 130;
        }
        catch (ArgumentException exception)
        {
            await WriteErrorAsync(exception.Message, CancellationToken.None);
            await WriteHelpAsync(Console.Error, CancellationToken.None);
            return 2;
        }
        catch (Exception exception)
        {
            await WriteErrorAsync($"Causalia command failed: {exception.Message}", CancellationToken.None);
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    private static async Task<int> ExecuteAsync(CommandLineOptions options, CancellationToken cancellationToken)
    {
        if (string.Equals(options.Command, "inspect", StringComparison.OrdinalIgnoreCase))
        {
            var inspector = new ProjectInspector();
            var report = await inspector.InspectAsync(options.TargetPath, cancellationToken);
            await ReportWriter.WriteAsync(report, options.OutputFormat, Console.Out, cancellationToken);
            return 0;
        }

        var initializer = new ProjectInitializer();
        var result = await initializer.InitializeAsync(
            options.TargetPath,
            options.ProjectPath,
            options.Force,
            cancellationToken);
        await InitializationResultWriter.WriteAsync(result, Console.Out, cancellationToken);
        return 0;
    }

    private static async Task WriteHelpAsync(TextWriter writer, CancellationToken cancellationToken)
    {
        const string help = """
            Causalia adoption tooling

            Usage:
              dotnet causalia inspect [path] [--format text|json]
              dotnet causalia init [path] [--project <project.csproj>] [--force]

            Targets:
              directory   Inspect or initialize from all .csproj files below the directory.
              .slnx       Inspect or initialize projects referenced by the XML solution.
              .sln        Inspect or initialize projects referenced by the solution.
              .csproj     Inspect or initialize one project.

            Init behavior:
              --project   Explicit production project to create the Causalia test project for.
              --force     Replace an existing generated Causalia test project.

            Examples:
              dotnet causalia inspect
              dotnet causalia inspect MyService.slnx --format json
              dotnet causalia init MyService.slnx
              dotnet causalia init MyService.slnx --project src/Orders/Orders.csproj
            """;

        await writer.WriteAsync(help.AsMemory(), cancellationToken);
    }

    private static async Task WriteErrorAsync(string message, CancellationToken cancellationToken)
    {
        await Console.Error.WriteLineAsync(message.AsMemory(), cancellationToken);
    }
}
