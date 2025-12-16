using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace Prism.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private int _focusScore;

    [ObservableProperty]
    private string _focusMessage;

    public ObservableCollection<string> RecentPickups { get; } = new();

    public DashboardViewModel()
    {
        FocusScore = 84;
        FocusMessage = "Excellent Focus";
        RecentPickups.Add("Chrome");
        RecentPickups.Add("Visual Studio");
        RecentPickups.Add("Slack");
    }
}
