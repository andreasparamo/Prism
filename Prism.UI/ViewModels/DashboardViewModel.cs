using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace Prism.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly Action? _navigateToBlockedWebsites;
    private readonly Action? _navigateToBlockedApplications;

    [ObservableProperty]
    private int _focusScore;

    [ObservableProperty]
    private string _focusMessage = string.Empty;

    public ObservableCollection<string> RecentPickups { get; } = new();

    public DashboardViewModel() : this(null, null, null)
    {
    }

    public DashboardViewModel(Action? navigateToBlockedWebsites, Action? navigateToBlockedApplications, Prism.Persistence.Services.DatabaseService? databaseService)
    {
        _navigateToBlockedWebsites = navigateToBlockedWebsites;
        _navigateToBlockedApplications = navigateToBlockedApplications;

        FocusScore = 84;
        FocusMessage = "Excellent Focus";
        RecentPickups.Add("Chrome");
        RecentPickups.Add("Visual Studio");
        RecentPickups.Add("Slack");
    }

    [RelayCommand]
    private void NavigateToBlockedWebsites() => _navigateToBlockedWebsites?.Invoke();

    [RelayCommand]
    private void NavigateToBlockedApplications() => _navigateToBlockedApplications?.Invoke();
}
