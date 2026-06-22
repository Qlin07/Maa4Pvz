using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using Serilog;

namespace MaaWpfGui;

public partial class App : Application
{
    private static readonly ILogger _logger = Log.ForContext<App>();

    public void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Hyperlink link && link.NavigateUri != null)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = link.NavigateUri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
