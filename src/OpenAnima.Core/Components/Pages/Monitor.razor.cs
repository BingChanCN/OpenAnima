using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace OpenAnima.Core.Components.Pages;

public partial class Monitor : IAsyncDisposable
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

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
        HubConnectionState.Connected => "Connected",
        HubConnectionState.Reconnecting => "Reconnecting...",
        HubConnectionState.Disconnected => "Disconnected",
        _ => connectionState.ToString()
    };

    protected override async Task OnInitializedAsync()
    {
        hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/runtime"))
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<long, double>("ReceiveHeartbeatTick", OnHeartbeatTick);
        hubConnection.On<bool>("ReceiveHeartbeatStateChanged", OnHeartbeatStateChanged);
        hubConnection.On<int>("ReceiveModuleCountChanged", OnModuleCountChanged);

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

    private void OnHeartbeatTick(long tick, double latency)
    {
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

    private void OnHeartbeatStateChanged(bool running)
    {
        isRunning = running;
        if (!_disposed)
            InvokeAsync(StateHasChanged);
    }

    private void OnModuleCountChanged(int count)
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

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (hubConnection is not null)
            await hubConnection.DisposeAsync();
    }
}
