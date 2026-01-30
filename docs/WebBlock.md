## Simplified Implementation (Lab Blocker Pattern)

> Based on the [Lab_website_blockerCSharp](https://github.com/ihu5/Lab_website_blockerCSharp) project.

### Key Concepts

The blocking logic is straightforward:
1. **Block** = Write entries to `C:\Windows\System32\drivers\etc\hosts`
2. **Unblock** = Clear or remove entries from the hosts file
3. Format: `0.0.0.0 domain.com www.domain.com`

### Step-by-Step Implementation for BlockedWebsitesViewModel

#### Step 1: Add Helper Methods

Add these methods directly to `BlockedWebsitesViewModel.cs`:

```csharp
private static readonly string HostsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.System), 
    @"drivers\etc\hosts"
);

/// <summary>
/// Writes all blocked websites to the hosts file.
/// </summary>
private bool WriteBlockedSitesToHostsFile()
{
    try
    {
        // Build the hosts file content
        var sb = new StringBuilder();
        foreach (var site in BlockedWebsites)
        {
            // Normalize: remove http://, https://, trailing slashes
            var domain = NormalizeDomain(site);
            sb.AppendLine($"0.0.0.0 {domain} www.{domain}");
        }
        
        // Write to hosts file (requires admin)
        File.WriteAllText(HostsPath, sb.ToString());
        return true;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error writing hosts: {ex.Message}");
        return false;
    }
}

/// <summary>
/// Clears all entries from the hosts file.
/// </summary>
private bool ClearHostsFile()
{
    try
    {
        File.WriteAllText(HostsPath, string.Empty);
        return true;
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error clearing hosts: {ex.Message}");
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
```

#### Step 2: Modify AddWebsite Command

Update the `AddWebsite` method to also write to hosts:

```csharp
[RelayCommand]
private void AddWebsite()
{
    if (string.IsNullOrWhiteSpace(NewWebsiteUrl)) return;
    
    if (!BlockedWebsites.Contains(NewWebsiteUrl))
    {
        // 1. Add to UI
        BlockedWebsites.Add(NewWebsiteUrl);
        
        // 2. Save to database
        _databaseService?.AddBlockedWebsite(NewWebsiteUrl);
        
        // 3. Write ALL blocked sites to hosts file
        WriteBlockedSitesToHostsFile();
    }
    
    NewWebsiteUrl = string.Empty;
}
```

#### Step 3: Modify RemoveWebsite Command

Update `RemoveWebsite` to rewrite hosts file:

```csharp
[RelayCommand]
private void RemoveWebsite(string website)
{
    if (BlockedWebsites.Contains(website))
    {
        // 1. Remove from UI
        BlockedWebsites.Remove(website);
        
        // 2. Remove from database
        _databaseService?.RemoveBlockedWebsite(website);
        
        // 3. Rewrite hosts file with remaining sites
        if (BlockedWebsites.Count > 0)
            WriteBlockedSitesToHostsFile();
        else
            ClearHostsFile();
    }
}
```

#### Step 4: Add Required Usings

```csharp
using System.IO;
using System.Text;
```

### Complete Modified ViewModel

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

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
            WriteBlockedSitesToHostsFile();
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
            
            if (BlockedWebsites.Count > 0)
                WriteBlockedSitesToHostsFile();
            else
                ClearHostsFile();
        }
    }

    private bool WriteBlockedSitesToHostsFile()
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var site in BlockedWebsites)
            {
                var domain = NormalizeDomain(site);
                sb.AppendLine($"0.0.0.0 {domain} www.{domain}");
            }
            File.WriteAllText(HostsPath, sb.ToString());
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return false;
        }
    }

    private bool ClearHostsFile()
    {
        try
        {
            File.WriteAllText(HostsPath, string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return false;
        }
    }

    private string NormalizeDomain(string url)
    {
        url = url.Replace("https://", "").Replace("http://", "");
        var slashIndex = url.IndexOf('/');
        if (slashIndex > 0) url = url.Substring(0, slashIndex);
        if (url.StartsWith("www.")) url = url.Substring(4);
        return url.ToLowerInvariant().Trim();
    }
}
```

> [!WARNING]
> This approach **overwrites the entire hosts file** each time. If you have other entries you want to keep, use the marker-based approach in `WebsiteBlockingService.cs` instead.

> [!IMPORTANT]
> The application must be **run as Administrator** for this to work.

