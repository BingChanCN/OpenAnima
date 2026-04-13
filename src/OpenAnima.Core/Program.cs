using System.Diagnostics;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Http.Resilience;
using OpenAnima.Contracts;
using OpenAnima.Core.Events;
using OpenAnima.Core.Hosting;
using OpenAnima.Core.Plugins;
using OpenAnima.Core.Hubs;
using Microsoft.AspNetCore.SignalR;
using OpenAnima.Core.Services;
using OpenAnima.Core.LLM;
using OpenAI;
using OpenAI.Responses;
using Microsoft.Extensions.Options;
using System.ClientModel;
using OpenAnima.Core.DependencyInjection;

// OpenAnima Core Runtime — Blazor Server web host

var builder = WebApplication.CreateBuilder(args);

// --- Register core runtime components as singletons ---
builder.Services.AddSingleton<PluginRegistry>();
builder.Services.AddSingleton<PluginLoader>();
// Global EventBus for singleton modules (ChatInputModule, ChatOutputModule, etc.)
// Note: WiringEngine and HeartbeatLoop use per-Anima EventBus inside AnimaRuntime.
// ANIMA-08: module instances are shared across Animas — per-Anima EventBus wiring is a future phase.
builder.Services.AddSingleton<EventBus>();
builder.Services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());

// --- Register service facades ---
builder.Services.AddSingleton<IModuleService, ModuleService>();

// --- Register LLM services ---
builder.Services.AddOptions<LLMOptions>()
    .Bind(builder.Configuration.GetSection(LLMOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<ResponsesClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
    var clientOptions = new OpenAIClientOptions
    {
        Endpoint = new Uri(options.Endpoint)
    };
    return new ResponsesClient(
        model: options.Model,
        credential: new ApiKeyCredential(options.ApiKey),
        options: clientOptions);
});

builder.Services.AddSingleton<ILLMService, LLMService>();

// --- Register token counting and context management ---
builder.Services.AddSingleton<TokenCounter>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LLMOptions>>().Value;
    return new TokenCounter(options.Model);
});

builder.Services.AddSingleton<ChatContextManager>();
builder.Services.AddScoped<ChatSessionState>();
builder.Services.AddScoped<ChatBackgroundExecutionService>();

// --- Register Anima services ---
builder.Services.AddAnimaServices();

// --- Register LLM provider registry ---
builder.Services.AddProviderServices();

// --- Register wiring services ---
builder.Services.AddWiringServices();

// --- Register run services ---
builder.Services.AddRunServices();

// --- Register workspace tool services ---
builder.Services.AddToolServices();

// --- Register named HTTP client with resilience pipeline ---
builder.Services.AddHttpClient("HttpRequest")
    .AddStandardResilienceHandler();

// --- Register hosted service for runtime lifecycle ---
builder.Services.AddHostedService<AnimaInitializationService>();
builder.Services.AddHostedService<OpenAnimaHostedService>();

// --- Register i18n services ---
builder.Services.AddLocalization();
builder.Services.AddSingleton<LanguageService>();

// --- Add Blazor Server ---
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddServerSideBlazor()
    .AddCircuitOptions(options =>
    {
        options.DisconnectedCircuitMaxRetained = 100;
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    });

// --- Add SignalR for real-time push ---
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

// --- Configure middleware pipeline ---
var supportedCultures = new[] { "zh-CN", "en-US" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("zh-CN")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(localizationOptions);

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<OpenAnima.Core.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapHub<RuntimeHub>("/hubs/runtime");

// --- Ensure Kestrel has a URL to bind ---
StartupUrlResolver.EnsureDefaultUrl(app.Urls, app.Configuration);

// --- Browser auto-launch ---
var noBrowser = args.Contains("--no-browser");
var url = StartupUrlResolver.ResolveDisplayUrl(app.Urls, app.Configuration);

app.Lifetime.ApplicationStarted.Register(() =>
{
    // Resolve the actual URL after Kestrel binds
    var addresses = app.Services
        .GetRequiredService<IServer>()
        .Features
        .Get<IServerAddressesFeature>();
    var listenUrl = addresses?.Addresses.FirstOrDefault() ?? url;

    Console.WriteLine();
    Console.WriteLine($"  OpenAnima Dashboard: {listenUrl}");
    Console.WriteLine("  Press Ctrl+C to stop.");
    Console.WriteLine();

    if (!noBrowser)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = listenUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"  Could not open browser: {ex.Message}");
        }
    }
});

app.Run();
