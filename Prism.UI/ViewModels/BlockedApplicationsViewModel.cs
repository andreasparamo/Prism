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

    private readonly Prism.Persistence.Services.DatabaseService? _databaseService;

    public ObservableCollection<string> BlockedApplications { get; } = new();

    public BlockedApplicationsViewModel(Action navigateBack, Prism.Persistence.Services.DatabaseService? databaseService = null)
    {
        _navigateBack = navigateBack;
        _databaseService = databaseService;

        LoadData();
    }

    private void LoadData()
    {
        BlockedApplications.Clear();
        if (_databaseService != null)
        {
            var apps = _databaseService.GetBlockedApplications();
            foreach (var app in apps)
            {
                BlockedApplications.Add(app);
            }
        }
        else
        {
            // Fallback for design-time or no service
            BlockedApplications.Add("Steam");
            BlockedApplications.Add("Discord");
            BlockedApplications.Add("Spotify");
        }
    }

    [RelayCommand]
    private void NavigateBack() => _navigateBack();

    [RelayCommand]
    private void AddApplication()
    {
        if (!string.IsNullOrWhiteSpace(NewApplicationName))
        {
            if (!BlockedApplications.Contains(NewApplicationName))
            {
                BlockedApplications.Add(NewApplicationName);
                _databaseService?.AddBlockedApplication(NewApplicationName);
            }
            NewApplicationName = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveApplication(string application)
    {
        if (BlockedApplications.Contains(application))
        {
            BlockedApplications.Remove(application);
            _databaseService?.RemoveBlockedApplication(application);
        }
    }
}
