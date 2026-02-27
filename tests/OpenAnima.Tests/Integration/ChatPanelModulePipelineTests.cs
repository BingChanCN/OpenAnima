using Microsoft.Extensions.Logging.Abstractions;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.LLM;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Services;
using OpenAnima.Core.Wiring;

namespace OpenAnima.Tests.Integration;

/// <summary>
/// Integration-style checks for ChatPanel's module pipeline contract:
/// required chain detection and missing-pipeline behavior.
/// </summary>
public class ChatPanelModulePipelineTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void ChatPipelineConfigurationValidator_ReturnsTrue_WhenRequiredChainExists()
    {
        var config = new WiringConfiguration
        {
            Name = "chat-valid",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "node-input", ModuleName = "ChatInputModule" },
                new() { ModuleId = "node-llm", ModuleName = "LLMModule" },
                new() { ModuleId = "node-output", ModuleName = "ChatOutputModule" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "node-input",
                    SourcePortName = "userMessage",
                    TargetModuleId = "node-llm",
                    TargetPortName = "prompt"
                },
                new()
                {
                    SourceModuleId = "node-llm",
                    SourcePortName = "response",
                    TargetModuleId = "node-output",
                    TargetPortName = "displayText"
                }
            }
        };

        Assert.True(ChatPipelineConfigurationValidator.IsConfigured(config));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void ChatPipelineConfigurationValidator_ReturnsFalse_WhenRequiredLinkMissing()
    {
        var config = new WiringConfiguration
        {
            Name = "chat-missing-llm-output",
            Nodes = new List<ModuleNode>
            {
                new() { ModuleId = "node-input", ModuleName = "ChatInputModule" },
                new() { ModuleId = "node-llm", ModuleName = "LLMModule" },
                new() { ModuleId = "node-output", ModuleName = "ChatOutputModule" }
            },
            Connections = new List<PortConnection>
            {
                new()
                {
                    SourceModuleId = "node-input",
                    SourcePortName = "userMessage",
                    TargetModuleId = "node-llm",
                    TargetPortName = "prompt"
                }
            }
        };

        Assert.False(ChatPipelineConfigurationValidator.IsConfigured(config));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task MissingPipeline_SendDoesNotReachOutputModule()
    {
        var eventBus = new EventBus(NullLogger<EventBus>.Instance);
        var fakeLlm = new CountingFakeLlmService("unused");
        var chatInput = new ChatInputModule(eventBus, NullLogger<ChatInputModule>.Instance);
        var llmModule = new LLMModule(fakeLlm, eventBus, NullLogger<LLMModule>.Instance);
        var chatOutput = new ChatOutputModule(eventBus, NullLogger<ChatOutputModule>.Instance);

        await chatInput.InitializeAsync();
        await llmModule.InitializeAsync();
        await chatOutput.InitializeAsync();

        var received = false;
        chatOutput.OnMessageReceived += _ => received = true;

        await chatInput.SendMessageAsync("hello");
        await Task.Delay(150);

        Assert.False(received);
        Assert.Equal(0, fakeLlm.CallCount);

        await chatInput.ShutdownAsync();
        await llmModule.ShutdownAsync();
        await chatOutput.ShutdownAsync();
    }

    private sealed class CountingFakeLlmService : ILLMService
    {
        private readonly string _response;

        public int CallCount { get; private set; }

        public CountingFakeLlmService(string response)
        {
            _response = response;
        }

        public Task<LLMResult> CompleteAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new LLMResult(true, _response, null));
        }

        public IAsyncEnumerable<string> StreamAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<StreamingResult> StreamWithUsageAsync(IReadOnlyList<ChatMessageInput> messages, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
