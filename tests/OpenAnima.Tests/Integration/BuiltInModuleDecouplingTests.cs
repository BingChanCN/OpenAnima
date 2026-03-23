using System.Text.RegularExpressions;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Source-level audit tests that codify the Phase 36 built-in module inventory
/// and the allowed Core-namespace exception policy.
/// </summary>
public class BuiltInModuleDecouplingTests
{
    private static readonly string RepoRoot = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")))
                return current.FullName;
            current = current.Parent;
        }
        throw new InvalidOperationException("Could not find repository root (.git directory not found)");
    }

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

        foreach (var fileName in ActiveModuleSourceFiles.Where(name => name != "LLMModule.cs" && name != "ChatInputModule.cs"))
        {
            var coreUsings = GetCoreUsings(fileName);
            if (coreUsings.Count > 0)
            {
                failures.Add($"{fileName}: unexpected Core using(s): {string.Join(", ", coreUsings)}");
            }
        }

        // LLMModule exceptions:
        // - OpenAnima.Core.LLM (documented in Phase 36)
        // - OpenAnima.Core.Providers (Phase 51: ILLMProviderRegistry + LLMProviderRegistryService)
        // - OpenAnima.Core.Services (Phase 51: IAnimaModuleConfigService for auto-clear SetConfigAsync)
        // - OpenAnima.Core.Memory (Phase 52: IMemoryRecallService for automatic memory recall)
        // - OpenAnima.Core.Runs (Phase 52: IStepRecorder for MemoryRecall StepRecord in run timeline)
        // - OpenAnima.Core.Tools (Phase 53: WorkspaceToolModule + ToolDescriptor for tool descriptor injection)
        // - OpenAnima.Core.Events (Phase 59: ToolCallStartedPayload + ToolCallCompletedPayload for agent tool call visibility)
        var llmCoreUsings = GetCoreUsings("LLMModule.cs");
        var expectedLlmUsings = new HashSet<string>
        {
            "using OpenAnima.Core.LLM;",
            "using OpenAnima.Core.Providers;",
            "using OpenAnima.Core.Services;",
            "using OpenAnima.Core.Memory;",
            "using OpenAnima.Core.Runs;",
            "using OpenAnima.Core.Tools;",
            "using OpenAnima.Core.Events;"
        };
        var unexpectedLlmUsings = llmCoreUsings.Where(u => !expectedLlmUsings.Contains(u)).ToList();
        var missingLlmUsings = expectedLlmUsings.Where(u => !llmCoreUsings.Contains(u)).ToList();
        if (unexpectedLlmUsings.Count > 0 || missingLlmUsings.Count > 0)
        {
            var issues = new List<string>();
            if (unexpectedLlmUsings.Count > 0)
                issues.Add($"unexpected: {FormatUsings(unexpectedLlmUsings)}");
            if (missingLlmUsings.Count > 0)
                issues.Add($"missing: {FormatUsings(missingLlmUsings)}");
            failures.Add($"LLMModule.cs: Core using mismatch — {string.Join("; ", issues)}");
        }

        // ChatInputModule exception: OpenAnima.Core.Channels (Phase 37 wiring requirement)
        // ChatInputModule routes through ActivityChannelHost (internal sealed class in Core.Channels).
        // Since ActivityChannelHost is internal, ChatInputModule must be in the same assembly.
        var chatInputCoreUsings = GetCoreUsings("ChatInputModule.cs");
        if (chatInputCoreUsings.Count != 1 || chatInputCoreUsings[0] != "using OpenAnima.Core.Channels;")
        {
            failures.Add(
                $"ChatInputModule.cs: expected only 'using OpenAnima.Core.Channels;' but found {FormatUsings(chatInputCoreUsings)}");
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
