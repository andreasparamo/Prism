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

    public ObservableCollection<string> BlockedWebsites { get; } = new();

    public BlockedWebsitesViewModel(Action navigateBack)
    {
        _navigateBack = navigateBack;
        
        // Sample data
        BlockedWebsites.Add("facebook.com");
        BlockedWebsites.Add("twitter.com");
        BlockedWebsites.Add("instagram.com");
    }

    [RelayCommand]
    private void NavigateBack() => _navigateBack();

    [RelayCommand]
    private void AddWebsite()
    {
        if (!string.IsNullOrWhiteSpace(NewWebsiteUrl))
        {
            BlockedWebsites.Add(NewWebsiteUrl);
            NewWebsiteUrl = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveWebsite(string website)
    {
        BlockedWebsites.Remove(website);
    }
}
