using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Thiccdal.Infrastructure.Bot.Models;
using Thiccdal.Infrastructure.Remotes;
using Thiccdal.Infrastructure.Twitch;

namespace Thiccdal.Remote.Twitch;

public class TwitchService : ITwitchService, IChatSource, IAsyncDisposable
{
    private readonly TwitchOptions _options;
    private readonly ITwitchTokenManager _tokenManager;
    private readonly ILogger<TwitchService> _logger;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _readCancellation;
    
    public event EventHandler<ChatMessage>? OnChatMessageRecieved;

    public TwitchService(
        IOptions<TwitchOptions> options,
        ITwitchTokenManager tokenManager,
        ILogger<TwitchService> logger)
    {
        _options = options.Value;
        _tokenManager = tokenManager;
        _logger = logger;
    }

    public async Task Connect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Connecting to Twitch channel {Channel}", _options.Channel);
        
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
    }

    public async Task Disconnect(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from Twitch IRC");

        _readCancellation?.Cancel();

        if (_writer != null)
        {
            await _writer.DisposeAsync();
        }

        _reader?.Dispose();
        _client?.Dispose();
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
                    // Split message into parts
                    // :thindal!thindal@thindal.tmi.twitch.tv PRIVMSG #thindal :Hello friends
                    // :napalmcodes!napalmcodes@napalmcodes.tmi.twitch.tv PRIVMSG #thindal :something
                    // :{username}!{username}@{username}.tmi.twitch.tv PRIVMSG #{channel} :{message}
                    var parts = line.Split("PRIVMSG");

                    var user = parts[0].Split('!')[0].TrimStart(':');
                    var channel = parts[1].Split(':')[0].Trim();
                    var message = parts[1].Split(':')[1].Trim();


                    // Build ChatMessage
                    var msg = new ChatMessage
                    {
                        Author = user,
                        Channel = channel,
                        Content = message,
                        Source = ChatSource.Twitch
                    };

                    // Invoke recieved
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
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Disconnect();
        GC.SuppressFinalize(this);
    }
}
