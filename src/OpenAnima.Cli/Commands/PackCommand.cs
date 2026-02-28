using System.CommandLine;
using System.CommandLine.Invocation;
using OpenAnima.Cli.Services;

namespace OpenAnima.Cli.Commands;

/// <summary>
/// Command for packing modules into .oamod files.
/// </summary>
public class PackCommand : Command
{
    /// <summary>
    /// Initializes a new instance of the PackCommand class.
    /// </summary>
    /// <param name="packService">The pack service to use.</param>
    public PackCommand(PackService packService) : base("pack", "Pack module into a .oamod file")
    {
        // Path argument
        var pathArgument = new Argument<string>(
            name: "path",
            description: "Path to the module project directory");

        AddArgument(pathArgument);

        // Output option
        var outputOption = new Option<DirectoryInfo?>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => null,
            description: "Output directory for the .oamod file");

        AddOption(outputOption);

        // No-build option
        var noBuildOption = new Option<bool>(
            aliases: new[] { "--no-build" },
            getDefaultValue: () => false,
            description: "Skip building the project before packing");

        AddOption(noBuildOption);

        // Set handler
        this.SetHandler((InvocationContext context) =>
        {
            var path = context.ParseResult.GetValueForArgument(pathArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var noBuild = context.ParseResult.GetValueForOption(noBuildOption);

            context.ExitCode = packService.Pack(path, output?.FullName, noBuild);
        });
    }
}
