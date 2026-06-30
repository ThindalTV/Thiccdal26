namespace Thiccdal.Teleprompter.Display;

using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

public partial class App : Application
{
    private const int HotkeyIdToggle = 1;

    private DisplayConfig _config = new();
    private TrayIcon? _trayIcon;
    private MainWindow? _mainWindow;
    private MouseBlocker? _mouseBlocker;
    private HotkeyManager? _hotkeyManager;
    private ObsWebSocketClient? _obsClient;
    private bool _isDisplayRunning;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!MonitorHelper.HasMultipleMonitors())
        {
            MessageBox.Show(
                "Teleprompter Display requires multiple monitors. Please connect an additional display and try again.",
                "Multiple Monitors Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        LoadConfiguration();
        InitializeTrayIcon();
        InitializeMainWindow();
        InitializeMouseBlocker();
        await InitializeObsClientAsync();

        _trayIcon?.Show();
        _trayIcon?.ShowBalloon("Teleprompter Display", "Application started. Right-click the tray icon to control.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopDisplay();
        _hotkeyManager?.Dispose();
        _obsClient?.Dispose();
        _mouseBlocker?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }

    private void LoadConfiguration()
    {
        var configPath = DisplayConfig.GetDefaultPath();
        _config = DisplayConfig.Load(configPath);
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new TrayIcon();
        _trayIcon.StartRequested += (_, _) => StartDisplay();
        _trayIcon.StopRequested += (_, _) => StopDisplay();
        _trayIcon.ConfigureRequested += (_, _) => OpenConfigureDialog();
        _trayIcon.QuitRequested += (_, _) => Shutdown();
    }

    private void InitializeMainWindow()
    {
        _mainWindow = new MainWindow();

        var helper = new WindowInteropHelper(_mainWindow);
        helper.EnsureHandle();

        _hotkeyManager = new HotkeyManager(helper.Handle);
        _hotkeyManager.Register(HotkeyIdToggle, _config.Hotkeys.ToggleDisplay, ToggleDisplay);

        var source = HwndSource.FromHwnd(helper.Handle);
        source?.AddHook(_hotkeyManager.ProcessMessage);
    }

    private void InitializeMouseBlocker()
    {
        _mouseBlocker = new MouseBlocker();
    }

    private async Task InitializeObsClientAsync()
    {
        if (!_config.Obs.Enabled)
        {
            return;
        }

        _obsClient = new ObsWebSocketClient();
        _obsClient.StreamStarted += OnObsStreamStarted;
        _obsClient.StreamStopped += OnObsStreamStopped;
        _obsClient.Connected += (_, _) => _trayIcon?.ShowBalloon("OBS Connected", "Connected to OBS WebSocket");
        _obsClient.Disconnected += (_, _) => { };

        try
        {
            var password = string.IsNullOrEmpty(_config.Obs.Password) ? null : _config.Obs.Password;
            await _obsClient.ConnectAsync(_config.Obs.Host, _config.Obs.Port, password, CancellationToken.None);
        }
        catch
        {
            _trayIcon?.ShowBalloon("OBS Connection Failed", "Could not connect to OBS. Auto-start disabled.");
        }
    }

    private void OnObsStreamStarted(object? sender, EventArgs e)
    {
        if (_config.Obs.AutoStartOnStream && !_isDisplayRunning)
        {
            Dispatcher.Invoke(StartDisplay);
        }
    }

    private void OnObsStreamStopped(object? sender, EventArgs e)
    {
        if (_config.Obs.AutoStopOnStreamEnd && _isDisplayRunning)
        {
            Dispatcher.Invoke(StopDisplay);
        }
    }

    private void ToggleDisplay()
    {
        if (_isDisplayRunning)
        {
            StopDisplay();
        }
        else
        {
            StartDisplay();
        }
    }

    private async void StartDisplay()
    {
        if (_isDisplayRunning || _mainWindow is null)
        {
            return;
        }

        var monitorBounds = MonitorHelper.GetMonitorBounds(_config.MonitorIndex);
        _mainWindow.PositionOnMonitor(monitorBounds);

        var url = $"{_config.ServerUrl.TrimEnd('/')}{_config.ViewPath}";
        await _mainWindow.InitializeAsync(url);

        _mainWindow.Show();
        _isDisplayRunning = true;
        _trayIcon?.UpdateStatus(true);

        if (_config.BlockMouse)
        {
            _mouseBlocker?.Start(monitorBounds);
        }
    }

    private void StopDisplay()
    {
        if (!_isDisplayRunning || _mainWindow is null)
        {
            return;
        }

        _mouseBlocker?.Stop();
        _mainWindow.Hide();
        _isDisplayRunning = false;
        _trayIcon?.UpdateStatus(false);
    }

    private void OpenConfigureDialog()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Configuration File",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = AppContext.BaseDirectory,
            FileName = "displayconfig.json"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var wasRunning = _isDisplayRunning;
            if (wasRunning)
            {
                StopDisplay();
            }

            _config = DisplayConfig.Load(dialog.FileName);

            _hotkeyManager?.UnregisterAll();
            _hotkeyManager?.Register(HotkeyIdToggle, _config.Hotkeys.ToggleDisplay, ToggleDisplay);

            _trayIcon?.ShowBalloon("Configuration Loaded", $"Loaded: {dialog.FileName}");

            if (wasRunning)
            {
                StartDisplay();
            }
        }
    }
}
