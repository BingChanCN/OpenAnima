using System.Reflection;
using System.Text;
using OpenAnima.Cli.Models;

namespace OpenAnima.Cli.Services;

/// <summary>
/// Template engine for generating module files from embedded templates.
/// </summary>
public class TemplateEngine
{
    private readonly Assembly _assembly;

    /// <summary>
    /// Default OpenAnima minimum version for generated modules.
    /// </summary>
    public const string DefaultOpenAnimaMinVersion = "1.4.0";

    /// <summary>
    /// Default module version.
    /// </summary>
    public const string DefaultModuleVersion = "1.0.0";

    /// <summary>
    /// Default module description placeholder.
    /// </summary>
    public const string DefaultDescription = "TODO: Add description";

    /// <summary>
    /// Default module author placeholder.
    /// </summary>
    public const string DefaultAuthor = "TODO: Add author";

    /// <summary>
    /// Creates a new template engine instance.
    /// </summary>
    public TemplateEngine()
    {
        _assembly = typeof(TemplateEngine).Assembly;
    }

    /// <summary>
    /// Renders the Module.cs file content.
    /// </summary>
    /// <param name="moduleName">The module name (used as class name and namespace).</param>
    /// <param name="version">Module version (default: 1.0.0).</param>
    /// <param name="description">Module description.</param>
    /// <param name="inputs">Input port declarations.</param>
    /// <param name="outputs">Output port declarations.</param>
    /// <returns>The rendered C# source code.</returns>
    public string RenderModuleCs(
        string moduleName,
        string? version = null,
        string? description = null,
        List<PortDeclaration>? inputs = null,
        List<PortDeclaration>? outputs = null)
    {
        var template = LoadTemplate("module-cs.tmpl");

        var portAttributes = GeneratePortAttributes(inputs, outputs);
        var hasPorts = (inputs != null && inputs.Count > 0) || (outputs != null && outputs.Count > 0);

        // Generate IModuleExecutor implementation if module has ports
        var implementsExecutor = hasPorts ? ", IModuleExecutor" : "";
        var executeMethod = hasPorts ? GenerateExecuteMethod() : "";

        return template
            .Replace("{{ModuleName}}", moduleName)
            .Replace("{{Namespace}}", moduleName)
            .Replace("{{ModuleVersion}}", version ?? DefaultModuleVersion)
            .Replace("{{ModuleDescription}}", description ?? DefaultDescription)
            .Replace("{{PortAttributes}}", portAttributes)
            .Replace("{{ImplementsExecutor}}", implementsExecutor)
            .Replace("{{ExecuteMethod}}", executeMethod);
    }

    /// <summary>
    /// Renders the Module.csproj file content.
    /// </summary>
    /// <param name="moduleName">The module name (used as project name).</param>
    /// <returns>The rendered project file content.</returns>
    public string RenderModuleCsproj(string moduleName)
    {
        var template = LoadTemplate("module-csproj.tmpl");

        return template
            .Replace("{{ModuleName}}", moduleName);
    }

    /// <summary>
    /// Renders the module.json manifest file content.
    /// </summary>
    /// <param name="manifest">The module manifest to render.</param>
    /// <returns>The rendered JSON content.</returns>
    public string RenderModuleJson(ModuleManifest manifest)
    {
        var template = LoadTemplate("module-json.tmpl");

        // Generate ports JSON arrays
        var inputsJson = GeneratePortsJson(manifest.Ports?.Inputs);
        var outputsJson = GeneratePortsJson(manifest.Ports?.Outputs);

        return template
            .Replace("{{ModuleId}}", manifest.Id ?? manifest.Name ?? "Module")
            .Replace("{{ModuleName}}", manifest.Name ?? "Module")
            .Replace("{{ModuleVersion}}", manifest.Version)
            .Replace("{{ModuleDescription}}", manifest.Description ?? DefaultDescription)
            .Replace("{{ModuleAuthor}}", manifest.Author ?? DefaultAuthor)
            .Replace("{{EntryAssembly}}", manifest.GetEntryAssembly())
            .Replace("{{OpenAnimaMinVersion}}", manifest.OpenAnima?.MinVersion ?? DefaultOpenAnimaMinVersion)
            .Replace("{{InputsJson}}", inputsJson)
            .Replace("{{OutputsJson}}", outputsJson);
    }

