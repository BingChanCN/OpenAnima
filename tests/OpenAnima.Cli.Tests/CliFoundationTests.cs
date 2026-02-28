using Xunit;
using OpenAnima.Cli.Services;
using OpenAnima.Cli.Models;
using OpenAnima.Core.Plugins;
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

        var outWriter = new StringWriter();
        var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            return Program.Main(args);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            outWriter.Dispose();
            errorWriter.Dispose();
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

        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

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
            Console.SetOut(originalOut);
            Console.SetError(originalError);
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

        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

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
            Console.SetOut(originalOut);
            Console.SetError(originalError);
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

        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

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
            Console.SetOut(originalOut);
            Console.SetError(originalError);
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

        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

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
            Console.SetOut(originalOut);
            Console.SetError(originalError);
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

        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

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
            Console.SetOut(originalOut);
            Console.SetError(originalError);
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

        var originalOut = Console.Out;
        var originalError = Console.Error;

        try
        {
            using var outWriter = new StringWriter();
            using var errorWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            Directory.CreateDirectory(outputDir);
            var packService = new PackService();

            // Act
            var exitCode = packService.Pack(nonExistentPath, outputDir, noBuild: true);

            // Assert
            Assert.Equal(ExitCodes.GeneralError, exitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }
}

/// <summary>
/// Unit tests for PackCommand.
/// </summary>
public class PackCommandTests
{
    [Fact]
    public void PackCommand_NonExistentDirectory_ReturnsGeneralError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}");
        var args = new[] { "pack", nonExistentPath };

        // Act
        var exitCode = RunCliWithArgs(args);

        // Assert
        Assert.Equal(ExitCodes.GeneralError, exitCode);
    }

    [Fact]
    public void PackCommand_HelpOutput_ContainsPack()
    {
        // Arrange
        var args = new[] { "--help" };

        // Act
        var (exitCode, stdout) = RunCliWithArgsAndCaptureOutput(args);

        // Assert
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("pack", stdout);
    }

    [Fact]
    public void PackCommand_IntegrationTest_CreatesOamodFile()
    {
        // Arrange - Create a temp module directory with module.json + fake DLL
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-module-{Guid.NewGuid()}");
        var binDir = Path.Combine(tempDir, "bin", "Release", "net8.0");
        var outputDir = Path.Combine(Path.GetTempPath(), $"test-output-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(binDir);
            Directory.CreateDirectory(outputDir);

            var manifestJson = @"{
                ""id"": ""IntegrationTestModule"",
                ""name"": ""IntegrationTestModule"",
                ""version"": ""1.0.0""
            }";

            File.WriteAllText(Path.Combine(tempDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(binDir, "IntegrationTestModule.dll"), "fake dll content");

            var args = new[] { "pack", tempDir, "--no-build", "-o", outputDir };

            // Act
            var exitCode = RunCliWithArgs(args);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
            var oamodPath = Path.Combine(outputDir, "IntegrationTestModule.oamod");
            Assert.True(File.Exists(oamodPath), "Expected .oamod file to exist");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(outputDir)) Directory.Delete(outputDir, true);
        }
    }

    /// <summary>
    /// Helper method to run CLI with captured output.
    /// </summary>
    private static (int ExitCode, string StdOut) RunCliWithArgsAndCaptureOutput(string[] args)
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

            // Get the string BEFORE restoring console
            var stdout = outWriter.ToString();

            return (exitCode, stdout);
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

        var outWriter = new StringWriter();
        var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            return Program.Main(args);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            outWriter.Dispose();
            errorWriter.Dispose();
        }
    }
}
/// <summary>
/// Unit tests for OamodExtractor.
/// </summary>
public class OamodExtractorTests
{
    [Fact]
    public void Extract_ValidOamod_ExtractsToCorrectDirectory()
    {
        // Arrange - Create a temp .oamod file (ZIP with module.json and dummy DLL)
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-oamod-{Guid.NewGuid()}");
        var extractBasePath = Path.Combine(Path.GetTempPath(), $"test-extract-{Guid.NewGuid()}");
        var oamodPath = Path.Combine(tempDir, "TestModule.oamod");

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(extractBasePath);

            // Create a ZIP with module.json and a dummy DLL
            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0"",
                ""entryAssembly"": ""TestModule.dll""
            }";

