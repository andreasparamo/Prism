using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;

namespace Prism.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly string WelcomeCompletedFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Prism",
        "welcome_completed");

    private readonly Prism.Persistence.Services.DatabaseService _databaseService;

    [ObservableProperty]
    private object _currentViewModel;

    /// <summary>
    /// Disposes IDisposable ViewModels when navigating away.
    /// This ensures WMI watchers are properly cleaned up.
    /// </summary>
    partial void OnCurrentViewModelChanging(object value)
    {
        if (value is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [ObservableProperty]
    private bool _showSidebar = true;

    public MainViewModel()
    {
        _databaseService = new Prism.Persistence.Services.DatabaseService();

        // Check if user has already seen the welcome page
        if (!HasCompletedWelcome())
        {
            ShowSidebar = false;
            CurrentViewModel = new WelcomeViewModel(OnWelcomeCompleted);
        }
        else
        {
            CurrentViewModel = new DashboardViewModel(
                () => NavigateBlockedWebsites(),
                () => NavigateBlockedApplications(),
                _databaseService);
        }
    }

    private bool HasCompletedWelcome()
    {
        return File.Exists(WelcomeCompletedFile);
    }

    private void MarkWelcomeCompleted()
    {
        try
        {
            var directory = Path.GetDirectoryName(WelcomeCompletedFile);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
            File.WriteAllText(WelcomeCompletedFile, DateTime.Now.ToString());
        }
        catch
        {
            // Ignore errors - welcome will show again next time
        }
    }

    private void OnWelcomeCompleted()
    {
        MarkWelcomeCompleted();
        ShowSidebar = true;
        CurrentViewModel = new DashboardViewModel(
            () => NavigateBlockedWebsites(),
            () => NavigateBlockedApplications(),
            _databaseService);
    }

    [RelayCommand]
    private void NavigateDashboard() => CurrentViewModel = new DashboardViewModel(
        () => NavigateBlockedWebsites(),
        () => NavigateBlockedApplications(),
        _databaseService);

    [RelayCommand]
    private void NavigateSchedule() => CurrentViewModel = new ScheduleViewModel();

    [RelayCommand]
    private void NavigateStats() => CurrentViewModel = new StatsViewModel();

    [RelayCommand]
    private void NavigateSettings() => CurrentViewModel = new SettingsViewModel();

    [RelayCommand]
    private void NavigateBlockedWebsites() => CurrentViewModel = new BlockedWebsitesViewModel(() => NavigateDashboard(), _databaseService);

    [RelayCommand]
    private void NavigateBlockedApplications() => CurrentViewModel = new BlockedApplicationsViewModel(() => NavigateDashboard(), _databaseService);
}
