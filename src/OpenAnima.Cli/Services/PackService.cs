using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using OpenAnima.Cli.Models;

namespace OpenAnima.Cli.Services;

/// <summary>
/// Service for packing modules into .oamod files.
/// </summary>
public class PackService
{
    /// <summary>
    /// Packs a module into a .oamod file.
    /// </summary>
    /// <param name="modulePath">Path to the module project directory.</param>
    /// <param name="outputPath">Output directory for the .oamod file (null = current directory).</param>
    /// <param name="noBuild">If true, skip building the project.</param>
    /// <returns>Exit code (0 = success, 1 = general error, 2 = validation error).</returns>
    public int Pack(string modulePath, string? outputPath, bool noBuild)
    {
        // Validate module path exists
        if (!Directory.Exists(modulePath))
        {
            Console.Error.WriteLine($"Error: Directory not found: {modulePath}");
            return ExitCodes.GeneralError;
        }

        // Check module.json exists
        var manifestPath = Path.Combine(modulePath, "module.json");
        if (!File.Exists(manifestPath))
        {
            Console.Error.WriteLine($"Error: module.json not found in {modulePath}");
            return ExitCodes.GeneralError;
        }

        // Parse and validate manifest
        var manifestJson = File.ReadAllText(manifestPath);
        var (manifest, errors) = ManifestValidator.ValidateJson(manifestJson);

        if (errors.Any())
        {
            Console.Error.WriteLine("Validation errors:");
            foreach (var error in errors)
            {
                Console.Error.WriteLine($"  - {error}");
            }
            return ExitCodes.ValidationError;
        }

        if (manifest == null)
        {
            Console.Error.WriteLine("Error: Failed to parse manifest");
            return ExitCodes.GeneralError;
        }

        // Build project if needed
        if (!noBuild)
        {
            var buildResult = BuildProject(modulePath);
            if (buildResult != 0)
            {
                Console.Error.WriteLine("Error: Build failed");
                return ExitCodes.GeneralError;
            }
        }

        // Find compiled DLL
        var dllName = manifest.GetEntryAssembly();
        var dllPath = FindCompiledDll(modulePath, dllName);

        if (dllPath == null)
        {
            Console.Error.WriteLine($"Error: Compiled DLL not found: {dllName}");
            Console.Error.WriteLine($"Searched in bin/Release/net8.0 and bin/Debug/net8.0");
            return ExitCodes.GeneralError;
        }

        // Compute MD5 checksum
        var checksumValue = ComputeMd5(dllPath);

        // Create enriched manifest (in memory only)
        var enrichedManifest = new ModuleManifest
        {
            SchemaVersion = manifest.SchemaVersion,
            Id = manifest.Id,
            Name = manifest.Name,
            Version = manifest.Version,
            Description = manifest.Description,
            Author = manifest.Author,
            EntryAssembly = manifest.EntryAssembly,
            OpenAnima = manifest.OpenAnima,
            Ports = manifest.Ports,
            TargetFramework = string.IsNullOrEmpty(manifest.TargetFramework) ? "net8.0" : manifest.TargetFramework,
            Checksum = new ChecksumInfo
            {
                Algorithm = "md5",
                Value = checksumValue
            }
        };

        // Serialize enriched manifest
        var enrichedJson = JsonSerializer.Serialize(enrichedManifest, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        // Create .oamod file
        var outputDir = outputPath ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDir);

        var oamodPath = Path.Combine(outputDir, $"{manifest.Id}.oamod");

        // Delete existing .oamod if present
        if (File.Exists(oamodPath))
        {
            File.Delete(oamodPath);
        }

        using (var archive = ZipFile.Open(oamodPath, ZipArchiveMode.Create))
        {
            // Add enriched module.json
            var manifestEntry = archive.CreateEntry("module.json");
            using (var entryStream = manifestEntry.Open())
            using (var writer = new StreamWriter(entryStream))
            {
                writer.Write(enrichedJson);
            }

            // Add DLL
            archive.CreateEntryFromFile(dllPath, Path.GetFileName(dllPath));
        }

        Console.WriteLine($"Packed: {oamodPath}");
        return ExitCodes.Success;
    }

    /// <summary>
    /// Builds the project using dotnet build.
    /// </summary>
    private int BuildProject(string modulePath)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"build \"{modulePath}\" --configuration Release",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                Console.Error.WriteLine("Error: Failed to start dotnet build process");
                return ExitCodes.GeneralError;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine("Error: dotnet CLI not found. Please install .NET SDK.");
            return ExitCodes.GeneralError;
        }
    }

    /// <summary>
    /// Finds the compiled DLL in bin/Release or bin/Debug.
    /// </summary>
    private string? FindCompiledDll(string modulePath, string dllName)
    {
        // Search Release first (since we build Release)
        var releasePath = Path.Combine(modulePath, "bin", "Release", "net8.0", dllName);
        if (File.Exists(releasePath))
        {
            return releasePath;
        }

        // Fallback to Debug
        var debugPath = Path.Combine(modulePath, "bin", "Debug", "net8.0", dllName);
        if (File.Exists(debugPath))
        {
            return debugPath;
        }

        return null;
    }

    /// <summary>
    /// Computes MD5 checksum of a file.
    /// </summary>
    private static string ComputeMd5(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        var hash = md5.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