            var tempModuleDir = Path.Combine(tempDir, "module-content");
            Directory.CreateDirectory(tempModuleDir);
            File.WriteAllText(Path.Combine(tempModuleDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(tempModuleDir, "TestModule.dll"), "fake dll content");

            ZipFile.CreateFromDirectory(tempModuleDir, oamodPath);

            // Act
            var extractedDir = OamodExtractor.Extract(oamodPath, extractBasePath);

            // Assert
            Assert.True(Directory.Exists(extractedDir), "Extracted directory should exist");
            Assert.True(File.Exists(Path.Combine(extractedDir, "module.json")), "module.json should exist");
            Assert.True(File.Exists(Path.Combine(extractedDir, "TestModule.dll")), "TestModule.dll should exist");
            Assert.Contains(".extracted", extractedDir);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(extractBasePath)) Directory.Delete(extractBasePath, true);
        }
    }

    [Fact]
    public void Extract_CalledTwice_IsIdempotent()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-oamod-{Guid.NewGuid()}");
        var extractBasePath = Path.Combine(Path.GetTempPath(), $"test-extract-{Guid.NewGuid()}");
        var oamodPath = Path.Combine(tempDir, "TestModule.oamod");

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(extractBasePath);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0"",
                ""entryAssembly"": ""TestModule.dll""
            }";

            var tempModuleDir = Path.Combine(tempDir, "module-content");
            Directory.CreateDirectory(tempModuleDir);
            File.WriteAllText(Path.Combine(tempModuleDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(tempModuleDir, "TestModule.dll"), "fake dll content");

            ZipFile.CreateFromDirectory(tempModuleDir, oamodPath);

            // Act - Extract twice
            var extractedDir1 = OamodExtractor.Extract(oamodPath, extractBasePath);
            var extractedDir2 = OamodExtractor.Extract(oamodPath, extractBasePath);

            // Assert - Both should succeed and point to the same directory
            Assert.Equal(extractedDir1, extractedDir2);
            Assert.True(File.Exists(Path.Combine(extractedDir2, "module.json")));
            Assert.True(File.Exists(Path.Combine(extractedDir2, "TestModule.dll")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(extractBasePath)) Directory.Delete(extractBasePath, true);
        }
    }

    [Fact]
    public void NeedsExtraction_NoExistingExtraction_ReturnsTrue()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-oamod-{Guid.NewGuid()}");
        var extractBasePath = Path.Combine(Path.GetTempPath(), $"test-extract-{Guid.NewGuid()}");
        var oamodPath = Path.Combine(tempDir, "TestModule.oamod");

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(extractBasePath);
            File.WriteAllText(oamodPath, "fake oamod content");

            // Act
            var needsExtraction = OamodExtractor.NeedsExtraction(oamodPath, extractBasePath);

            // Assert
            Assert.True(needsExtraction, "Should need extraction when no extraction exists");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(extractBasePath)) Directory.Delete(extractBasePath, true);
        }
    }

    [Fact]
    public void NeedsExtraction_AfterExtraction_ReturnsFalse()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-oamod-{Guid.NewGuid()}");
        var extractBasePath = Path.Combine(Path.GetTempPath(), $"test-extract-{Guid.NewGuid()}");
        var oamodPath = Path.Combine(tempDir, "TestModule.oamod");

        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(extractBasePath);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0"",
                ""entryAssembly"": ""TestModule.dll""
            }";

            var tempModuleDir = Path.Combine(tempDir, "module-content");
            Directory.CreateDirectory(tempModuleDir);
            File.WriteAllText(Path.Combine(tempModuleDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(tempModuleDir, "TestModule.dll"), "fake dll content");

            ZipFile.CreateFromDirectory(tempModuleDir, oamodPath);

            // Act - Extract first
            OamodExtractor.Extract(oamodPath, extractBasePath);

            // Then check if needs extraction
            var needsExtraction = OamodExtractor.NeedsExtraction(oamodPath, extractBasePath);

            // Assert
            Assert.False(needsExtraction, "Should not need extraction after successful extraction");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            if (Directory.Exists(extractBasePath)) Directory.Delete(extractBasePath, true);
        }
    }
}

