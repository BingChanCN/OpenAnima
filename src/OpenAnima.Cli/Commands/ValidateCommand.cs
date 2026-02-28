using System.CommandLine;
using System.CommandLine.Invocation;
using System.Reflection;
using System.Runtime.Loader;
using OpenAnima.Cli.Services;

namespace OpenAnima.Cli.Commands;

/// <summary>
/// The 'validate' command for validating module projects.
/// </summary>
public class ValidateCommand : Command
{
    /// <summary>
    /// Creates a new instance of the ValidateCommand.
    /// </summary>
    public ValidateCommand() : base("validate", "Validate a module project")
    {
        // Positional argument: module path (required)
        var pathArgument = new Argument<string>(
            name: "path",
            description: "Path to the module project directory");

        AddArgument(pathArgument);

        // Use SetHandler with InvocationContext for proper exit code propagation
        this.SetHandler(context =>
        {
            var modulePath = context.ParseResult.GetValueForArgument(pathArgument);
            context.ExitCode = HandleCommand(modulePath);
        });
    }

    /// <summary>
    /// Handles the command execution.
    /// </summary>
    private int HandleCommand(string modulePath)
    {
        if (string.IsNullOrWhiteSpace(modulePath))
        {
            Console.Error.WriteLine("error: Module path is required.");
            return ExitCodes.ValidationError;
        }

        // Accumulate ALL errors (VAL-05: report all, not just first)
        var errors = new List<string>();

        // VAL-02: Check module.json exists
        var manifestPath = Path.Combine(modulePath, "module.json");
        if (!File.Exists(manifestPath))
        {
            errors.Add($"module.json not found in '{modulePath}'.");

            // Output errors and return early if manifest doesn't exist
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"error: {error}");
            }
            return ExitCodes.ValidationError;
        }

        // Read and validate JSON
        string json;
        try
        {
            json = File.ReadAllText(manifestPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: Failed to read module.json: {ex.Message}");
            return ExitCodes.ValidationError;
        }

        // VAL-02: Validate JSON and VAL-03: Validate required fields
        var (manifest, manifestErrors) = ManifestValidator.ValidateJson(json);
        errors.AddRange(manifestErrors);

        // VAL-04: Validate IModule implementation if manifest parsed successfully
        if (manifest != null)
        {
            ValidateIModuleImplementation(modulePath, manifest, errors);
        }

        // VAL-05: Output all errors
        if (errors.Count > 0)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"error: {error}");
            }
            return ExitCodes.ValidationError;
        }

        // Success
        Console.WriteLine("Module is valid.");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Validates that the module assembly contains an IModule implementation.
    /// </summary>
    private void ValidateIModuleImplementation(string modulePath, Models.ModuleManifest manifest, List<string> errors)
    {
        // Look for compiled DLL in bin/ subdirectory
        var binDir = Path.Combine(modulePath, "bin");
        if (!Directory.Exists(binDir))
        {
            // Not an error - module may not be built yet
            return;
        }

        var entryAssemblyName = manifest.GetEntryAssembly();
        var dllFiles = Directory.GetFiles(binDir, "*.dll", SearchOption.AllDirectories);

        // Find DLL matching the entry assembly name
        var targetDll = dllFiles.FirstOrDefault(f =>
            Path.GetFileName(f).Equals(entryAssemblyName, StringComparison.OrdinalIgnoreCase));

        if (targetDll == null)
        {
            // Not an error - module may not be built yet
            return;
        }

        // Load assembly in isolated context and check for IModule implementation
        AssemblyLoadContext? loadContext = null;
        try
        {
            loadContext = new AssemblyLoadContext("validation", isCollectible: true);
            var assembly = loadContext.LoadFromAssemblyPath(targetDll);

            // Scan types for IModule implementation using name-based comparison
            // (avoids type identity issues across load contexts)
            var hasIModule = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Any(t => t.GetInterfaces()
                    .Any(i => i.FullName == "OpenAnima.Contracts.IModule"));

            if (!hasIModule)
            {
                errors.Add($"No class implementing IModule found in assembly '{entryAssemblyName}'.");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to load or inspect assembly '{entryAssemblyName}': {ex.Message}");
        }
        finally
        {
            loadContext?.Unload();
        }
    }
}
