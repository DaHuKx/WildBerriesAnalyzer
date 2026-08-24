using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WildBerriesAnalyzer.Business.Models;
using WildBerriesAnalyzer.ServerClient.Interfaces;

namespace WildBerriesAnalyzer.ServerClient.Clients;

public sealed class SearchHubClient : ISearchHubClient
{
    private const string HubPath = "hubs/search";

    private readonly IOptions<WbServerClientOptions> _options;
    private readonly IWbAuthTokenStore _tokenStore;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HubConnection? _connection;
    private bool _disposed;

    public SearchHubClient(IOptions<WbServerClientOptions> options, IWbAuthTokenStore tokenStore)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _tokenStore.TokensCleared += OnTokensCleared;
    }

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public event Action<SearchProgress>? ProgressReceived;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureConnection();

            if (_connection!.State == HubConnectionState.Connected)
            {
                return;
            }

            await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopAndDisposeConnectionAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _tokenStore.TokensCleared -= OnTokensCleared;
        await DisconnectAsync().ConfigureAwait(false);
        _gate.Dispose();
        _disposed = true;
    }

    private void EnsureConnection()
    {
        if (_connection is not null)
        {
            return;
        }

        var baseAddress = _options.Value.BaseAddress;
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new InvalidOperationException(
                $"Не задан {nameof(WbServerClientOptions.BaseAddress)} для ServerClient.");
        }

        var hubUri = new Uri(new Uri(EnsureTrailingSlash(baseAddress), UriKind.Absolute), HubPath);
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_tokenStore.AccessToken);
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .Build();

        _connection.On<SearchProgress>(SearchHubEvents.Progress, progress =>
        {
            if (progress is not null)
            {
                ProgressReceived?.Invoke(progress);
            }
        });
    }

    private async Task StopAndDisposeConnectionAsync()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _connection = null;
        }
    }

    private void OnTokensCleared(object? sender, EventArgs e)
    {
        _ = DisconnectAsync();
    }

    private static string EnsureTrailingSlash(string baseAddress) =>
        baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/";
}
