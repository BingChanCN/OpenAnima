using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Localization;
using OpenAnima.Core.Anima;
using OpenAnima.Core.Resources;
using OpenAnima.Core.Services;

namespace OpenAnima.Core.Components.Pages;

public partial class Monitor : IAsyncDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IAnimaContext AnimaContext { get; set; } = default!;
    [Inject] private LanguageService LangSvc { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResources> L { get; set; } = default!;

    private HubConnection? hubConnection;
    private HubConnectionState connectionState = HubConnectionState.Disconnected;
    private bool isRunning;
    private long tickCount;
    private double latencyMs;
    private List<double> latencyHistory = new();
    private const int MaxHistoryPoints = 60;
    private bool _disposed;
    private int _ticksSinceLastRender;
    private const int RenderEveryNTicks = 5;

    private string LatencyClass => latencyMs < 50
        ? "latency-normal"
        : latencyMs < 100
            ? "latency-caution"
            : "latency-warning";

    private string ConnectionClass => connectionState switch
    {
        HubConnectionState.Connected => "connected",
        HubConnectionState.Reconnecting => "reconnecting",
        _ => "disconnected"
    };

    private string ConnectionTitle => connectionState switch
    {
        HubConnectionState.Connected => L["Monitor.Connected"].Value,
        HubConnectionState.Reconnecting => L["Monitor.Reconnecting"].Value,
        HubConnectionState.Disconnected => L["Monitor.Disconnected"].Value,
        _ => connectionState.ToString()
    };

    protected override async Task OnInitializedAsync()
    {
        LangSvc.LanguageChanged += OnLanguageChanged;

        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/runtime"))
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<string, long, double>("ReceiveHeartbeatTick", OnHeartbeatTick);
        hubConnection.On<string, bool>("ReceiveHeartbeatStateChanged", OnHeartbeatStateChanged);
        hubConnection.On<string, int>("ReceiveModuleCountChanged", OnModuleCountChanged);

        hubConnection.Reconnecting += OnReconnecting;
        hubConnection.Reconnected += OnReconnected;
        hubConnection.Closed += OnClosed;

        try
        {
            await hubConnection.StartAsync();
            connectionState = HubConnectionState.Connected;
        }
        catch
        {
            connectionState = HubConnectionState.Disconnected;
        }
    }

    private void OnHeartbeatTick(string animaId, long tick, double latency)
    {
        if (animaId != AnimaContext.ActiveAnimaId) return;
        tickCount = tick;
        latencyMs = latency;
        latencyHistory.Add(latency);
        if (latencyHistory.Count > MaxHistoryPoints)
            latencyHistory.RemoveAt(0);

        _ticksSinceLastRender++;
        if (_ticksSinceLastRender >= RenderEveryNTicks)
        {
            _ticksSinceLastRender = 0;
            if (!_disposed)
                InvokeAsync(StateHasChanged);
        }
    }

    private void OnHeartbeatStateChanged(string animaId, bool running)
    {
        if (animaId != AnimaContext.ActiveAnimaId) return;
        isRunning = running;
        if (!_disposed)
            InvokeAsync(StateHasChanged);
    }

    private void OnModuleCountChanged(string animaId, int count)
    {
        // Module count tracked for future use
    }

    private Task OnReconnecting(Exception? error)
    {
        connectionState = HubConnectionState.Reconnecting;
        if (!_disposed)
            InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    private Task OnReconnected(string? connectionId)
    {
        connectionState = HubConnectionState.Connected;
        if (!_disposed)
            InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    private Task OnClosed(Exception? error)
    {
        connectionState = HubConnectionState.Disconnected;
        if (!_disposed)
            InvokeAsync(StateHasChanged);
        return Task.CompletedTask;
    }

    private void OnLanguageChanged()
    {
        if (!_disposed)
            InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        LangSvc.LanguageChanged -= OnLanguageChanged;
        if (hubConnection is not null)
            await hubConnection.DisposeAsync();
    }
}
