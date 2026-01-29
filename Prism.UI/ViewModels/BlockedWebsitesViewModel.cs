using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Prism.UI.ViewModels;

public partial class BlockedWebsitesViewModel : ObservableObject
{
    private readonly Action _navigateBack;
    private readonly Prism.Persistence.Services.DatabaseService? _databaseService;
    
    private static readonly string HostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), 
        @"drivers\etc\hosts"
    );

    [ObservableProperty]
    private string _newWebsiteUrl = string.Empty;

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
    }

    [RelayCommand]
    private void NavigateBack() => _navigateBack();

    [RelayCommand]
    private void AddWebsite()
    {
        if (string.IsNullOrWhiteSpace(NewWebsiteUrl)) return;
        
        if (!BlockedWebsites.Contains(NewWebsiteUrl))
        {
            BlockedWebsites.Add(NewWebsiteUrl);
            _databaseService?.AddBlockedWebsite(NewWebsiteUrl);
            
            // Add to hosts file (append, don't overwrite)
            AddToHostsFile(NewWebsiteUrl);
        }
        
        NewWebsiteUrl = string.Empty;
    }

    [RelayCommand]
    private void RemoveWebsite(string website)
    {
        if (BlockedWebsites.Contains(website))
        {
            BlockedWebsites.Remove(website);
            _databaseService?.RemoveBlockedWebsite(website);
            
            // Remove from hosts file
            RemoveFromHostsFile(website);
        }
    }

    /// <summary>
    /// Adds a single website entry to the hosts file.
    /// </summary>
    private bool AddToHostsFile(string website)
    {
        try
        {
            var domain = NormalizeDomain(website);
            var entry = $"0.0.0.0 {domain} www.{domain}";
            
            // Read existing content
            var existingContent = File.Exists(HostsPath) ? File.ReadAllText(HostsPath) : "";
            
            // Check if entry already exists
            if (existingContent.Contains(domain))
            {
                return true; // Already blocked
            }
            
            // Append new entry
            using (StreamWriter w = File.AppendText(HostsPath))
            {
                w.WriteLine(entry);
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding to hosts: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Removes a single website entry from the hosts file.
    /// </summary>
    private bool RemoveFromHostsFile(string website)
    {
        try
        {
            if (!File.Exists(HostsPath)) return true;
            
            var domain = NormalizeDomain(website);
            
            // Read all lines, filter out the ones containing this domain
            var lines = File.ReadAllLines(HostsPath)
                .Where(line => !line.Contains(domain))
                .ToList();
            
            // Write back
            File.WriteAllLines(HostsPath, lines);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error removing from hosts: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Normalizes a URL to just the domain.
    /// </summary>
    private string NormalizeDomain(string url)
    {
        url = url.Replace("https://", "").Replace("http://", "");
        var slashIndex = url.IndexOf('/');
        if (slashIndex > 0) url = url.Substring(0, slashIndex);
        if (url.StartsWith("www.")) url = url.Substring(4);
        return url.ToLowerInvariant().Trim();
    }
}