    /// <summary>
    /// Renders the module.json manifest file content with defaults.
    /// </summary>
    /// <param name="moduleName">The module name.</param>
    /// <param name="version">Module version.</param>
    /// <param name="description">Module description.</param>
    /// <param name="author">Module author.</param>
    /// <param name="inputs">Input port declarations.</param>
    /// <param name="outputs">Output port declarations.</param>
    /// <returns>The rendered JSON content.</returns>
    public string RenderModuleJson(
        string moduleName,
        string? version = null,
        string? description = null,
        string? author = null,
        List<PortDeclaration>? inputs = null,
        List<PortDeclaration>? outputs = null)
    {
        var manifest = new ModuleManifest
        {
            Id = moduleName,
            Name = moduleName,
            Version = version ?? DefaultModuleVersion,
            Description = description ?? string.Empty,
            Author = author ?? string.Empty,
            OpenAnima = new OpenAnimaCompatibility
            {
                MinVersion = DefaultOpenAnimaMinVersion
            },
            Ports = new PortDeclarations
            {
                Inputs = inputs ?? new List<PortDeclaration>(),
                Outputs = outputs ?? new List<PortDeclaration>()
            }
        };

        return RenderModuleJson(manifest);
    }

    /// <summary>
    /// Loads an embedded template by name.
    /// </summary>
    /// <param name="templateName">The template file name.</param>
    /// <returns>The template content.</returns>
    /// <exception cref="InvalidOperationException">Thrown if template is not found.</exception>
    private string LoadTemplate(string templateName)
    {
        // Try different resource name patterns
        var resourceNames = new[]
        {
            $"OpenAnima.Cli.Templates.{templateName}",
            $"Templates.{templateName}",
            templateName
        };

        foreach (var resourceName in resourceNames)
        {
            using var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }

        // List available resources for debugging
        var availableResources = _assembly.GetManifestResourceNames();
        var availableList = string.Join(", ", availableResources);

        throw new InvalidOperationException(
            $"Template '{templateName}' not found as embedded resource. " +
            $"Tried: {string.Join(", ", resourceNames)}. " +
            $"Available resources: {availableList}");
    }

    /// <summary>
    /// Generates port attribute declarations for the module class.
    /// </summary>
    /// <param name="inputs">Input port declarations.</param>
    /// <param name="outputs">Output port declarations.</param>
    /// <returns>String containing port attribute declarations.</returns>
    private static string GeneratePortAttributes(
        List<PortDeclaration>? inputs,
        List<PortDeclaration>? outputs)
    {
        var sb = new StringBuilder();

        if (inputs != null)
        {
            foreach (var port in inputs)
            {
                sb.AppendLine($"[InputPort(\"{port.Name}\", PortType.{port.GetNormalizedType()})]");
            }
        }

        if (outputs != null)
        {
            foreach (var port in outputs)
            {
                sb.AppendLine($"[OutputPort(\"{port.Name}\", PortType.{port.GetNormalizedType()})]");
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Generates the ExecuteAsync method for modules with ports.
    /// </summary>
    private static string GenerateExecuteMethod()
    {
        return @"

    private ModuleExecutionState _state = ModuleExecutionState.Idle;
    private Exception? _lastError;

    public ModuleExecutionState GetState() => _state;
    public Exception? GetLastError() => _lastError;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // TODO: Implement your module's core logic here
        // Read from input ports (via EventBus subscriptions set up in InitializeAsync)
        // Process the data
        // Write to output ports (via EventBus publish)
        _state = ModuleExecutionState.Processing;
        try
        {
            // Example: Read input, process, write output
            // await ProcessAndPublishAsync(ct);
            _state = ModuleExecutionState.Completed;
        }
        catch (Exception ex)
        {
            _lastError = ex;
            _state = ModuleExecutionState.Error;
            throw;
        }
    }";
    }

    /// <summary>
    /// Generates JSON array for port declarations.
    /// </summary>
    private static string GeneratePortsJson(List<PortDeclaration>? ports)
    {
        if (ports == null || ports.Count == 0)
        {
            return "[]";
        }

        var sb = new StringBuilder();
        sb.AppendLine("[");
        for (int i = 0; i < ports.Count; i++)
        {
            var port = ports[i];
            sb.AppendLine($"      {{ \"name\": \"{port.Name}\", \"type\": \"{port.GetNormalizedType()}\" }}{(i < ports.Count - 1 ? "," : "")}");
        }
        sb.Append("    ]");
        return sb.ToString();
    }
}