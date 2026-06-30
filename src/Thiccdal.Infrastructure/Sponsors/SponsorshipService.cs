using System.Timers;
using Microsoft.Extensions.Logging;

namespace Thiccdal.Infrastructure.Sponsors;

public sealed class SponsorshipService : ISponsorshipService, IDisposable
{
    private readonly ILogger<SponsorshipService> _logger;
    private readonly object _lock = new object();
    private System.Timers.Timer? _timer;
    private SponsorConfig? _config;
    private SponsorReadState _state = SponsorReadState.Idle;
    private DateTimeOffset? _nextReadAt;

    public SponsorshipService(ILogger<SponsorshipService> logger)
    {
        _logger = logger;
    }

    public SponsorConfig? Config
    {
        get { lock (_lock) { return _config; } }
    }

    public SponsorReadState ReadState
    {
        get { lock (_lock) { return _state; } }
    }

    public DateTimeOffset? NextReadAt
    {
        get { lock (_lock) { return _nextReadAt; } }
    }

    public event EventHandler? StateChanged;

    public void Configure(SponsorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_lock)
        {
            _config = config;
            StopTimer();
            _state = SponsorReadState.Idle;
            _nextReadAt = null;
            if (config.HasSponsor && config.ReadIntervalMinutes > 0)
            {
                StartTimer(config.ReadIntervalMinutes);
            }
        }
        _logger.LogInformation("Sponsorship configured. HasSponsor={HasSponsor}, IntervalMinutes={Interval}",
            config.HasSponsor, config.ReadIntervalMinutes);
        OnStateChanged();
    }

    public void StartRead()
    {
        lock (_lock)
        {
            _state = SponsorReadState.ReadActive;
            // Pause the timer while reading
            _timer?.Stop();
        }
        _logger.LogInformation("Sponsor read started");
        OnStateChanged();
    }

    public void EndRead()
    {
        lock (_lock)
        {
            _state = SponsorReadState.Idle;
            StopTimer();
            if (_config?.HasSponsor == true && _config.ReadIntervalMinutes > 0)
            {
                StartTimer(_config.ReadIntervalMinutes);
            }
        }
        _logger.LogInformation("Sponsor read ended; timer restarted");
        OnStateChanged();
    }

    public void SkipRead()
    {
        lock (_lock)
        {
            _state = SponsorReadState.Idle;
            StopTimer();
            if (_config?.HasSponsor == true && _config.ReadIntervalMinutes > 0)
            {
                StartTimer(_config.ReadIntervalMinutes);
            }
        }
        _logger.LogInformation("Sponsor read skipped; timer restarted");
        OnStateChanged();
    }

    public void Dispose()
    {
        lock (_lock) { StopTimer(); }
    }

    private void StartTimer(int intervalMinutes)
    {
        // Must be called inside _lock
        _nextReadAt = DateTimeOffset.UtcNow.AddMinutes(intervalMinutes);
        _timer = new System.Timers.Timer(TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds);
        _timer.AutoReset = false;
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    private void StopTimer()
    {
        // Must be called inside _lock
        if (_timer is null) return;
        _timer.Stop();
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();
        _timer = null;
        _nextReadAt = null;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        lock (_lock)
        {
            _state = SponsorReadState.ReadDue;
            _nextReadAt = null;
        }
        _logger.LogInformation("Sponsor read is due");
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
