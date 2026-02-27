using OpenAnima.Core.Wiring;

namespace OpenAnima.Core.Services;

/// <summary>
/// Validates whether the current wiring configuration can support ChatPanel conversation flow.
/// Required chain: ChatInputModule.userMessage -> LLMModule.prompt -> ChatOutputModule.displayText.
/// </summary>
public static class ChatPipelineConfigurationValidator
{
    private const string ChatInputModuleName = "ChatInputModule";
    private const string LlmModuleName = "LLMModule";
    private const string ChatOutputModuleName = "ChatOutputModule";

    public static bool IsConfigured(WiringConfiguration? configuration)
    {
        if (configuration == null || configuration.Nodes.Count == 0 || configuration.Connections.Count == 0)
            return false;

        var moduleNameById = configuration.Nodes.ToDictionary(node => node.ModuleId, node => node.ModuleName);

        var hasInputToLlm = configuration.Connections.Any(connection =>
            moduleNameById.TryGetValue(connection.SourceModuleId, out var sourceModuleName) &&
            moduleNameById.TryGetValue(connection.TargetModuleId, out var targetModuleName) &&
            sourceModuleName == ChatInputModuleName &&
            targetModuleName == LlmModuleName &&
            connection.SourcePortName == "userMessage" &&
            connection.TargetPortName == "prompt");

        if (!hasInputToLlm)
            return false;

        var hasLlmToOutput = configuration.Connections.Any(connection =>
            moduleNameById.TryGetValue(connection.SourceModuleId, out var sourceModuleName) &&
            moduleNameById.TryGetValue(connection.TargetModuleId, out var targetModuleName) &&
            sourceModuleName == LlmModuleName &&
            targetModuleName == ChatOutputModuleName &&
            connection.SourcePortName == "response" &&
            connection.TargetPortName == "displayText");

        return hasLlmToOutput;
    }
}
