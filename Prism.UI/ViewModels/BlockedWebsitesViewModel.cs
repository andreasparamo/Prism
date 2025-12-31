using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace Prism.UI.ViewModels;

public partial class BlockedWebsitesViewModel : ObservableObject
{
    private readonly Action _navigateBack;

    [ObservableProperty]
    private string _newWebsiteUrl = string.Empty;

    private readonly Prism.Persistence.Services.DatabaseService? _databaseService;

    public ObservableCollection<string> BlockedWebsites { get; } = new();

    public BlockedWebsitesViewModel(Action navigateBack, Prism.Persistence.Services.DatabaseService? databaseService = null)
    {
        _navigateBack = navigateBack;
        _databaseService = databaseService;

        LoadData();
    }

    private void LoadData()
    {
        BlockedWebsites.Clear();
        if (_databaseService != null)
        {
            var sites = _databaseService.GetBlockedWebsites();
            foreach (var site in sites)
            {
                BlockedWebsites.Add(site);
            }
        }
        else
        {
            // Sample data for fallback
            BlockedWebsites.Add("facebook.com");
            BlockedWebsites.Add("twitter.com");
            BlockedWebsites.Add("instagram.com");
        }
    }

    [RelayCommand]
    private void NavigateBack() => _navigateBack();

    [RelayCommand]
    private void AddWebsite()
    {
        if (!string.IsNullOrWhiteSpace(NewWebsiteUrl))
        {
            if (!BlockedWebsites.Contains(NewWebsiteUrl))
            {
                BlockedWebsites.Add(NewWebsiteUrl);
                _databaseService?.AddBlockedWebsite(NewWebsiteUrl);
            }
            NewWebsiteUrl = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveWebsite(string website)
    {
        if (BlockedWebsites.Contains(website))
        {
            BlockedWebsites.Remove(website);
            _databaseService?.RemoveBlockedWebsite(website);
        }
    }
}
