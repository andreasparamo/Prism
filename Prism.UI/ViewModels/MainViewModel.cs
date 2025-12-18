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

    [ObservableProperty]
    private object _currentViewModel;

    [ObservableProperty]
    private bool _showSidebar = true;

    public MainViewModel()
    {
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
                () => NavigateBlockedApplications());
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
            () => NavigateBlockedApplications());
    }

    [RelayCommand]
    private void NavigateDashboard() => CurrentViewModel = new DashboardViewModel(
        () => NavigateBlockedWebsites(),
        () => NavigateBlockedApplications());

    [RelayCommand]
    private void NavigateSchedule() => CurrentViewModel = new ScheduleViewModel();

    [RelayCommand]
    private void NavigateStats() => CurrentViewModel = new StatsViewModel();

    [RelayCommand]
    private void NavigateSettings() => CurrentViewModel = new SettingsViewModel();

    [RelayCommand]
    private void NavigateBlockedWebsites() => CurrentViewModel = new BlockedWebsitesViewModel(() => NavigateDashboard());

    [RelayCommand]
    private void NavigateBlockedApplications() => CurrentViewModel = new BlockedApplicationsViewModel(() => NavigateDashboard());
}