/// <summary>
/// Unit tests for ModuleNameValidator service.
/// </summary>
public class ModuleNameValidatorTests
{
    [Fact]
    public void Validate_ValidName_ReturnsNoErrors()
    {
        var errors = ModuleNameValidator.Validate("MyModule");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ValidNameWithUnderscore_ReturnsNoErrors()
    {
        var errors = ModuleNameValidator.Validate("My_Module");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ValidNameWithDigits_ReturnsNoErrors()
    {
        var errors = ModuleNameValidator.Validate("Module123");
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_EmptyName_ReturnsError()
    {
        var errors = ModuleNameValidator.Validate("");
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("required"));
    }

    [Fact]
    public void Validate_ReservedKeyword_ReturnsError()
    {
        var errors = ModuleNameValidator.Validate("class");
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("reserved"));
    }

    [Fact]
    public void Validate_StartsWithDigit_ReturnsError()
    {
        var errors = ModuleNameValidator.Validate("123Module");
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("start with a letter"));
    }

    [Fact]
    public void Validate_ContainsInvalidCharacter_ReturnsError()
    {
        var errors = ModuleNameValidator.Validate("My-Module");
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("invalid character"));
    }

    [Fact]
    public void Validate_ContainsSpace_ReturnsError()
    {
        var errors = ModuleNameValidator.Validate("My Module");
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("invalid character"));
    }

    [Fact]
    public void IsValid_ValidName_ReturnsTrue()
    {
        Assert.True(ModuleNameValidator.IsValid("MyModule"));
    }

    [Fact]
    public void IsValid_InvalidName_ReturnsFalse()
    {
        Assert.False(ModuleNameValidator.IsValid("123Module"));
    }

    [Fact]
    public void GetSuggestion_ReservedKeyword_PrefixesWithMy()
    {
        var suggestion = ModuleNameValidator.GetSuggestion("class");
        Assert.NotNull(suggestion);
        Assert.StartsWith("My", suggestion);
    }

    [Fact]
    public void GetSuggestion_StartsWithDigit_PrefixesWithModule()
    {
        var suggestion = ModuleNameValidator.GetSuggestion("123Test");
        Assert.NotNull(suggestion);
        Assert.StartsWith("Module", suggestion);
    }

    [Fact]
    public void GetSuggestion_InvalidCharacters_ReplacesWithUnderscore()
    {
        var suggestion = ModuleNameValidator.GetSuggestion("My-Module");
        Assert.NotNull(suggestion);
        Assert.Contains("_", suggestion);
        Assert.DoesNotContain("-", suggestion);
    }
}

/// <summary>
/// Unit tests for NewCommand.
/// </summary>
public class NewCommandTests
{
    [Fact]
    public void NewCommand_ValidModuleName_ReturnsSuccess()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-new-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var args = new[] { "new", "TestModule", "-o", tempDir };

