using System.CommandLine;
using System.CommandLine.Parsing;
using System.Reflection;
using OpenAnima.Cli.Commands;
using OpenAnima.Cli.Services;

namespace OpenAnima.Cli;

/// <summary>
/// Entry point for the OpenAnima CLI tool.
/// </summary>
public class Program
{
    /// <summary>
    /// Main entry point for the CLI.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public static int Main(string[] args)
    {
        // Create root command with description
        var rootCommand = new RootCommand("OpenAnima module development CLI");

        // Global --verbosity/-v option
        var verbosityOption = new Option<string>(
            aliases: new[] { "--verbosity", "-v" },
            getDefaultValue: () => "quiet",
            description: "Set the verbosity level (quiet, normal, detailed)");

        verbosityOption.AddValidator(result =>
        {
            var value = result.GetValueForOption(verbosityOption);
            if (value != "quiet" && value != "normal" && value != "detailed")
            {
                result.ErrorMessage = $"Invalid verbosity '{value}'. Valid values: quiet, normal, detailed";
            }
        });

        rootCommand.AddGlobalOption(verbosityOption);

        // Global --version option
        var versionOption = new Option<bool>(
            aliases: new[] { "--version" },
            description: "Show version information");

        rootCommand.AddGlobalOption(versionOption);

        // Global --help/-h option
        var helpOption = new Option<bool>(
            aliases: new[] { "--help", "-h" },
            description: "Show help and usage information");

        rootCommand.AddGlobalOption(helpOption);

        // Register the 'new' command
        var templateEngine = new TemplateEngine();
        var newCommand = new NewCommand(templateEngine);
        rootCommand.AddCommand(newCommand);

        // Register the 'validate' command
        rootCommand.AddCommand(new ValidateCommand());

        // Parse arguments
        var parseResult = rootCommand.Parse(args);

        // Check for --help first (before checking errors)
        if (parseResult.GetValueForOption(helpOption))
        {
            PrintHelp();
            return ExitCodes.Success;
        }

        // Check for --version (before checking errors)
        if (parseResult.GetValueForOption(versionOption))
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            Console.WriteLine($"oani version {version}");
            return ExitCodes.Success;
        }

        // Handle parse errors - output to stderr
        // But skip "Required command was not provided" error - we'll show help instead
        var nonCommandErrors = parseResult.Errors
            .Where(e => !e.Message.Equals("Required command was not provided.", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (nonCommandErrors.Count > 0)
        {
            foreach (var error in nonCommandErrors)
            {
                Console.Error.WriteLine($"error: {error.Message}");
            }
            return ExitCodes.GeneralError;
        }

        // No command provided (or only verbosity option) - show help
        if (parseResult.CommandResult.Command == rootCommand)
        {
            PrintHelp();
            return ExitCodes.Success;
        }

        // Invoke the command and return the exit code
        var exitCode = parseResult.Invoke();
        return exitCode;
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"OpenAnima module development CLI

Usage: oani [options] [command]

Options:
  -v, --verbosity <level>  Set the verbosity level (quiet, normal, detailed) [default: quiet]
  --version                Show version information
  -h, --help               Show help and usage information

Commands:
  new <name>       Create a new module project
  validate <path>  Validate a module project");
    }
}