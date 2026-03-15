using System.Text.RegularExpressions;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Source-level audit tests that codify the Phase 36 built-in module inventory
/// and the allowed Core-namespace exception policy.
/// </summary>
public class BuiltInModuleDecouplingTests
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string ModulesDirectory =
        Path.Combine(RepoRoot, "src", "OpenAnima.Core", "Modules");

    private static readonly IReadOnlyList<string> ActiveModuleSourceFiles =
    [
        "LLMModule.cs",
        "ChatInputModule.cs",
        "ChatOutputModule.cs",
        "HeartbeatModule.cs",
        "FixedTextModule.cs",
        "TextJoinModule.cs",
        "TextSplitModule.cs",
        "ConditionalBranchModule.cs",
        "AnimaInputPortModule.cs",
        "AnimaOutputPortModule.cs",
        "AnimaRouteModule.cs",
        "HttpRequestModule.cs"
    ];

    private static readonly Regex CoreUsingRegex =
        new(@"^\s*using\s+OpenAnima\.Core\.[^;]+;\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [Fact]
    [Trait("Category", "Integration")]
    public void ActiveBuiltInInventory_IsHardCodedAsTheAuthoritative12ModuleSet()
    {
        Assert.True(Directory.Exists(ModulesDirectory), $"Modules directory not found: {ModulesDirectory}");
        Assert.Equal(12, ActiveModuleSourceFiles.Count);

        foreach (var fileName in ActiveModuleSourceFiles)
        {
            Assert.True(File.Exists(GetModulePath(fileName)), $"Expected built-in module source missing: {fileName}");
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void HelperFiles_ArePresent_ButNotCountedAsActiveBuiltInModules()
    {
        var moduleFiles = Directory.GetFiles(ModulesDirectory, "*.cs")
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("FormatDetector.cs", moduleFiles);
        Assert.Contains("ModuleMetadataRecord.cs", moduleFiles);

        Assert.DoesNotContain("FormatDetector.cs", ActiveModuleSourceFiles);
        Assert.DoesNotContain("ModuleMetadataRecord.cs", ActiveModuleSourceFiles);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NonLlmBuiltInModules_HaveNoCoreUsings_AndLlmModuleHasOnlyTheDocumentedException()
    {
        var failures = new List<string>();

        foreach (var fileName in ActiveModuleSourceFiles.Where(name => name != "LLMModule.cs"))
        {
            var coreUsings = GetCoreUsings(fileName);
            if (coreUsings.Count > 0)
            {
                failures.Add($"{fileName}: unexpected Core using(s): {string.Join(", ", coreUsings)}");
            }
        }

        var llmCoreUsings = GetCoreUsings("LLMModule.cs");
        if (llmCoreUsings.Count != 1 || llmCoreUsings[0] != "using OpenAnima.Core.LLM;")
        {
            failures.Add(
                $"LLMModule.cs: expected only 'using OpenAnima.Core.LLM;' but found {FormatUsings(llmCoreUsings)}");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static List<string> GetCoreUsings(string fileName) =>
        CoreUsingRegex.Matches(File.ReadAllText(GetModulePath(fileName)))
            .Select(match => match.Value.Trim())
            .ToList();

    private static string GetModulePath(string fileName) =>
        Path.Combine(ModulesDirectory, fileName);

    private static string FormatUsings(IReadOnlyCollection<string> usings) =>
        usings.Count == 0 ? "<none>" : string.Join(", ", usings);
}
