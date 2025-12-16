using Prism.Core.Enums;
using Prism.Monitoring;
using Prism.Persistence.Services;
using Prism.UI.Views;
using System.Windows;
using System.Windows.Threading;

namespace Prism.UI;

public partial class App : Application
{
    private MonitorService _monitorService = new();
    private DatabaseService _databaseService = new();
    private BlockWindow? _blockWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Seed Data for Testing
        _databaseService.SetAppCategory("notepad", AppCategory.Distracting); 
        _databaseService.SetAppCategory("msedge", AppCategory.Productive);
        _databaseService.SetAppCategory("chrome", AppCategory.Productive);

        _monitorService.WindowChanged += MonitorService_WindowChanged;
        _monitorService.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _monitorService.Stop();
        base.OnExit(e);
    }

    private void MonitorService_WindowChanged(object? sender, WindowChangedEventArgs e)
    {
        // Log Activity
        _databaseService.LogActivity(e.ProcessName, e.WindowTitle);

        // Check if we need to block
        // In a real app, we check if "Current Time" falls into a "Active Schedule"
        var category = _databaseService.GetAppCategory(e.ProcessName);

        // MOCK: Always block "Distracting" apps for now to demonstrate
        if (category == AppCategory.Distracting)
        {
            Dispatcher.Invoke(() => ShowBlocker());
        }
        else
        {
            Dispatcher.Invoke(() => HideBlocker());
        }
    }

    private void ShowBlocker()
    {
        if (_blockWindow == null || !_blockWindow.IsLoaded)
        {
            _blockWindow = new BlockWindow();
            _blockWindow.Show();
        }
        else
        {
            _blockWindow.Show();
            _blockWindow.Activate();
            _blockWindow.Topmost = true;
        }
    }

    private void HideBlocker()
    {
        _blockWindow?.Hide();
    }
}
