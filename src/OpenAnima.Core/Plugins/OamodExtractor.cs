using System.IO.Compression;

namespace OpenAnima.Core.Plugins;

/// <summary>
/// Extracts .oamod packages (ZIP files) to loadable directories.
/// </summary>
public static class OamodExtractor
{
    private const string ExtractedSubdirectory = ".extracted";
    private const string TimestampMarkerFile = ".extraction-timestamp";

    /// <summary>
    /// Extracts a .oamod file to a subdirectory of extractBasePath.
    /// Returns the path to the extracted directory (ready for PluginLoader.LoadModule).
    /// </summary>
    public static string Extract(string oamodPath, string extractBasePath)
    {
        // Derive module name from filename
        string moduleName = Path.GetFileNameWithoutExtension(oamodPath);

        // Extract directory: extractBasePath/.extracted/moduleName
        string extractedBaseDir = Path.Combine(extractBasePath, ExtractedSubdirectory);
        string extractDir = Path.Combine(extractedBaseDir, moduleName);

        // Create .extracted/ subdirectory if it doesn't exist
        if (!Directory.Exists(extractedBaseDir))
        {
            Directory.CreateDirectory(extractedBaseDir);
        }

        // If the extract directory already exists, delete it recursively (clean re-extract for idempotency)
        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, true);
        }

        // Extract the ZIP
        ZipFile.ExtractToDirectory(oamodPath, extractDir);

        // Write timestamp marker file
        string markerPath = Path.Combine(extractDir, TimestampMarkerFile);
        File.WriteAllText(markerPath, File.GetLastWriteTimeUtc(oamodPath).ToString("O"));

        return extractDir;
    }

    /// <summary>
    /// Checks if a .oamod file needs extraction by comparing timestamps.
    /// Returns true if the .oamod is newer than the extracted version or if no extraction exists.
    /// </summary>
    public static bool NeedsExtraction(string oamodPath, string extractBasePath)
    {
        string moduleName = Path.GetFileNameWithoutExtension(oamodPath);
        string extractDir = Path.Combine(extractBasePath, ExtractedSubdirectory, moduleName);
        string markerPath = Path.Combine(extractDir, TimestampMarkerFile);

        // If extraction doesn't exist, needs extraction
        if (!Directory.Exists(extractDir) || !File.Exists(markerPath))
        {
            return true;
        }

        // Compare timestamps
        try
        {
            string markerContent = File.ReadAllText(markerPath);
            DateTime extractedTimestamp = DateTime.Parse(markerContent);
            DateTime oamodTimestamp = File.GetLastWriteTimeUtc(oamodPath);

            // If .oamod is newer, needs re-extraction
            return oamodTimestamp > extractedTimestamp;
        }
        catch
        {
            // If marker file is corrupted or unreadable, re-extract
            return true;
        }
    }
}
