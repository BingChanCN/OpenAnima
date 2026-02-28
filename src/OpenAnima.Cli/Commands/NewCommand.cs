using System.CommandLine;
using System.CommandLine.Invocation;
using OpenAnima.Cli.Models;
using OpenAnima.Cli.Services;

namespace OpenAnima.Cli.Commands;

/// <summary>
/// The 'new' command for creating new module projects.
/// </summary>
public class NewCommand : Command
{
    private readonly TemplateEngine _templateEngine;

    /// <summary>
    /// Creates a new instance of the NewCommand.
    /// </summary>
    /// <param name="templateEngine">The template engine for generating files.</param>
    public NewCommand(TemplateEngine templateEngine) : base("new", "Create a new module project")
    {
        _templateEngine = templateEngine ?? throw new ArgumentNullException(nameof(templateEngine));

        // Positional argument: module name (required)
        var moduleNameArgument = new Argument<string>(
            name: "name",
            description: "The name of the module to create");

        AddArgument(moduleNameArgument);

        // Options
        var outputOption = new Option<DirectoryInfo?>(
            aliases: new[] { "--output", "-o" },
            getDefaultValue: () => null,
            description: "Output directory for the generated project (default: current directory)");

        var dryRunOption = new Option<bool>(
            aliases: new[] { "--dry-run" },
            getDefaultValue: () => false,
            description: "Preview generated files without creating them");

        var typeOption = new Option<string>(
            aliases: new[] { "--type", "-t" },
            getDefaultValue: () => "standard",
            description: "Module type (default: standard)");

        typeOption.AddValidator(result =>
        {
            var value = result.GetValueForOption(typeOption);
            if (!string.Equals(value, "standard", StringComparison.OrdinalIgnoreCase))
            {
                result.ErrorMessage = $"Invalid module type '{value}'. Currently supported: standard";
            }
        });

        var inputsOption = new Option<string[]>(
            aliases: new[] { "--inputs" },
            getDefaultValue: () => Array.Empty<string>(),
            description: "Input port specifications (format: Name or Name:Type, default type: Text)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        var outputsOption = new Option<string[]>(
            aliases: new[] { "--outputs" },
            getDefaultValue: () => Array.Empty<string>(),
            description: "Output port specifications (format: Name or Name:Type, default type: Text)")
        {
            AllowMultipleArgumentsPerToken = true
        };

        AddOption(outputOption);
        AddOption(dryRunOption);
        AddOption(typeOption);
        AddOption(inputsOption);
        AddOption(outputsOption);

        // Use SetHandler with InvocationContext for proper exit code propagation
        this.SetHandler(context =>
        {
            var moduleName = context.ParseResult.GetValueForArgument(moduleNameArgument);
            var output = context.ParseResult.GetValueForOption(outputOption);
            var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
            var type = context.ParseResult.GetValueForOption(typeOption) ?? "standard";
            var inputs = context.ParseResult.GetValueForOption(inputsOption) ?? Array.Empty<string>();
            var outputs = context.ParseResult.GetValueForOption(outputsOption) ?? Array.Empty<string>();

            context.ExitCode = HandleCommand(moduleName, output, dryRun, type, inputs, outputs);
        });
    }

