namespace Thiccdal.Teleprompter.Display;

using System.Drawing;
using System.IO;
using System.Windows.Forms;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _startMenuItem;
    private readonly ToolStripMenuItem _stopMenuItem;
    private bool _disposed;

    public event EventHandler? StartRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? ConfigureRequested;
    public event EventHandler? QuitRequested;

    public TrayIcon()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "Teleprompter Display - Stopped"
        };

        _startMenuItem = new ToolStripMenuItem("Start");
        _startMenuItem.Click += (_, _) => StartRequested?.Invoke(this, EventArgs.Empty);

        _stopMenuItem = new ToolStripMenuItem("Stop");
        _stopMenuItem.Click += (_, _) => StopRequested?.Invoke(this, EventArgs.Empty);

        var configureMenuItem = new ToolStripMenuItem("Configure...");
        configureMenuItem.Click += (_, _) => ConfigureRequested?.Invoke(this, EventArgs.Empty);

        var quitMenuItem = new ToolStripMenuItem("Quit");
        quitMenuItem.Click += (_, _) => QuitRequested?.Invoke(this, EventArgs.Empty);

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add(_startMenuItem);
        contextMenu.Items.Add(_stopMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(configureMenuItem);
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add(quitMenuItem);

        _notifyIcon.ContextMenuStrip = contextMenu;

        UpdateStatus(false);
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    public void UpdateStatus(bool isRunning)
    {
        _notifyIcon.Text = isRunning
            ? "Teleprompter Display - Running"
            : "Teleprompter Display - Stopped";

        _startMenuItem.Enabled = !isRunning;
        _stopMenuItem.Enabled = isRunning;
    }

    public void ShowBalloon(string title, string message)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, ToolTipIcon.Info);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Dispose();
        _disposed = true;
    }

    private static Icon LoadIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "icon.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        return SystemIcons.Application;
    }
}
