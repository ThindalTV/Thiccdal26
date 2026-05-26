using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public class TwitchService : ITwitchService, IChatSource, IAsyncDisposable, IDisposable
{
    private readonly TwitchOptions _options;
    private readonly ITwitchTokenManager _tokenManager;
    private readonly ILogger<TwitchService> _logger;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readCancellation;

    private TwitchConnectionState _connectionState = TwitchConnectionState.NotAuthorized;

    public TwitchConnectionState ConnectionState => _connectionState;

    public event EventHandler<TwitchConnectionState>? ConnectionStateChanged;
    public event EventHandler<ChatEvent>? OnChatMessageRecieved;

    public bool Connected => _connectionState == TwitchConnectionState.Connected;

    public TwitchService(
        IOptions<TwitchOptions> options,
        ITwitchTokenManager tokenManager,
        ILogger<TwitchService> logger)
    {
        _options = options.Value;
        _tokenManager = tokenManager;
        _logger = logger;
    }

    public async Task RefreshConnectionState(CancellationToken cancellationToken = default)
    {
        if (_connectionState == TwitchConnectionState.Connected ||
            _connectionState == TwitchConnectionState.Connecting)
        {
            return;
        }

        var hasToken = await _tokenManager.HasToken(cancellationToken);
        SetState(hasToken ? TwitchConnectionState.Authorized : TwitchConnectionState.NotAuthorized);
    }

    public async Task Connect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to Twitch channel {Channel}", _options.Channel);

        SetState(TwitchConnectionState.Connecting);

        try
        {
            var token = await _tokenManager.GetToken(cancellationToken);

            _client = new TcpClient();
            await _client.ConnectAsync("irc.chat.twitch.tv", 6667, cancellationToken);

            var stream = _client.GetStream();
            _reader = new StreamReader(stream);
            _writer = new StreamWriter(stream) { AutoFlush = true };

            await _writer.WriteLineAsync($"PASS oauth:{token}");
            await _writer.WriteLineAsync($"NICK {_options.Username}");
            await _writer.WriteLineAsync($"JOIN #{_options.Channel}");

            _logger.LogInformation("Connected to Twitch channel {Channel}", _options.Channel);

            _readCancellation = new CancellationTokenSource();
            _ = Task.Run(() => ReadMessages(_readCancellation.Token), _readCancellation.Token);

            SetState(TwitchConnectionState.Connected);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Twitch");
            SetState(TwitchConnectionState.Error);
            throw;
        }
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from Twitch IRC");

        _readCancellation?.Cancel();

        if (_writer != null)
        {
            await _writer.DisposeAsync();
            _writer = null;
        }

        _reader?.Dispose();
        _reader = null;

        _client?.Dispose();
        _client = null;

        SetState(TwitchConnectionState.Disconnected);
    }

    private async Task ReadMessages(CancellationToken cancellationToken)
    {
        if (_reader == null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null) break;

                _logger.LogDebug("Received: {Line}", line);

                if (line.StartsWith("PING"))
                {
                    await _writer!.WriteLineAsync($"PONG {line.Split(' ')[1]}");
                }
                else if (line.Contains("PRIVMSG"))
                {
                    // :{username}!{username}@{username}.tmi.twitch.tv PRIVMSG #{channel} :{message}
                    var parts = line.Split("PRIVMSG");

                    var user = parts[0].Split('!')[0].TrimStart(':');
                    var channel = parts[1].Split(':')[0].Trim();
                    var message = parts[1].Split(':')[1].Trim();

                    var msg = new ChatEvent
                    {
                        Author = user,
                        Channel = channel,
                        Content = message,
                        Source = PlatformEventSource.Twitch
                    };

                    OnChatMessageRecieved?.Invoke(this, msg);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Stopped reading Twitch messages due to cancellation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Twitch IRC messages");
            SetState(TwitchConnectionState.Error);
        }
    }

    public async Task SendMessage(string message, CancellationToken cancellationToken = default)
    {
        if (!Connected || _writer == null) return;
        await _writer.WriteLineAsync(new StringBuilder($"PRIVMSG #{_options.Channel} :{message}"), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await Disconnect();
        GC.SuppressFinalize(this);
    }

    public async void Dispose()
    {
        GC.SuppressFinalize(this);
        await DisposeAsync();
    }

    private void SetState(TwitchConnectionState state)
    {
        if (_connectionState == state) return;
        _connectionState = state;
        _logger.LogInformation("Twitch connection state: {State}", state);
        ConnectionStateChanged?.Invoke(this, state);
    }
}