using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Prism.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object _currentViewModel;

    public MainViewModel()
    {
        CurrentViewModel = new DashboardViewModel();
    }

    [RelayCommand]
    private void NavigateDashboard() => CurrentViewModel = new DashboardViewModel();

    [RelayCommand]
    private void NavigateSchedule() => CurrentViewModel = new ScheduleViewModel();

    [RelayCommand]
    private void NavigateStats() => CurrentViewModel = new StatsViewModel();

    [RelayCommand]
    private void NavigateSettings() => CurrentViewModel = new SettingsViewModel();
}
