namespace Causalia.Tool;

internal sealed class CommandLineOptions
{
    private CommandLineOptions(
        string command,
        string targetPath,
        OutputFormat outputFormat,
        string? projectPath,
        bool force,
        bool showHelp)
    {
        Command = command;
        TargetPath = targetPath;
        OutputFormat = outputFormat;
        ProjectPath = projectPath;
        Force = force;
        ShowHelp = showHelp;
    }

    public string Command { get; }

    public string TargetPath { get; }

    public OutputFormat OutputFormat { get; }

    public string? ProjectPath { get; }

    public bool Force { get; }

    public bool ShowHelp { get; }

    public static CommandLineOptions Parse(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count == 0 || IsHelp(arguments[0]))
        {
            return CreateHelpOptions();
        }

        var command = arguments[0];

        if (!IsSupportedCommand(command))
        {
            throw new ArgumentException($"Unknown command '{command}'.");
        }

        return ParseCommand(command, arguments);
    }

    private static CommandLineOptions ParseCommand(string command, IReadOnlyList<string> arguments)
    {
        var targetPath = ".";
        var outputFormat = OutputFormat.Text;
        string? projectPath = null;
        var force = false;
        var targetWasSpecified = false;

        for (var index = 1; index < arguments.Count; index++)
        {
            var argument = arguments[index];

            if (IsHelp(argument))
            {
                return new CommandLineOptions(command, targetPath, outputFormat, projectPath, force, true);
            }

            if (string.Equals(argument, "--format", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCommand(command, "inspect", "--format");
                index = MoveToOptionValue(arguments, index, "--format");
                outputFormat = ParseOutputFormat(arguments[index]);
                continue;
            }

            if (string.Equals(argument, "--project", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCommand(command, "init", "--project");
                index = MoveToOptionValue(arguments, index, "--project");
                projectPath = arguments[index];
                continue;
            }

            if (string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase))
            {
                EnsureCommand(command, "init", "--force");
                force = true;
                continue;
            }

            if (argument.StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown option '{argument}'.");
            }

            if (targetWasSpecified)
            {
                throw new ArgumentException("Only one target can be specified.");
            }

            targetPath = argument;
            targetWasSpecified = true;
        }

        return new CommandLineOptions(command, targetPath, outputFormat, projectPath, force, false);
    }

    private static int MoveToOptionValue(IReadOnlyList<string> arguments, int index, string option)
    {
        var valueIndex = index + 1;

        if (valueIndex >= arguments.Count)
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        return valueIndex;
    }

    private static void EnsureCommand(string actualCommand, string expectedCommand, string option)
    {
        if (!string.Equals(actualCommand, expectedCommand, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{option} is only valid with '{expectedCommand}'.");
        }
    }

    private static bool IsSupportedCommand(string command)
    {
        return string.Equals(command, "inspect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "init", StringComparison.OrdinalIgnoreCase);
    }

    private static CommandLineOptions CreateHelpOptions()
    {
        return new CommandLineOptions(string.Empty, ".", OutputFormat.Text, null, false, true);
    }

    private static OutputFormat ParseOutputFormat(string value)
    {
        if (string.Equals(value, "text", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Text;
        }

        if (string.Equals(value, "json", StringComparison.OrdinalIgnoreCase))
        {
            return OutputFormat.Json;
        }

        throw new ArgumentException($"Unsupported output format '{value}'. Use 'text' or 'json'.");
    }

    private static bool IsHelp(string value)
    {
        return string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "help", StringComparison.OrdinalIgnoreCase);
    }
}
