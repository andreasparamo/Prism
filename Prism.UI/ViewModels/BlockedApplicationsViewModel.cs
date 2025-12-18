using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace Prism.UI.ViewModels;

public partial class BlockedApplicationsViewModel : ObservableObject
{
    private readonly Action _navigateBack;

    [ObservableProperty]
    private string _newApplicationName = string.Empty;

    public ObservableCollection<string> BlockedApplications { get; } = new();

    public BlockedApplicationsViewModel(Action navigateBack)
    {
        _navigateBack = navigateBack;
        
        // Sample data
        BlockedApplications.Add("Steam");
        BlockedApplications.Add("Discord");
        BlockedApplications.Add("Spotify");
    }

    [RelayCommand]
    private void NavigateBack() => _navigateBack();

    [RelayCommand]
    private void AddApplication()
    {
        if (!string.IsNullOrWhiteSpace(NewApplicationName))
        {
            BlockedApplications.Add(NewApplicationName);
            NewApplicationName = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveApplication(string application)
    {
        BlockedApplications.Remove(application);
    }
}
