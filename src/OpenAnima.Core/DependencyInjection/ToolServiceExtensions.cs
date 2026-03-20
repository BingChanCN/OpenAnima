using Microsoft.Extensions.DependencyInjection;
using OpenAnima.Core.Modules;
using OpenAnima.Core.Tools;

namespace OpenAnima.Core.DependencyInjection;

/// <summary>
/// Extension methods for registering workspace tool services in the DI container.
/// </summary>
public static class ToolServiceExtensions
{
    /// <summary>
    /// Registers all IWorkspaceTool implementations and the WorkspaceToolModule orchestrator.
    /// </summary>
    public static IServiceCollection AddToolServices(this IServiceCollection services)
    {
        // File tools
        services.AddSingleton<IWorkspaceTool, FileReadTool>();
        services.AddSingleton<IWorkspaceTool, FileWriteTool>();
        services.AddSingleton<IWorkspaceTool, DirectoryListTool>();
        services.AddSingleton<IWorkspaceTool, FileSearchTool>();
        services.AddSingleton<IWorkspaceTool, GrepSearchTool>();

        // Git tools
        services.AddSingleton<IWorkspaceTool, GitStatusTool>();
        services.AddSingleton<IWorkspaceTool, GitDiffTool>();
        services.AddSingleton<IWorkspaceTool, GitLogTool>();
        services.AddSingleton<IWorkspaceTool, GitShowTool>();
        services.AddSingleton<IWorkspaceTool, GitCommitTool>();
        services.AddSingleton<IWorkspaceTool, GitCheckoutTool>();

        // Shell tool
        services.AddSingleton<IWorkspaceTool, ShellExecTool>();

        // Module orchestrator
        services.AddSingleton<WorkspaceToolModule>();

        return services;
    }
}
