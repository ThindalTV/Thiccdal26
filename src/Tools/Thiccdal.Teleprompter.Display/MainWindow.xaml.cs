namespace Thiccdal.Teleprompter.Display;

using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public async Task InitializeAsync(string url)
    {
        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.Navigate(url);
    }

    public void PositionOnMonitor(Rect monitorBounds)
    {
        WindowState = WindowState.Normal;
        Left = monitorBounds.Left;
        Top = monitorBounds.Top;
        Width = monitorBounds.Width;
        Height = monitorBounds.Height;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
