using Xunit;
using OpenAnima.Cli.Services;
using OpenAnima.Cli.Models;
using System.IO.Compression;
using System.Text.Json;

namespace OpenAnima.Cli.Tests;

/// <summary>
/// Unit tests for CLI exit codes and help functionality.
/// </summary>
public class CliFoundationTests
{
    [Fact]
    public void ExitCodes_Success_IsZero()
    {
        Assert.Equal(0, ExitCodes.Success);
    }

    [Fact]
    public void ExitCodes_GeneralError_IsOne()
    {
        Assert.Equal(1, ExitCodes.GeneralError);
    }

    [Fact]
    public void ExitCodes_ValidationError_IsTwo()
    {
        Assert.Equal(2, ExitCodes.ValidationError);
    }

    [Fact]
    public void Program_HelpOption_ReturnsSuccess()
    {
        // Arrange
        var args = new[] { "--help" };

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public void Program_VersionOption_ReturnsSuccess()
    {
        // Arrange
        var args = new[] { "--version" };

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public void Program_ShortHelpOption_ReturnsSuccess()
    {
        // Arrange
        var args = new[] { "-h" };

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public void Program_NoArgs_ReturnsSuccess()
    {
        // Arrange
        var args = Array.Empty<string>();

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert - No args shows help, returns success
        Assert.Equal(ExitCodes.Success, exitCode);
    }

    [Fact]
    public void Program_InvalidVerbosity_ReturnsError()
    {
        // Arrange
        var args = new[] { "--verbosity", "invalid" };

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert
        Assert.Equal(ExitCodes.GeneralError, exitCode);
    }

    [Theory]
    [InlineData("quiet")]
    [InlineData("normal")]
    [InlineData("detailed")]
    public void Program_ValidVerbosity_ReturnsSuccess(string verbosity)
    {
        // Arrange
        var args = new[] { "--verbosity", verbosity };

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
    }

    /// <summary>
    /// Helper method to run CLI with captured output.
    /// </summary>
    private static int RunCliWithArgs(string[] args)
    {
        // Capture stdout/stderr to avoid polluting test output
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();

            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            return Program.Main(args);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}

/// <summary>
/// Unit tests for TemplateEngine service.
/// </summary>
public class TemplateEngineTests
{
    private readonly TemplateEngine _engine = new();

    [Fact]
    public void RenderModuleCs_ContainsIModule()
    {
        var result = _engine.RenderModuleCs("TestModule");
        Assert.Contains("IModule", result);
    }

    [Fact]
    public void RenderModuleCs_ContainsIModuleMetadata()
    {
        var result = _engine.RenderModuleCs("TestModule");
        Assert.Contains("IModuleMetadata", result);
    }

    [Fact]
    public void RenderModuleCs_ContainsModuleName()
    {
        var result = _engine.RenderModuleCs("TestModule");
        Assert.Contains("TestModule", result);
    }

    [Fact]
    public void RenderModuleCs_ContainsInitializeAsync()
    {
        var result = _engine.RenderModuleCs("TestModule");
        Assert.Contains("InitializeAsync", result);
    }

    [Fact]
    public void RenderModuleCs_ContainsShutdownAsync()
    {
        var result = _engine.RenderModuleCs("TestModule");
        Assert.Contains("ShutdownAsync", result);
    }

    [Fact]
    public void RenderModuleCsproj_ContainsProjectReference()
    {
        var result = _engine.RenderModuleCsproj("TestModule");
        Assert.Contains("ProjectReference", result);
    }

    [Fact]
    public void RenderModuleCsproj_ContainsOpenAnimaContracts()
    {
        var result = _engine.RenderModuleCsproj("TestModule");
        Assert.Contains("OpenAnima.Contracts", result);
    }

    [Fact]
    public void RenderModuleCsproj_ContainsTargetFramework()
    {
        var result = _engine.RenderModuleCsproj("TestModule");
        Assert.Contains("net8.0", result);
    }

    [Fact]
    public void RenderModuleJson_ContainsSchemaVersion()
    {
        var result = _engine.RenderModuleJson("TestModule");
        Assert.Contains("schemaVersion", result);
    }

    [Fact]
    public void RenderModuleJson_ContainsModuleName()
    {
        var result = _engine.RenderModuleJson("TestModule");
        Assert.Contains("TestModule", result);
    }

    [Fact]
    public void RenderModuleJson_ContainsDefaultVersion()
    {
        var result = _engine.RenderModuleJson("TestModule");
        Assert.Contains("1.0.0", result);
    }

    [Fact]
    public void RenderModuleCs_WithPorts_GeneratesAttributes()
    {
        var inputs = new List<PortDeclaration>
        {
            new() { Name = "TextInput", Type = "Text" }
        };
        var outputs = new List<PortDeclaration>
        {
            new() { Name = "TriggerOutput", Type = "Trigger" }
        };

        var result = _engine.RenderModuleCs("TestModule", inputs: inputs, outputs: outputs);

        Assert.Contains("[InputPort(\"TextInput\", PortType.Text)]", result);
        Assert.Contains("[OutputPort(\"TriggerOutput\", PortType.Trigger)]", result);
    }
}

/// <summary>
/// Unit tests for ManifestValidator service.
/// </summary>
public class ManifestValidatorTests
{
    [Fact]
    public void Validate_MissingId_ReturnsError()
    {
        var manifest = new ModuleManifest
        {
            Id = null,
            Name = "TestModule"
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Contains(errors, e => e.Contains("'id'"));
    }

    [Fact]
    public void Validate_MissingName_ReturnsError()
    {
        var manifest = new ModuleManifest
        {
            Id = "test-module",
            Name = null
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Contains(errors, e => e.Contains("'name'"));
    }

    [Fact]
    public void Validate_ValidManifest_ReturnsNoErrors()
    {
        var manifest = new ModuleManifest
        {
            Id = "TestModule",
            Name = "TestModule",
            Version = "1.0.0"
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_InvalidVersion_ReturnsError()
    {
        var manifest = new ModuleManifest
        {
            Id = "TestModule",
            Name = "TestModule",
            Version = "invalid"
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Contains(errors, e => e.Contains("semantic version"));
    }

    [Fact]
    public void Validate_InvalidPortType_ReturnsError()
    {
        var manifest = new ModuleManifest
        {
            Id = "TestModule",
            Name = "TestModule",
            Version = "1.0.0",
            Ports = new PortDeclarations
            {
                Inputs = new List<PortDeclaration>
                {
                    new() { Name = "Input", Type = "InvalidType" }
                }
            }
        };

        var errors = ManifestValidator.Validate(manifest);

        Assert.Contains(errors, e => e.Contains("Invalid port type"));
    }

    [Fact]
    public void ValidateJson_EmptyJson_ReturnsError()
    {
        var (manifest, errors) = ManifestValidator.ValidateJson("");

        Assert.Null(manifest);
        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ValidateJson_ValidJson_ReturnsManifest()
    {
        var json = @"{
            ""id"": ""TestModule"",
            ""name"": ""TestModule"",
            ""version"": ""1.0.0""
        }";

        var (manifest, errors) = ManifestValidator.ValidateJson(json);

        Assert.NotNull(manifest);
        Assert.Empty(errors);
        Assert.Equal("TestModule", manifest.Id);
    }
}

/// <summary>
/// Unit tests for ValidateCommand.
/// </summary>
public class ValidateCommandTests
{
    [Fact]
    public void ValidateCommand_NoPathArgument_ReturnsError()
    {
        // Arrange
        var args = new[] { "validate" };

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert - Missing required argument should return non-zero
        Assert.NotEqual(ExitCodes.Success, exitCode);
    }

    [Fact]
    public void ValidateCommand_ValidModule_ReturnsSuccess()
    {
        // Arrange - Create a valid module directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0""
            }";

            File.WriteAllText(Path.Combine(tempDir, "module.json"), manifestJson);

            var args = new[] { "validate", tempDir };

            // Act
            var exitCode = RunCliWithArgs(args);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ValidateCommand_MissingModuleJson_ReturnsValidationError()
    {
        // Arrange - Create directory without module.json
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var args = new[] { "validate", tempDir };

            // Act
            var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

            // Assert
            Assert.Equal(ExitCodes.ValidationError, exitCode);
            Assert.Contains("module.json not found", stderr);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ValidateCommand_InvalidJson_ReturnsValidationError()
    {
        // Arrange - Create module with invalid JSON
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "module.json"), "{ invalid json }");

            var args = new[] { "validate", tempDir };

            // Act
            var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

            // Assert
            Assert.Equal(ExitCodes.ValidationError, exitCode);
            Assert.Contains("JSON", stderr);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ValidateCommand_MultipleErrors_ReportsAll()
    {
        // Arrange - Create module with multiple missing fields
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);

            var manifestJson = @"{
                ""id"": """",
                ""name"": """",
                ""version"": ""1.0.0""
            }";

            File.WriteAllText(Path.Combine(tempDir, "module.json"), manifestJson);

            var args = new[] { "validate", tempDir };

            // Act
            var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

            // Assert
            Assert.Equal(ExitCodes.ValidationError, exitCode);
            Assert.Contains("'id'", stderr);
            Assert.Contains("'name'", stderr);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ValidateCommand_NonExistentPath_ReturnsValidationError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");
        var args = new[] { "validate", nonExistentPath };

        // Act
        var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

        // Assert
        Assert.Equal(ExitCodes.ValidationError, exitCode);
        Assert.Contains("module.json not found", stderr);
    }

    /// <summary>
    /// Helper method to run CLI with captured output and error.
    /// </summary>
    private static (int ExitCode, string StdErr) RunCliWithArgsAndCaptureError(string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        var outWriter = new StringWriter();
        var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            var exitCode = Program.Main(args);
            var stderr = errorWriter.ToString();

            return (exitCode, stderr);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            outWriter.Dispose();
            errorWriter.Dispose();
        }
    }

    /// <summary>
    /// Helper method to run CLI with captured output.
    /// </summary>
    private static int RunCliWithArgs(string[] args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();

            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            return Program.Main(args);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}

/// <summary>
/// Unit tests for PackService.
/// </summary>
public class PackServiceTests
{
    [Fact]
    public void Pack_ValidModuleWithNoBuild_ProducesOamodFile()
    {
        // Arrange - Create a temp module directory with module.json and a fake DLL
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");
        var binDir = Path.Combine(tempDir, "bin", "Release", "net8.0");
        var outputDir = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(outputDir);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0""
            }";

            File.WriteAllText(Path.Combine(tempDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(binDir, "TestModule.dll"), "fake dll content");

            var packService = new PackService();

            // Act
            var exitCode = packService.Pack(tempDir, outputDir, noBuild: true);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
            var oamodPath = Path.Combine(outputDir, "TestModule.oamod");
            Assert.True(File.Exists(oamodPath), "Expected .oamod file to exist");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Pack_CreatedOamodIsValidZip_ContainsModuleJsonAndDll()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");
        var binDir = Path.Combine(tempDir, "bin", "Release", "net8.0");
        var outputDir = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(outputDir);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0""
            }";

            File.WriteAllText(Path.Combine(tempDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(binDir, "TestModule.dll"), "fake dll content");

            var packService = new PackService();
            packService.Pack(tempDir, outputDir, noBuild: true);

            // Act - Open the .oamod as a ZIP
            var oamodPath = Path.Combine(outputDir, "TestModule.oamod");
            using var archive = ZipFile.OpenRead(oamodPath);

            // Assert
            Assert.NotNull(archive.GetEntry("module.json"));
            Assert.NotNull(archive.GetEntry("TestModule.dll"));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Pack_EmbeddedManifestContainsChecksum()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");
        var binDir = Path.Combine(tempDir, "bin", "Release", "net8.0");
        var outputDir = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(outputDir);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0""
            }";

            File.WriteAllText(Path.Combine(tempDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(binDir, "TestModule.dll"), "fake dll content");

            var packService = new PackService();
            packService.Pack(tempDir, outputDir, noBuild: true);

            // Act - Read module.json from the .oamod
            var oamodPath = Path.Combine(outputDir, "TestModule.oamod");
            using var archive = ZipFile.OpenRead(oamodPath);
            var manifestEntry = archive.GetEntry("module.json");
            using var stream = manifestEntry!.Open();
            using var reader = new StreamReader(stream);
            var embeddedJson = reader.ReadToEnd();
            var manifest = JsonSerializer.Deserialize<ModuleManifest>(embeddedJson);

            // Assert
            Assert.NotNull(manifest);
            Assert.NotNull(manifest.Checksum);
            Assert.Equal("md5", manifest.Checksum.Algorithm);
            Assert.NotEmpty(manifest.Checksum.Value);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Pack_EmbeddedManifestContainsTargetFramework()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");
        var binDir = Path.Combine(tempDir, "bin", "Release", "net8.0");
        var outputDir = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(outputDir);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0""
            }";

            File.WriteAllText(Path.Combine(tempDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(binDir, "TestModule.dll"), "fake dll content");

            var packService = new PackService();
            packService.Pack(tempDir, outputDir, noBuild: true);

            // Act - Read module.json from the .oamod
            var oamodPath = Path.Combine(outputDir, "TestModule.oamod");
            using var archive = ZipFile.OpenRead(oamodPath);
            var manifestEntry = archive.GetEntry("module.json");
            using var stream = manifestEntry!.Open();
            using var reader = new StreamReader(stream);
            var embeddedJson = reader.ReadToEnd();
            var manifest = JsonSerializer.Deserialize<ModuleManifest>(embeddedJson);

            // Assert
            Assert.NotNull(manifest);
            Assert.Equal("net8.0", manifest.TargetFramework);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Pack_MissingModuleJson_ReturnsGeneralError()
    {
        // Arrange - Create directory without module.json
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");
        var outputDir = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outputDir);

            var packService = new PackService();

            // Act
            var exitCode = packService.Pack(tempDir, outputDir, noBuild: true);

            // Assert
            Assert.Equal(ExitCodes.GeneralError, exitCode);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }

    [Fact]
    public void Pack_NonExistentDirectory_ReturnsGeneralError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");
        var outputDir = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(outputDir);
            var packService = new PackService();

            // Act
            var exitCode = packService.Pack(nonExistentPath, outputDir, noBuild: true);

            // Assert
            Assert.Equal(ExitCodes.GeneralError, exitCode);
        }
        finally
        {
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }
}