    /// <summary>
    /// Handles the command execution.
    /// </summary>
    private int HandleCommand(
        string moduleName,
        DirectoryInfo? outputPath,
        bool dryRun,
        string moduleType,
        string[] inputs,
        string[] outputs)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            Console.Error.WriteLine("error: Module name is required.");
            return ExitCodes.ValidationError;
        }

        // Validate module name
        var nameErrors = ModuleNameValidator.Validate(moduleName);
        if (nameErrors.Count > 0)
        {
            foreach (var error in nameErrors)
            {
                Console.Error.WriteLine($"error: {error}");
            }

            var suggestion = ModuleNameValidator.GetSuggestion(moduleName);
            if (suggestion != null)
            {
                Console.Error.WriteLine($"hint: Did you mean '{suggestion}'?");
            }

            return ExitCodes.ValidationError;
        }

        // Parse port specifications
        var inputPorts = ParsePortSpecifications(inputs, "input");
        var outputPorts = ParsePortSpecifications(outputs, "output");

        // Check for port parsing errors
        var portErrors = new List<string>();
        portErrors.AddRange(inputPorts.Errors);
        portErrors.AddRange(outputPorts.Errors);

        if (portErrors.Count > 0)
        {
            foreach (var error in portErrors)
            {
                Console.Error.WriteLine($"error: {error}");
            }
            return ExitCodes.ValidationError;
        }

        // Determine output directory
        var targetDir = outputPath?.FullName ?? Directory.GetCurrentDirectory();
        var moduleDir = Path.Combine(targetDir, moduleName);

        // Generate content using template engine
        var moduleCsContent = _templateEngine.RenderModuleCs(
            moduleName,
            inputs: inputPorts.Ports,
            outputs: outputPorts.Ports);

        var csprojContent = _templateEngine.RenderModuleCsproj(moduleName);

        var jsonContent = _templateEngine.RenderModuleJson(
            moduleName,
            description: $"The {moduleName} module",
            inputs: inputPorts.Ports,
            outputs: outputPorts.Ports);

        // Dry run mode - output to stdout
        if (dryRun)
        {
            Console.WriteLine($"--- {moduleName}/{moduleName}.cs ---");
            Console.WriteLine(moduleCsContent);
            Console.WriteLine();
            Console.WriteLine($"--- {moduleName}/{moduleName}.csproj ---");
            Console.WriteLine(csprojContent);
            Console.WriteLine();
            Console.WriteLine($"--- {moduleName}/module.json ---");
            Console.WriteLine(jsonContent);
            Console.WriteLine();
            Console.WriteLine($"Created (dry-run) {moduleName}/");
            return ExitCodes.Success;
        }

        // Check if directory already exists
        if (Directory.Exists(moduleDir))
        {
            Console.Error.WriteLine($"error: Directory '{moduleDir}' already exists.");
            Console.Error.WriteLine("hint: Choose a different module name or output directory.");
            return ExitCodes.GeneralError;
        }

        try
        {
            // Create directory and write files
            Directory.CreateDirectory(moduleDir);
            File.WriteAllText(Path.Combine(moduleDir, $"{moduleName}.cs"), moduleCsContent);
            File.WriteAllText(Path.Combine(moduleDir, $"{moduleName}.csproj"), csprojContent);
            File.WriteAllText(Path.Combine(moduleDir, "module.json"), jsonContent);

            Console.WriteLine($"Created {moduleName}/");
            return ExitCodes.Success;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"error: Permission denied: {ex.Message}");
            return ExitCodes.GeneralError;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"error: Failed to create module: {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }

    /// <summary>
    /// Parses port specification strings into PortDeclaration objects.
    /// </summary>
    private static (List<PortDeclaration> Ports, List<string> Errors) ParsePortSpecifications(
        string[] specs, string direction)
    {
        var ports = new List<PortDeclaration>();
        var errors = new List<string>();

        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec))
            {
                continue;
            }

            // Support comma-separated ports in a single argument
            // e.g., --inputs Text,Trigger is equivalent to --inputs Text Trigger
            var parts = spec.Split(',');

            foreach (var part in parts)
            {
                var trimmedPart = part.Trim();
                if (string.IsNullOrWhiteSpace(trimmedPart))
                {
                    continue;
                }

                string name;
                string type;

                var colonIndex = trimmedPart.IndexOf(':');
                if (colonIndex >= 0)
                {
                    name = trimmedPart.Substring(0, colonIndex).Trim();
                    type = trimmedPart.Substring(colonIndex + 1).Trim();
                }
                else
                {
                    name = trimmedPart;
                    type = "Text"; // Default type
                }

                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add($"Invalid {direction} port specification: '{trimmedPart}' - name is empty.");
                    continue;
                }

                if (!IsValidPortType(type))
                {
                    errors.Add($"Invalid {direction} port type '{type}' for port '{name}'. Valid types: Text, Trigger.");
                    continue;
                }

                ports.Add(new PortDeclaration
                {
                    Name = name,
                    Type = type
                });
            }
        }

        return (ports, errors);
    }

    /// <summary>
    /// Checks if a port type is valid.
    /// </summary>
    private static bool IsValidPortType(string type)
    {
        return type.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Trigger", StringComparison.OrdinalIgnoreCase);
    }
}