            // Act
            var exitCode = RunCliWithArgs(args);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.True(Directory.Exists(Path.Combine(tempDir, "TestModule")));
            Assert.True(File.Exists(Path.Combine(tempDir, "TestModule", "TestModule.cs")));
            Assert.True(File.Exists(Path.Combine(tempDir, "TestModule", "TestModule.csproj")));
            Assert.True(File.Exists(Path.Combine(tempDir, "TestModule", "module.json")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NewCommand_InvalidModuleName_ReturnsValidationError()
    {
        // Arrange
        var args = new[] { "new", "123Invalid" };

        // Act
        var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

        // Assert
        Assert.Equal(ExitCodes.ValidationError, exitCode);
        Assert.Contains("start with a letter", stderr);
    }

    [Fact]
    public void NewCommand_ReservedKeyword_ReturnsValidationError()
    {
        // Arrange
        var args = new[] { "new", "class" };

        // Act
        var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

        // Assert
        Assert.Equal(ExitCodes.ValidationError, exitCode);
        Assert.Contains("reserved", stderr);
    }

    [Fact]
    public void NewCommand_DryRun_DoesNotCreateFiles()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-new-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var args = new[] { "new", "TestModule", "-o", tempDir, "--dry-run" };

            // Act
            var (exitCode, stdout) = RunCliWithArgsAndCaptureOutput(args);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
            Assert.Contains("TestModule.cs", stdout);
            Assert.Contains("TestModule.csproj", stdout);
            Assert.Contains("module.json", stdout);
            Assert.False(Directory.Exists(Path.Combine(tempDir, "TestModule")));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NewCommand_WithInputPorts_GeneratesPortAttributes()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-new-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var args = new[] { "new", "TestModule", "-o", tempDir, "--inputs", "TextInput" };

            // Act
            var exitCode = RunCliWithArgs(args);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
            var csContent = File.ReadAllText(Path.Combine(tempDir, "TestModule", "TestModule.cs"));
            Assert.Contains("[InputPort(\"TextInput\", PortType.Text)]", csContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NewCommand_WithOutputPorts_GeneratesPortAttributes()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-new-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var args = new[] { "new", "TestModule", "-o", tempDir, "--outputs", "TriggerOutput:Trigger" };

            // Act
            var exitCode = RunCliWithArgs(args);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
            var csContent = File.ReadAllText(Path.Combine(tempDir, "TestModule", "TestModule.cs"));
            Assert.Contains("[OutputPort(\"TriggerOutput\", PortType.Trigger)]", csContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NewCommand_WithMultiplePorts_GeneratesAllAttributes()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-new-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var args = new[] { "new", "TestModule", "-o", tempDir,
                "--inputs", "Input1", "Input2:Text",
                "--outputs", "Output1", "Output2:Trigger" };

            // Act
            var exitCode = RunCliWithArgs(args);

            // Assert
            Assert.Equal(ExitCodes.Success, exitCode);
            var csContent = File.ReadAllText(Path.Combine(tempDir, "TestModule", "TestModule.cs"));
            Assert.Contains("[InputPort(\"Input1\", PortType.Text)]", csContent);
            Assert.Contains("[InputPort(\"Input2\", PortType.Text)]", csContent);
            Assert.Contains("[OutputPort(\"Output1\", PortType.Text)]", csContent);
            Assert.Contains("[OutputPort(\"Output2\", PortType.Trigger)]", csContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NewCommand_InvalidPortType_ReturnsValidationError()
    {
        // Arrange
        var args = new[] { "new", "TestModule", "--inputs", "Input:InvalidType" };

        // Act
        var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

        // Assert
        Assert.Equal(ExitCodes.ValidationError, exitCode);
        Assert.Contains("Invalid", stderr);
        Assert.Contains("port type", stderr);
    }

    [Fact]
    public void NewCommand_ExistingDirectory_ReturnsGeneralError()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-new-{Guid.NewGuid()}");
        var moduleDir = Path.Combine(tempDir, "TestModule");

        try
        {
            Directory.CreateDirectory(moduleDir);
            var args = new[] { "new", "TestModule", "-o", tempDir };

            // Act
            var (exitCode, stderr) = RunCliWithArgsAndCaptureError(args);

            // Assert
            Assert.Equal(ExitCodes.GeneralError, exitCode);
            Assert.Contains("already exists", stderr);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void NewCommand_GeneratedFilesContainExpectedContent()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"test-new-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(tempDir);
            var args = new[] { "new", "TestModule", "-o", tempDir };

            // Act - Create module
            var exitCode = RunCliWithArgs(args);
            Assert.Equal(ExitCodes.Success, exitCode);

            // Assert - Verify file contents
            var csContent = File.ReadAllText(Path.Combine(tempDir, "TestModule", "TestModule.cs"));
            var csprojContent = File.ReadAllText(Path.Combine(tempDir, "TestModule", "TestModule.csproj"));
            var jsonContent = File.ReadAllText(Path.Combine(tempDir, "TestModule", "module.json"));

            // Verify C# file
            Assert.Contains("namespace TestModule", csContent);
            Assert.Contains("class TestModule", csContent);
            Assert.Contains("IModule", csContent);
            Assert.Contains("IModuleMetadata", csContent);

            // Verify project file
            Assert.Contains("net8.0", csprojContent);
            Assert.Contains("OpenAnima.Contracts", csprojContent);

            // Verify manifest
            Assert.Contains("\"id\": \"TestModule\"", jsonContent);
            Assert.Contains("\"name\": \"TestModule\"", jsonContent);
            Assert.Contains("\"version\": \"1.0.0\"", jsonContent);
            Assert.Contains("\"schemaVersion\"", jsonContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
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
    private static (int ExitCode, string StdOut) RunCliWithArgsAndCaptureOutput(string[] args)
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
            var stdout = outWriter.ToString();

            return (exitCode, stdout);
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

        var outWriter = new StringWriter();
        var errorWriter = new StringWriter();

        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errorWriter);

            return Program.Main(args);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            outWriter.Dispose();
            errorWriter.Dispose();
        }
    }
}

/// <summary>
/// Unit tests for PluginLoader.ScanDirectory with .oamod files.
/// </summary>
public class PluginLoaderOamodTests
{
    [Fact]
    public void ScanDirectory_WithOamodFile_ExtractsAndLoads()
    {
        // Arrange - Create a modules directory with a .oamod file
        var modulesPath = Path.Combine(Path.GetTempPath(), $"test-modules-{Guid.NewGuid()}");
        var oamodPath = Path.Combine(modulesPath, "TestModule.oamod");

        try
        {
            Directory.CreateDirectory(modulesPath);

            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0"",
                ""entryAssembly"": ""TestModule.dll""
            }";

            var tempModuleDir = Path.Combine(Path.GetTempPath(), $"module-content-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempModuleDir);
            File.WriteAllText(Path.Combine(tempModuleDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(tempModuleDir, "TestModule.dll"), "fake dll content");

            ZipFile.CreateFromDirectory(tempModuleDir, oamodPath);
            Directory.Delete(tempModuleDir, true);

            var loader = new PluginLoader();

            // Act
            var results = loader.ScanDirectory(modulesPath);

            // Assert - Should have attempted to load the .oamod
            Assert.NotEmpty(results);
            // The load will fail (fake DLL), but extraction should have occurred
            var extractedDir = Path.Combine(modulesPath, ".extracted", "TestModule");
            Assert.True(Directory.Exists(extractedDir), "Extracted directory should exist");
            Assert.True(File.Exists(Path.Combine(extractedDir, "module.json")), "module.json should exist in extracted directory");
        }
        finally
        {
            if (Directory.Exists(modulesPath)) Directory.Delete(modulesPath, true);
        }
    }

    [Fact]
    public void ScanDirectory_SkipsExtractedDirectory()
    {
        // Arrange - Create a modules directory with both a .oamod and a regular module
        var modulesPath = Path.Combine(Path.GetTempPath(), $"test-modules-{Guid.NewGuid()}");
        var oamodPath = Path.Combine(modulesPath, "TestModule.oamod");
        var regularModuleDir = Path.Combine(modulesPath, "RegularModule");

        try
        {
            Directory.CreateDirectory(modulesPath);
            Directory.CreateDirectory(regularModuleDir);

            // Create regular module
            var regularManifest = @"{
                ""id"": ""RegularModule"",
                ""name"": ""RegularModule"",
                ""version"": ""1.0.0"",
                ""entryAssembly"": ""RegularModule.dll""
            }";
            File.WriteAllText(Path.Combine(regularModuleDir, "module.json"), regularManifest);

            // Create .oamod
            var manifestJson = @"{
                ""id"": ""TestModule"",
                ""name"": ""TestModule"",
                ""version"": ""1.0.0"",
                ""entryAssembly"": ""TestModule.dll""
            }";

            var tempModuleDir = Path.Combine(Path.GetTempPath(), $"module-content-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempModuleDir);
            File.WriteAllText(Path.Combine(tempModuleDir, "module.json"), manifestJson);
            File.WriteAllText(Path.Combine(tempModuleDir, "TestModule.dll"), "fake dll content");

            ZipFile.CreateFromDirectory(tempModuleDir, oamodPath);
            Directory.Delete(tempModuleDir, true);

            var loader = new PluginLoader();

            // Act
            var results = loader.ScanDirectory(modulesPath);

            // Assert - Should have 2 results: RegularModule and TestModule (from .oamod)
            // Not 3 (which would indicate .extracted was scanned as a separate module)
            Assert.Equal(2, results.Count);
        }
        finally
        {
            if (Directory.Exists(modulesPath)) Directory.Delete(modulesPath, true);
        }
    }
}
