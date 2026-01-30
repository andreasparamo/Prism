# Application Blocking Implementation Plan

> Implementation guide for blocking applications, following the same pattern as website blocking in `BlockedWebsitesViewModel.cs`.

---

## Overview

Application blocking uses the existing `BlockedApplicationsViewModel.cs` structure and extends it with actual blocking enforcement. Unlike website blocking (which modifies the hosts file), application blocking works by:

1. **Subscribing to process creation events** via WMI (Windows Management Instrumentation)
2. **Terminating blocked applications** immediately when they launch
3. **Showing the block overlay** (`BlockWindow`) when a blocked app tries to launch

---

## Required Packages & Libraries

### New NuGet Package (Must Install)

| Package | Version | Purpose | Install Command |
|---------|---------|---------|-----------------|
| `System.Management` | 8.0.0 | WMI process event monitoring | `dotnet add Prism.UI package System.Management` |

### Existing Packages (Already Installed)

These are already in `Prism.UI.csproj` and will be used:

| Package | Version | Purpose |
|---------|---------|---------|
| `CommunityToolkit.Mvvm` | 8.4.0 | `ObservableObject`, `[RelayCommand]`, `[ObservableProperty]` |
| `Microsoft.Data.Sqlite` | 10.0.1 | Database operations (in `Prism.Persistence`) |

### Required Using Statements

```csharp
// New - for WMI process watching
using System.Management;

// New - for thread-safe UI updates
using System.Windows;                // Application.Current.Dispatcher
using System.Windows.Threading;      // Dispatcher

// Existing - already available in .NET
using System;
using System.Collections.Generic;    // List<T> for snapshot
using System.Diagnostics;            // Process class for termination
using System.Collections.ObjectModel;
using System.Linq;                   // .ToList() for snapshot
using System.Threading.Tasks;        // Task, async/await
using System.Text.RegularExpressions; // Regex for wildcard matching

// Existing - from CommunityToolkit.Mvvm
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
```

### .csproj Addition

Add this to `Prism.UI/Prism.UI.csproj` in the `<ItemGroup>` with other packages:

```xml
<PackageReference Include="System.Management" Version="8.0.0" />
```

### System Requirements

| Requirement | Details |
|-------------|---------|
| **Admin Privileges** | Required for WMI `Win32_ProcessStartTrace` and `Process.Kill()` |
| **Windows Version** | Windows 10/11 (WMI is Windows-only) |
| **.NET Version** | 8.0 (already configured in project) |

---

## Configuring Administrator Privileges

> [!IMPORTANT]
> WMI process monitoring and process termination **require administrator privileges**. The app must be configured to request elevation.

### Step 1: Create app.manifest

Create `Prism.UI/app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="MyApplication.app"/>
  
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <!-- Request administrator privileges -->
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>

  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows 10/11 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>

  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </windowsSettings>
  </application>
</assembly>
```

### Step 2: Update Prism.UI.csproj

Add the manifest reference to `Prism.UI/Prism.UI.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    
    <!-- Reference the app.manifest for admin privileges -->
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Management" Version="8.0.0" />
    <!-- ... other packages ... -->
  </ItemGroup>
</Project>
```

### Step 3: Runtime Admin Check (Fallback)

Add a runtime check in `App.xaml.cs` that shows an error if not running as admin:

```csharp
using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace Prism.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Check if running as administrator
        if (!IsRunningAsAdministrator())
        {
            // Option 1: Show error and exit
            MessageBox.Show(
                "Prism requires administrator privileges to block applications.\n\n" +
                "Please right-click the application and select 'Run as administrator'.",
                "Administrator Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
            
            // Option 2: Try to restart elevated (uncomment to use)
            // RestartAsAdmin();
            
            Shutdown(1);
            return;
        }
        
        // Continue with normal startup...
        InitializeMainWindow();
    }

    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    private static bool IsRunningAsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts the application with administrator privileges (UAC prompt).
    /// </summary>
    private void RestartAsAdmin()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
                UseShellExecute = true,
                Verb = "runas"  // Triggers UAC elevation prompt
            };
            
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to restart as admin: {ex.Message}");
            MessageBox.Show(
                "Failed to restart with administrator privileges.\n\n" +
                "Please manually run the application as administrator.",
                "Elevation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void InitializeMainWindow()
    {
        // Your existing MainWindow initialization code
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}
```

### Step 4: Required Using Statements for Admin Check

```csharp
using System.Diagnostics;
using System.Security.Principal;  // WindowsIdentity, WindowsPrincipal
```

### Admin Check Summary

| Method | Description |
|--------|-------------|
| `IsRunningAsAdministrator()` | Returns `true` if running with admin privileges |
| `RestartAsAdmin()` | Restarts the app with `runas` verb (triggers UAC prompt) |

### What Happens at Runtime

```
1. User launches app normally
   └── app.manifest triggers UAC prompt automatically
   
2. If manifest is missing or user bypasses:
   └── App.OnStartup() checks IsRunningAsAdministrator()
       ├── If true: Continue normally
       └── If false: Show error + optional self-elevation
```

> [!TIP]
> The `app.manifest` with `requireAdministrator` is the **primary** method. The runtime check is a **fallback** in case the manifest fails or is removed.

> [!NOTE]
> During development in Visual Studio, you may need to run VS as administrator, or the debugger won't be able to attach to the elevated process.

---

## Current State

### What Exists

| Component | Status | Description |
|-----------|--------|-------------|
| `BlockedApplicationsViewModel.cs` | ✅ Exists | UI logic for managing blocked app list |
| `BlockedApplicationsPage.xaml` | ✅ Exists | UI for adding/removing blocked apps |
| `DatabaseService` methods | ✅ Exists | `GetBlockedApplications()`, `AddBlockedApplication()`, `RemoveBlockedApplication()` |
| **Actual blocking enforcement** | ❌ Missing | No code to terminate/block apps |

### What Needs to Be Added

| Component | Location | Description |
|-----------|----------|-------------|
| WMI process watcher | `Prism.Monitoring` or `App.xaml.cs` | Listen for new process creation events |
| Process termination logic | Same location | Kill blocked processes when detected |
| Blocking enforcement on startup | `App.xaml.cs` | Kill any already-running blocked apps on app start |

---

## Implementation Checklist

> [!IMPORTANT]
> Follow these steps in order to add app blocking to your existing `BlockedApplicationsViewModel.cs`.

### Step-by-Step Implementation

| Step | File | Action |
|------|------|--------|
| 1 | `Prism.UI.csproj` | Add `System.Management` NuGet package |
| 2 | `Prism.UI.csproj` | Add `app.manifest` reference |
| 3 | `Prism.UI/app.manifest` | Create manifest with `requireAdministrator` |
| 4 | `BlockedApplicationsViewModel.cs` | Add using statements |
| 5 | `BlockedApplicationsViewModel.cs` | Add fields and properties |
| 6 | `BlockedApplicationsViewModel.cs` | Update constructor with async init |
| 7 | `BlockedApplicationsViewModel.cs` | Add WMI watcher methods |
| 8 | `BlockedApplicationsViewModel.cs` | Add pattern matching methods |
| 9 | `BlockedApplicationsViewModel.cs` | Implement `IDisposable` |
| 10 | `MainViewModel.cs` | Add dispose logic for navigation |
| 11 | `App.xaml.cs` | Add admin check and OnExit cleanup |
| 12 | Build & Test | Run as admin and verify blocking |

---

### Step 1: Add NuGet Package

```powershell
cd Prism.UI
dotnet add package System.Management --version 8.0.0
```

Or add to `Prism.UI.csproj`:
```xml
<PackageReference Include="System.Management" Version="8.0.0" />
```

---

### Step 2-3: Create app.manifest

See [Configuring Administrator Privileges](#configuring-administrator-privileges) section above.

---

### Step 4: Add Using Statements to BlockedApplicationsViewModel.cs

Add these at the top of the file:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
```

---

### Step 5: Add Fields and Properties

Add these inside the class, before the constructor:

```csharp
private ManagementEventWatcher? _processWatcher;
private readonly Dispatcher _dispatcher;

// Thread-safe snapshot
private List<string> _blockedAppsSnapshot = new();
private readonly object _snapshotLock = new();

[ObservableProperty]
private string _statusMessage = string.Empty;

[ObservableProperty]
[NotifyCanExecuteChangedFor(nameof(AddApplicationCommand))]
private bool _isBlockingActive = false;

[ObservableProperty]
private bool _isInitializing = true;
```

---

### Step 6: Update Constructor

Replace your existing constructor with:

```csharp
public BlockedApplicationsViewModel(Action navigateBack, Prism.Persistence.Services.DatabaseService? databaseService = null)
{
    _navigateBack = navigateBack;
    _databaseService = databaseService;
    _dispatcher = Application.Current.Dispatcher;

    // Start async initialization
    _ = InitializeAsync();
}

private async Task InitializeAsync()
{
    try
    {
        IsInitializing = true;
        SetStatus("Loading blocked applications...");

        await LoadDataAsync();
        UpdateSnapshot();

        if (BlockedApplications.Count > 0)
        {
            SetStatus($"Terminating {BlockedApplications.Count} blocked apps...");
            await Task.Run(() => KillAlreadyRunningBlockedApps());
        }

        StartProcessWatcher();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Initialization error: {ex.Message}");
        SetStatus($"⚠️ Initialization failed: {ex.Message}");
    }
    finally
    {
        IsInitializing = false;
    }
}
```

---

### Step 7: Add WMI Watcher Methods

Add these methods to the class:

```csharp
#region WMI Process Watching

private void StartProcessWatcher()
{
    try
    {
        var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
        _processWatcher = new ManagementEventWatcher(query);
        _processWatcher.EventArrived += OnProcessStarted;
        _processWatcher.Start();
        
        IsBlockingActive = true;
        SetStatus("✅ App blocking active");
    }
    catch (ManagementException ex)
    {
        Debug.WriteLine($"WMI error: {ex.Message}");
        IsBlockingActive = false;
        SetStatus("⚠️ App blocking requires administrator privileges");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Process watcher failed: {ex.Message}");
        IsBlockingActive = false;
        SetStatus($"⚠️ App blocking failed: {ex.Message}");
    }
}

private void OnProcessStarted(object sender, EventArrivedEventArgs e)
{
    try
    {
        var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
        var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
        
        var blockedApps = GetBlockedAppsSnapshot();
        
        foreach (var blockedPattern in blockedApps)
        {
            if (IsProcessBlocked(processName, blockedPattern))
            {
                TerminateProcess(processId);
                SetStatus($"🚫 Blocked: {processName}");
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"OnProcessStarted error: {ex.Message}");
    }
}

private void TerminateProcess(int processId)
{
    try
    {
        using var process = Process.GetProcessById(processId);
        process.Kill();
    }
    catch { }
}

#endregion
```

---

### Step 8: Add Helper Methods

```csharp
#region Thread Safety

private void UpdateSnapshot()
{
    lock (_snapshotLock)
    {
        _blockedAppsSnapshot = BlockedApplications.ToList();
    }
}

private List<string> GetBlockedAppsSnapshot()
{
    lock (_snapshotLock)
    {
        return new List<string>(_blockedAppsSnapshot);
    }
}

private void SetStatus(string message)
{
    if (_dispatcher.CheckAccess())
        StatusMessage = message;
    else
        _dispatcher.Invoke(() => StatusMessage = message);
}

#endregion

#region Pattern Matching

private bool IsProcessBlocked(string processName, string blockedPattern)
{
    var normalizedProcess = NormalizeForComparison(processName);
    var normalizedPattern = NormalizeForComparison(blockedPattern);

    if (normalizedPattern.Contains('*') || normalizedPattern.Contains('?'))
        return MatchesWildcard(normalizedProcess, normalizedPattern);

    return normalizedProcess == normalizedPattern;
}

private string NormalizeForComparison(string name)
{
    name = name.Trim().ToLowerInvariant();
    if (name.EndsWith(".exe"))
        name = name.Substring(0, name.Length - 4);
    return name;
}

private bool MatchesWildcard(string input, string pattern)
{
    var regexPattern = "^" + Regex.Escape(pattern)
        .Replace("\\*", ".*")
        .Replace("\\?", ".") + "$";
    return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
}

#endregion
```

---

### Step 9: Implement IDisposable

Update the class declaration and add Dispose:

```csharp
public partial class BlockedApplicationsViewModel : ObservableObject, IDisposable
{
    // ... existing code ...

    public void Dispose()
    {
        _processWatcher?.Stop();
        _processWatcher?.Dispose();
    }
}
```

---

### Step 10: Update MainViewModel.cs

Add disposal when navigating away:

```csharp
[ObservableProperty]
private object? _currentViewModel;

partial void OnCurrentViewModelChanging(object? oldValue, object? newValue)
{
    if (oldValue is IDisposable disposable)
    {
        disposable.Dispose();
    }
}
```

---

### Step 11: Update App.xaml.cs

Add admin check and cleanup:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    if (!IsRunningAsAdministrator())
    {
        MessageBox.Show(
            "Prism requires administrator privileges to block applications.",
            "Administrator Required",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );
        Shutdown(1);
        return;
    }
    
    // ... existing startup code ...
}

private static bool IsRunningAsAdministrator()
{
    using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
    var principal = new System.Security.Principal.WindowsPrincipal(identity);
    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
}
```

---

### Step 12: Build and Test

```powershell
# Build
dotnet build Prism.UI

# Run as Administrator
# Right-click the .exe and select "Run as administrator"
```

**Test Checklist:**
- [ ] App shows "✅ App blocking active" on startup
- [ ] Adding "notepad" to blocked list terminates running notepad
- [ ] Launching notepad while blocked is immediately killed
- [ ] Status shows "🚫 Blocked: notepad.exe" when blocking occurs
- [ ] Removing from list allows notepad to run again

---

## Efficient Approach: WMI Process Creation Events

Instead of polling every second (inefficient), we use **WMI events** to get notified **only when a process starts**:

```csharp
using System.Management;  // Requires System.Management NuGet package

private ManagementEventWatcher? _processWatcher;

/// <summary>
/// Starts watching for new process creation events.
/// Only triggers when a new process starts - no polling!
/// </summary>
public void StartProcessWatcher()
{
    try
    {
        // WMI query: notify us when ANY new process is created
        var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
        
        _processWatcher = new ManagementEventWatcher(query);
        _processWatcher.EventArrived += OnProcessStarted;
        _processWatcher.Start();
        
        System.Diagnostics.Debug.WriteLine("Process watcher started");
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to start process watcher: {ex.Message}");
    }
}

/// <summary>
/// Called ONLY when a new process starts - efficient, event-driven.
/// </summary>
private void OnProcessStarted(object sender, EventArrivedEventArgs e)
{
    try
    {
        // Get the process name from the event
        var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
        var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
        
        // Check if this process is blocked
        var normalizedName = NormalizeAppName(processName);
        
        foreach (var blocked in BlockedApplications)
        {
            if (NormalizeAppName(blocked) == normalizedName)
            {
                // Kill it immediately
                TerminateProcess(processId);
                System.Diagnostics.Debug.WriteLine($"Blocked: {processName} (PID: {processId})");
                break;
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error handling process start: {ex.Message}");
    }
}

/// <summary>
/// Terminates a process by its ID.
/// </summary>
private void TerminateProcess(int processId)
{
    try
    {
        var process = Process.GetProcessById(processId);
        process.Kill();
        process.Dispose();
    }
    catch (ArgumentException)
    {
        // Process already exited
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Failed to terminate PID {processId}: {ex.Message}");
    }
}

/// <summary>
/// Stops the process watcher.
/// </summary>
public void StopProcessWatcher()
{
    _processWatcher?.Stop();
    _processWatcher?.Dispose();
    _processWatcher = null;
}

/// <summary>
/// Normalizes an application name for comparison.
/// </summary>
private string NormalizeAppName(string appName)
{
    appName = appName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
    var slashIndex = appName.LastIndexOfAny(new[] { '\\', '/' });
    if (slashIndex >= 0)
        appName = appName.Substring(slashIndex + 1);
    return appName.Trim().ToLowerInvariant();
}
```

---

## Step-by-Step Implementation

### Step 1: Add NuGet Package

Add the `System.Management` package to `Prism.UI.csproj`:

```xml
<PackageReference Include="System.Management" Version="8.0.0" />
```

Or via command:
```powershell
dotnet add Prism.UI package System.Management
```

---

### Step 2: Update BlockedApplicationsViewModel (Thread-Safe)

> [!IMPORTANT]
> WMI events fire on a **background thread**. Accessing `ObservableCollection` from a background thread causes `NotSupportedException`. We fix this by:
> 1. Creating a **snapshot copy** of the blocked list for background thread access
> 2. Using `Dispatcher.Invoke` for any UI updates

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Windows;
using System.Windows.Threading;

namespace Prism.UI.ViewModels;

public partial class BlockedApplicationsViewModel : ObservableObject, IDisposable
{
    private readonly Action _navigateBack;
    private readonly Prism.Persistence.Services.DatabaseService? _databaseService;
    private ManagementEventWatcher? _processWatcher;
    private readonly Dispatcher _dispatcher;
    
    // Thread-safe snapshot of blocked apps for background thread access
    private List<string> _blockedAppsSnapshot = new();
    private readonly object _snapshotLock = new();

    [ObservableProperty]
    private string _newApplicationName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Indicates if the WMI process watcher is running.
    /// False if WMI failed to start (e.g., not running as admin).
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddApplicationCommand))]
    private bool _isBlockingActive = false;

    /// <summary>
    /// True if blocking is active and user can modify the list.
    /// Used to disable Add/Remove buttons when blocking isn't working.
    /// </summary>
    public bool CanModifyBlockList => IsBlockingActive;

    public ObservableCollection<string> BlockedApplications { get; } = new();

    public BlockedApplicationsViewModel(Action navigateBack, Prism.Persistence.Services.DatabaseService? databaseService = null)
    {
        _navigateBack = navigateBack;
        _databaseService = databaseService;
        _dispatcher = Application.Current.Dispatcher;

        // Start async initialization (fire-and-forget with error handling)
        _ = InitializeAsync();
    }

    /// <summary>
    /// Indicates if the ViewModel is still initializing.
    /// </summary>
    [ObservableProperty]
    private bool _isInitializing = true;

    /// <summary>
    /// Async initialization to ensure proper ordering:
    /// 1. Load data from database
    /// 2. Create snapshot
    /// 3. Kill already-running blocked apps
    /// 4. Start process watcher
    /// </summary>
    private async Task InitializeAsync()
    {
        try
        {
            IsInitializing = true;
            SetStatus("Loading blocked applications...");

            // 1. Load data from database (async to not block UI)
            await LoadDataAsync();

            // 2. Create snapshot AFTER data is loaded
            UpdateSnapshot();

            // 3. Only kill if we have blocked apps
            if (BlockedApplications.Count > 0)
            {
                SetStatus($"Terminating {BlockedApplications.Count} blocked apps...");
                await Task.Run(() => KillAlreadyRunningBlockedApps());
            }

            // 4. Start the process watcher
            StartProcessWatcher();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Initialization error: {ex.Message}");
            SetStatus($"⚠️ Initialization failed: {ex.Message}");
        }
        finally
        {
            IsInitializing = false;
        }
    }

    /// <summary>
    /// Loads blocked applications from the database asynchronously.
    /// </summary>
    private async Task LoadDataAsync()
    {
        // Run database query on background thread
        var apps = await Task.Run(() => 
            _databaseService?.GetBlockedApplications() ?? new List<string>()
        );

        // Update UI on dispatcher thread
        _dispatcher.Invoke(() =>
        {
            BlockedApplications.Clear();
            foreach (var app in apps)
            {
                BlockedApplications.Add(app);
            }
        });
    }

    /// <summary>
    /// Updates the thread-safe snapshot whenever the blocked list changes.
    /// Call this after any Add/Remove operation.
    /// </summary>
    private void UpdateSnapshot()
    {
        lock (_snapshotLock)
        {
            _blockedAppsSnapshot = BlockedApplications.ToList();
        }
    }

    /// <summary>
    /// Gets a thread-safe copy of the blocked apps list.
    /// Safe to call from any thread.
    /// </summary>
    private List<string> GetBlockedAppsSnapshot()
    {
        lock (_snapshotLock)
        {
            return new List<string>(_blockedAppsSnapshot);
        }
    }

    /// <summary>
    /// Kills any blocked applications that are already running when the app starts.
    /// Only called after LoadDataAsync completes.
    /// </summary>
    private void KillAlreadyRunningBlockedApps()
    {
        var snapshot = GetBlockedAppsSnapshot();
        
        // Guard: Only proceed if we have apps to kill
        if (snapshot.Count == 0)
        {
            Debug.WriteLine("No blocked apps to terminate");
            return;
        }

        foreach (var app in snapshot)
        {
            TerminateApplicationByName(app);
        }
    }

    /// <summary>
    /// Updates the status message on the UI thread.
    /// </summary>
    private void SetStatus(string message)
    {
        if (_dispatcher.CheckAccess())
        {
            StatusMessage = message;
        }
        else
        {
            _dispatcher.Invoke(() => StatusMessage = message);
        }
    }

    [RelayCommand]
    private void NavigateBack() => _navigateBack();

    /// <summary>
    /// Can only add applications if blocking is active.
    /// </summary>
    private bool CanAddApplication() => IsBlockingActive;

    [RelayCommand(CanExecute = nameof(CanAddApplication))]
    private void AddApplication()
    {
        if (string.IsNullOrWhiteSpace(NewApplicationName)) return;
        
        if (!BlockedApplications.Contains(NewApplicationName))
        {
            BlockedApplications.Add(NewApplicationName);
            _databaseService?.AddBlockedApplication(NewApplicationName);
            
            // Update snapshot for background thread
            UpdateSnapshot();
            
            // Immediately terminate if already running
            TerminateApplicationByName(NewApplicationName);
            
            SetStatus($"🚫 Blocked: {NewApplicationName}");
        }
        
        NewApplicationName = string.Empty;
    }

    /// <summary>
    /// Can only remove applications if blocking is active.
    /// </summary>
    private bool CanRemoveApplication(string _) => IsBlockingActive;

    [RelayCommand(CanExecute = nameof(CanRemoveApplication))]
    private void RemoveApplication(string application)
    {
        if (BlockedApplications.Contains(application))
        {
            BlockedApplications.Remove(application);
            _databaseService?.RemoveBlockedApplication(application);
            
            // Update snapshot for background thread
            UpdateSnapshot();
            
            SetStatus($"✅ Unblocked: {application}");
        }
    }

    #region Process Watching (Event-Driven, Thread-Safe)

    private void StartProcessWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _processWatcher = new ManagementEventWatcher(query);
            _processWatcher.EventArrived += OnProcessStarted;
            _processWatcher.Start();
            
            // Success - blocking is active
            IsBlockingActive = true;
            SetStatus("✅ App blocking active");
        }
        catch (ManagementException ex)
        {
            // WMI-specific error (usually permissions)
            Debug.WriteLine($"WMI error: {ex.Message}");
            IsBlockingActive = false;
            SetStatus("⚠️ App blocking requires administrator privileges");
        }
        catch (UnauthorizedAccessException)
        {
            // Access denied
            IsBlockingActive = false;
            SetStatus("⚠️ App blocking requires administrator privileges");
        }
        catch (Exception ex)
        {
            // Other errors
            Debug.WriteLine($"Process watcher failed: {ex.Message}");
            IsBlockingActive = false;
            SetStatus($"⚠️ App blocking failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Called on BACKGROUND THREAD when a new process starts.
    /// Uses snapshot copy to avoid cross-thread collection access.
    /// </summary>
    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            
            // Use thread-safe snapshot, NOT the ObservableCollection directly
            var blockedApps = GetBlockedAppsSnapshot();
            
            foreach (var blockedPattern in blockedApps)
            {
                if (IsProcessBlocked(processName, blockedPattern))
                {
                    TerminateProcess(processId);
                    Debug.WriteLine($"🚫 Blocked: {processName} (matched: {blockedPattern})");
                    
                    // Update UI on UI thread
                    SetStatus($"🚫 Blocked: {processName}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnProcessStarted error: {ex.Message}");
        }
    }

    private void TerminateProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill();
        }
        catch (ArgumentException)
        {
            // Process already exited - this is fine
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to kill PID {processId}: {ex.Message}");
        }
    }

    private void TerminateApplicationByName(string appName)
    {
        try
        {
            // For termination, extract the base name without wildcards
            var baseName = ExtractBaseName(appName);
            var processes = Process.GetProcessesByName(baseName);
            foreach (var p in processes)
            {
                try { p.Kill(); } catch { }
                p.Dispose();
            }
        }
        catch { }
    }

    #region Pattern Matching

    /// <summary>
    /// Checks if a process name matches a blocked pattern.
    /// Supports:
    /// - Exact match: "chrome" matches "chrome.exe" only
    /// - Wildcards: "chrome*" matches "chrome.exe", "chromedriver.exe"
    /// - Full path: "C:\Program Files\Discord\discord.exe"
    /// </summary>
    private bool IsProcessBlocked(string processName, string blockedPattern)
    {
        // Normalize both for comparison
        var normalizedProcess = NormalizeForComparison(processName);
        var normalizedPattern = NormalizeForComparison(blockedPattern);

        // Check if pattern contains wildcards
        if (normalizedPattern.Contains('*') || normalizedPattern.Contains('?'))
        {
            return MatchesWildcard(normalizedProcess, normalizedPattern);
        }

        // Check if pattern is a full path
        if (IsFullPath(blockedPattern))
        {
            // Get full path of running process and compare
            return MatchesFullPath(processName, blockedPattern);
        }

        // Exact match (default)
        return normalizedProcess == normalizedPattern;
    }

    /// <summary>
    /// Normalizes a name for comparison (removes .exe, lowercases).
    /// Does NOT extract from path - keeps full path if present.
    /// </summary>
    private string NormalizeForComparison(string name)
    {
        name = name.Trim().ToLowerInvariant();
        
        // Remove .exe suffix if present
        if (name.EndsWith(".exe"))
            name = name.Substring(0, name.Length - 4);
        
        return name;
    }

    /// <summary>
    /// Extracts just the base name for Process.GetProcessesByName().
    /// Removes path, extension, and wildcards.
    /// </summary>
    private string ExtractBaseName(string appName)
    {
        // Remove wildcards
        appName = appName.Replace("*", "").Replace("?", "");
        
        // Remove .exe
        appName = appName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
        
        // Remove path
        var slashIndex = appName.LastIndexOfAny(new[] { '\\', '/' });
        if (slashIndex >= 0)
            appName = appName.Substring(slashIndex + 1);
        
        return appName.Trim();
    }

    /// <summary>
    /// Checks if a pattern is a full path (contains drive letter or UNC).
    /// </summary>
    private bool IsFullPath(string pattern)
    {
        return (pattern.Length >= 2 && pattern[1] == ':') ||  // C:\...
               pattern.StartsWith("\\\\");                     // \\server\...
    }

    /// <summary>
    /// Matches a process name against a wildcard pattern.
    /// * = any characters, ? = single character
    /// </summary>
    private bool MatchesWildcard(string input, string pattern)
    {
        // Convert wildcard pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Matches a process against a full path pattern.
    /// </summary>
    private bool MatchesFullPath(string processName, string fullPathPattern)
    {
        try
        {
            // Find processes by name
            var baseName = ExtractBaseName(processName);
            var processes = Process.GetProcessesByName(baseName);
            
            foreach (var proc in processes)
            {
                try
                {
                    var procPath = proc.MainModule?.FileName ?? "";
                    if (string.Equals(procPath, fullPathPattern, StringComparison.OrdinalIgnoreCase))
                    {
                        proc.Dispose();
                        return true;
                    }
                }
                catch { }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch { }
        
        return false;
    }

    #endregion

    public void Dispose()
    {
        _processWatcher?.Stop();
        _processWatcher?.Dispose();
    }
}
```

### Pattern Matching Examples

| Pattern | Matches | Does NOT Match |
|---------|---------|----------------|
| `chrome` | `chrome.exe` | `chromedriver.exe`, `googlechrome.exe` |
| `chrome*` | `chrome.exe`, `chromedriver.exe` | `googlechrome.exe` |
| `*chrome*` | `chrome.exe`, `googlechrome.exe`, `chromedriver.exe` | - |
| `discord` | `discord.exe` | `discordcanary.exe` |
| `discord*` | `discord.exe`, `discordcanary.exe`, `discordptb.exe` | - |
| `C:\Program Files\Discord\discord.exe` | Only that specific path | Discord installed elsewhere |

### Usage Tips

```
Blocking "notepad"     → Blocks only notepad.exe
Blocking "notepad*"    → Blocks notepad.exe, notepad++.exe, etc.
Blocking "*game*"      → Blocks anything with "game" in the name
Blocking full path     → Blocks only that specific installation
```

> [!TIP]
> For precise blocking, use **exact names** (e.g., `discord`).
> For broader blocking, use **wildcards** (e.g., `*game*`).
> For maximum precision, use **full paths** (e.g., `C:\Games\Steam\steam.exe`).

---

## Why WMI is More Efficient

| Approach | CPU Usage | How It Works |
|----------|-----------|--------------|
| **Polling (Timer)** | ❌ High | Checks ALL processes every second, even if nothing changed |
| **WMI Events** | ✅ Minimal | OS notifies us ONLY when a process starts |

```
Polling:      [check] [check] [check] [check] [check] ... (constant work)
WMI Events:   [wait] .......... [event!] [wait] ... [event!] (work only when needed)
```

---

## Key Differences from Website Blocking

| Aspect | Website Blocking | Application Blocking |
|--------|-----------------|---------------------|
| **Mechanism** | Modifies hosts file | WMI process events + Kill |
| **Persistence** | System-level (hosts file persists) | Runtime only (needs app running) |
| **Admin Required** | Yes (hosts file) | Yes (WMI + process termination) |
| **Efficiency** | One-time write | Event-driven, minimal CPU |
| **Bypass Difficulty** | Edit hosts file | Close Prism app |

---

## Important Notes

> [!WARNING]
> This approach terminates processes immediately when they start, which may cause data loss. Consider adding a brief delay or showing a warning first.

> [!IMPORTANT]
> The application must run with **administrator privileges** for WMI process watching and process termination to work.

> [!NOTE]
> WMI `Win32_ProcessStartTrace` requires admin rights. This aligns with the existing manifest that requests admin for hosts file modification.

---

## XAML: Displaying Status and Disabled State

### Status Message Display

Add a status bar to show `StatusMessage` in `BlockedApplicationsPage.xaml`:

```xml
<!-- Status bar at top or bottom of page -->
<TextBlock 
    Text="{Binding StatusMessage}"
    Foreground="{Binding IsBlockingActive, Converter={StaticResource BoolToColorConverter}}"
    FontSize="14"
    Margin="0,10,0,0"
    HorizontalAlignment="Center"/>
```

### Warning Panel When Blocking is Inactive

Show a warning banner when WMI fails:

```xml
<!-- Warning banner - only visible when blocking is not active -->
<Border 
    Background="#FFF3CD" 
    CornerRadius="8"
    Padding="15"
    Margin="0,0,0,15"
    Visibility="{Binding IsBlockingActive, Converter={StaticResource InverseBoolToVisibilityConverter}}">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="⚠️" FontSize="20" Margin="0,0,10,0"/>
        <StackPanel>
            <TextBlock 
                Text="App Blocking Unavailable" 
                FontWeight="Bold" 
                Foreground="#856404"/>
            <TextBlock 
                Text="Please restart Prism as Administrator to enable app blocking." 
                Foreground="#856404"/>
        </StackPanel>
    </StackPanel>
</Border>
```

### Disabled Add Button

The button automatically disables because of `CanExecute`:

```xml
<!-- Add button - automatically disabled when IsBlockingActive is false -->
<Button 
    Content="Block Application"
    Command="{Binding AddApplicationCommand}"
    Style="{StaticResource MaterialDesignRaisedButton}"/>
    
<!-- The command's CanExecute (CanAddApplication) returns IsBlockingActive -->
<!-- WPF automatically disables the button when CanExecute returns false -->
```

### Alternative: Manual IsEnabled Binding

If not using `CanExecute`, bind `IsEnabled` directly:

```xml
<Button 
    Content="Block Application"
    Command="{Binding AddApplicationCommand}"
    IsEnabled="{Binding IsBlockingActive}"
    Style="{StaticResource MaterialDesignRaisedButton}"/>

<Button 
    Content="Remove"
    Command="{Binding RemoveApplicationCommand}"
    CommandParameter="{Binding}"
    IsEnabled="{Binding DataContext.IsBlockingActive, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
    Style="{StaticResource MaterialDesignFlatSecondaryButton}"/>
```

### Converters (Add to App.xaml)

```xml
<!-- In App.xaml Resources -->
<BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter"/>

<!-- Inverse visibility converter -->
<local:InverseBoolToVisibilityConverter x:Key="InverseBoolToVisibilityConverter"/>
```

```csharp
// InverseBoolToVisibilityConverter.cs
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

### Complete Page Example

```xml
<UserControl x:Class="Prism.UI.Views.BlockedApplicationsPage"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:md="http://materialdesigninxaml.net/winfx/xaml/themes">
    
    <Grid Margin="20">
        <StackPanel>
            <!-- Warning when not admin -->
            <Border 
                Background="#FFF3CD" 
                CornerRadius="8"
                Padding="15"
                Margin="0,0,0,15"
                Visibility="{Binding IsBlockingActive, Converter={StaticResource InverseBoolToVisibilityConverter}}">
                <TextBlock Text="⚠️ App blocking requires administrator privileges" Foreground="#856404"/>
            </Border>
            
            <!-- Status message -->
            <TextBlock Text="{Binding StatusMessage}" FontSize="14" Margin="0,0,0,10"/>
            
            <!-- Add new app -->
            <StackPanel Orientation="Horizontal" Margin="0,0,0,15">
                <TextBox 
                    Text="{Binding NewApplicationName, UpdateSourceTrigger=PropertyChanged}"
                    Width="300"
                    md:HintAssist.Hint="Application name (e.g., Discord)"
                    IsEnabled="{Binding IsBlockingActive}"/>
                <Button 
                    Content="Block"
                    Command="{Binding AddApplicationCommand}"
                    Margin="10,0,0,0"/>
            </StackPanel>
            
            <!-- List of blocked apps -->
            <ItemsControl ItemsSource="{Binding BlockedApplications}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Background="#2D2D2D" CornerRadius="8" Padding="15" Margin="0,5">
                            <Grid>
                                <TextBlock Text="{Binding}" VerticalAlignment="Center"/>
                                <Button 
                                    Content="Remove"
                                    Command="{Binding DataContext.RemoveApplicationCommand, RelativeSource={RelativeSource AncestorType=ItemsControl}}"
                                    CommandParameter="{Binding}"
                                    HorizontalAlignment="Right"/>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </StackPanel>
    </Grid>
</UserControl>
```

---

## User Feedback (When App is Blocked)

> [!TIP]
> When a blocked app is silently terminated, users have no idea what happened. Add visual feedback to inform them.

### Option 1: Status Message with Emoji

Update the `SetStatus` call in `OnProcessStarted`:

```csharp
private void OnProcessStarted(object sender, EventArrivedEventArgs e)
{
    try
    {
        var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
        var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
        
        var normalizedName = NormalizeAppName(processName);
        var blockedApps = GetBlockedAppsSnapshot();
        
        foreach (var blocked in blockedApps)
        {
            if (NormalizeAppName(blocked) == normalizedName)
            {
                TerminateProcess(processId);
                
                // User feedback with emoji (thread-safe)
                SetStatus($"🚫 Blocked: {processName}");
                
                // Log to history (optional)
                LogBlockEvent(processName);
                
                // Show overlay (optional)
                ShowBlockOverlay(processName);
                
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"OnProcessStarted error: {ex.Message}");
    }
}
```

### Option 2: Show BlockWindow Overlay

Show the existing `BlockWindow` briefly when an app is blocked:

```csharp
// Add this field
private Action<string>? _onAppBlocked;

// Constructor - accept callback from App.xaml.cs
public BlockedApplicationsViewModel(
    Action navigateBack, 
    Prism.Persistence.Services.DatabaseService? databaseService = null,
    Action<string>? onAppBlocked = null)
{
    _navigateBack = navigateBack;
    _databaseService = databaseService;
    _onAppBlocked = onAppBlocked;
    _dispatcher = Application.Current.Dispatcher;
    // ... rest of constructor
}

/// <summary>
/// Shows the block overlay on the UI thread.
/// </summary>
private void ShowBlockOverlay(string appName)
{
    _dispatcher.Invoke(() =>
    {
        _onAppBlocked?.Invoke(appName);
    });
}
```

In `App.xaml.cs`, wire up the callback:

```csharp
// In App.xaml.cs

private BlockWindow? _blockWindow;

private void ShowBlockerForApp(string appName)
{
    if (_blockWindow == null)
    {
        _blockWindow = new BlockWindow();
    }
    
    // Update the block window message (if it has a ViewModel)
    if (_blockWindow.DataContext is BlockViewModel vm)
    {
        vm.BlockedItemName = appName;
        vm.BlockMessage = $"🚫 {appName} is blocked";
    }
    
    _blockWindow.Show();
    _blockWindow.Activate();
    
    // Auto-hide after 3 seconds
    var timer = new System.Windows.Threading.DispatcherTimer
    {
        Interval = TimeSpan.FromSeconds(3)
    };
    timer.Tick += (s, e) =>
    {
        timer.Stop();
        _blockWindow?.Hide();
    };
    timer.Start();
}

// When creating the ViewModel:
var blockedAppsVm = new BlockedApplicationsViewModel(
    () => NavigateDashboard(),
    _databaseService,
    ShowBlockerForApp  // Pass the callback
);
```

### Option 3: Toast Notification (Non-Intrusive)

Use the existing `Microsoft.Toolkit.Uwp.Notifications` package for toast notifications:

```csharp
using Microsoft.Toolkit.Uwp.Notifications;

/// <summary>
/// Shows a Windows toast notification when an app is blocked.
/// </summary>
private void ShowToastNotification(string appName)
{
    _dispatcher.Invoke(() =>
    {
        try
        {
            new ToastContentBuilder()
                .AddText("🚫 Application Blocked")
                .AddText($"{appName} was prevented from running.")
                .SetToastDuration(ToastDuration.Short)
                .Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Toast failed: {ex.Message}");
        }
    });
}
```

### Option 4: Log to Blocking History

Add blocking events to the database for analytics:

```csharp
/// <summary>
/// Logs a blocking event to the database.
/// </summary>
private void LogBlockEvent(string appName)
{
    try
    {
        _databaseService?.LogBlockedAttempt(appName, DateTime.Now);
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Failed to log block event: {ex.Message}");
    }
}
```

Add the corresponding method to `DatabaseService.cs`:

```csharp
// In DatabaseService.cs

public void LogBlockedAttempt(string appName, DateTime timestamp)
{
    using var connection = GetConnection();
    connection.Open();
    
    using var command = connection.CreateCommand();
    command.CommandText = @"
        INSERT INTO BlockingHistory (AppName, Timestamp, Type)
        VALUES (@appName, @timestamp, 0)";
    command.Parameters.AddWithValue("@appName", appName);
    command.Parameters.AddWithValue("@timestamp", timestamp.ToString("o"));
    command.ExecuteNonQuery();
}

// Don't forget to create the table in InitializeDatabase():
// CREATE TABLE IF NOT EXISTS BlockingHistory (
//     Id INTEGER PRIMARY KEY AUTOINCREMENT,
//     AppName TEXT NOT NULL,
//     Timestamp TEXT NOT NULL,
//     Type INTEGER NOT NULL  -- 0 = App, 1 = Website
// );
```

### Complete OnProcessStarted with All Feedback Options

```csharp
/// <summary>
/// Called on BACKGROUND THREAD when a new process starts.
/// Uses snapshot copy and Dispatcher for thread safety.
/// </summary>
private void OnProcessStarted(object sender, EventArrivedEventArgs e)
{
    try
    {
        var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
        var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
        
        var normalizedName = NormalizeAppName(processName);
        var blockedApps = GetBlockedAppsSnapshot();
        
        foreach (var blocked in blockedApps)
        {
            if (NormalizeAppName(blocked) == normalizedName)
            {
                // 1. Kill the process
                TerminateProcess(processId);
                Debug.WriteLine($"🚫 Blocked: {processName} (PID: {processId})");
                
                // 2. Update status bar (thread-safe)
                SetStatus($"🚫 Blocked: {processName}");
                
                // 3. Show toast notification (thread-safe)
                ShowToastNotification(processName);
                
                // 4. Log to database (optional)
                LogBlockEvent(processName);
                
                // 5. Show overlay briefly (optional, more intrusive)
                // ShowBlockOverlay(processName);
                
                break;
            }
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"OnProcessStarted error: {ex.Message}");
    }
}
```

### Comparison of Feedback Methods

| Method | Intrusiveness | Best For |
|--------|--------------|----------|
| **StatusMessage** | 🟢 Low | Always use - subtle indicator in app |
| **Toast Notification** | 🟢 Low | Non-intrusive, works even if app is minimized |
| **BlockWindow Overlay** | 🔴 High | When user needs to know immediately |
| **Logging History** | 🟢 None | Analytics, parental monitoring |

> [!TIP]
> Combine **StatusMessage + Toast** for the best user experience. Use **BlockWindow** only if you want to be very intrusive (like a parental control app).

---

## Lifecycle Management (Preventing Memory Leaks)

> [!CAUTION]
> The `ManagementEventWatcher` runs on a background thread and will **leak memory** if not disposed properly. Nothing automatically calls `Dispose()` when navigating away or closing the app.

### Problem

| Issue | Cause |
|-------|-------|
| **Memory leak** | `ManagementEventWatcher` keeps running after navigating away |
| **Event handler leak** | `EventArrived` handler holds reference to ViewModel |
| **Multiple watchers** | Navigating to/from page creates new watchers without stopping old ones |

### Solution 1: Dispose When Navigating Away (MainViewModel)

Modify `MainViewModel.cs` to dispose the previous ViewModel when switching views:

```csharp
// In MainViewModel.cs

[ObservableProperty]
private object? _currentViewModel;

// Called automatically by source generator when CurrentViewModel changes
partial void OnCurrentViewModelChanging(object? oldValue, object? newValue)
{
    // Dispose the old ViewModel if it implements IDisposable
    if (oldValue is IDisposable disposable)
    {
        disposable.Dispose();
        System.Diagnostics.Debug.WriteLine($"Disposed: {oldValue.GetType().Name}");
    }
}

[RelayCommand]
private void NavigateBlockedApplications()
{
    // Old ViewModel will be disposed automatically by OnCurrentViewModelChanging
    CurrentViewModel = new BlockedApplicationsViewModel(
        () => NavigateDashboard(),
        _databaseService
    );
}
```

### Solution 2: Dispose on App Exit (App.xaml.cs)

Add cleanup in `App.xaml.cs` when the application closes:

```csharp
// In App.xaml.cs

private BlockedApplicationsViewModel? _blockedAppsViewModel;

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    
    // ... existing startup code ...
}

protected override void OnExit(ExitEventArgs e)
{
    // Dispose any active ViewModels with resources
    _blockedAppsViewModel?.Dispose();
    
    // Stop monitoring service
    _monitorService?.Stop();
    
    base.OnExit(e);
}
```

### Solution 3: Singleton Service (Recommended for Long-Lived Blocking)

For **app-wide blocking** that persists across navigation, move the watcher to a dedicated service:

```csharp
// New file: Prism.Monitoring/Services/ApplicationBlockingService.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;

namespace Prism.Monitoring.Services;

public sealed class ApplicationBlockingService : IDisposable
{
    private ManagementEventWatcher? _processWatcher;
    private readonly List<string> _blockedApps = new();
    private readonly object _lock = new();
    private bool _isDisposed;

    public void Start()
    {
        if (_processWatcher != null) return;
        
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _processWatcher = new ManagementEventWatcher(query);
            _processWatcher.EventArrived += OnProcessStarted;
            _processWatcher.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to start blocking service: {ex.Message}");
        }
    }

    public void Stop()
    {
        _processWatcher?.Stop();
    }

    public void UpdateBlockedApps(IEnumerable<string> apps)
    {
        lock (_lock)
        {
            _blockedApps.Clear();
            _blockedApps.AddRange(apps);
        }
    }

    public void AddBlockedApp(string app)
    {
        lock (_lock)
        {
            if (!_blockedApps.Contains(app))
                _blockedApps.Add(app);
        }
        TerminateByName(app);
    }

    public void RemoveBlockedApp(string app)
    {
        lock (_lock)
        {
            _blockedApps.Remove(app);
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            var normalized = Normalize(processName);

            List<string> snapshot;
            lock (_lock)
            {
                snapshot = new List<string>(_blockedApps);
            }

            foreach (var blocked in snapshot)
            {
                if (Normalize(blocked) == normalized)
                {
                    TerminateById(processId);
                    break;
                }
            }
        }
        catch { }
    }

    private void TerminateById(int pid)
    {
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
        }
        catch { }
    }

    private void TerminateByName(string name)
    {
        try
        {
            foreach (var p in Process.GetProcessesByName(Normalize(name)))
            {
                try { p.Kill(); } catch { }
                p.Dispose();
            }
        }
        catch { }
    }

    private static string Normalize(string name) =>
        name.Replace(".exe", "", StringComparison.OrdinalIgnoreCase)
            .Trim().ToLowerInvariant();

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        
        _processWatcher?.Stop();
        _processWatcher?.Dispose();
        _processWatcher = null;
    }
}
```

### Using the Singleton Service in App.xaml.cs

```csharp
// In App.xaml.cs

using Prism.Monitoring.Services;

public partial class App : Application
{
    private ApplicationBlockingService? _blockingService;
    private DatabaseService? _databaseService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        _databaseService = new DatabaseService();
        
        // Start app-wide blocking service
        _blockingService = new ApplicationBlockingService();
        var blockedApps = _databaseService.GetBlockedApplications();
        _blockingService.UpdateBlockedApps(blockedApps);
        _blockingService.Start();
        
        // ... rest of startup ...
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _blockingService?.Dispose();
        base.OnExit(e);
    }

    // Expose for ViewModels to use
    public ApplicationBlockingService? BlockingService => _blockingService;
}
```

### Simplified ViewModel (Uses Singleton Service)

If using the singleton service, the ViewModel becomes simpler:

```csharp
public partial class BlockedApplicationsViewModel : ObservableObject
{
    // No IDisposable needed - service handles lifecycle
    private readonly ApplicationBlockingService? _blockingService;
    
    public BlockedApplicationsViewModel(...)
    {
        _blockingService = (Application.Current as App)?.BlockingService;
        LoadData();
    }

    [RelayCommand]
    private void AddApplication()
    {
        if (string.IsNullOrWhiteSpace(NewApplicationName)) return;
        
        if (!BlockedApplications.Contains(NewApplicationName))
        {
            BlockedApplications.Add(NewApplicationName);
            _databaseService?.AddBlockedApplication(NewApplicationName);
            
            // Delegate to singleton service
            _blockingService?.AddBlockedApp(NewApplicationName);
        }
        
        NewApplicationName = string.Empty;
    }

    [RelayCommand]
    private void RemoveApplication(string application)
    {
        if (BlockedApplications.Contains(application))
        {
            BlockedApplications.Remove(application);
            _databaseService?.RemoveBlockedApplication(application);
            
            // Delegate to singleton service
            _blockingService?.RemoveBlockedApp(application);
        }
    }
}
```

### Comparison of Approaches

| Approach | Pros | Cons |
|----------|------|------|
| **Dispose on navigate** | Simple, per-page lifecycle | Blocking stops when navigating away |
| **Dispose on exit** | Ensures cleanup | Need to track all disposables |
| **Singleton service** | ✅ Always active, clean separation | Slightly more complex setup |

> [!TIP]
> **Recommended**: Use the **singleton service** approach. App blocking should work even when the user is on the Dashboard or Settings page, not just when viewing the Blocked Applications page.

---

## Persistent Blocking Options

> [!WARNING]
> When the Prism app closes, the WMI watcher stops and **blocked apps can run freely**. The following options address this limitation.

### Option 1: System Tray Persistence (Minimize to Tray)

Keep the app running in the system tray when the user clicks the close button:

#### Required NuGet Package

```xml
<!-- For Windows Forms NotifyIcon in WPF -->
<!-- No additional package needed - System.Windows.Forms is in .NET -->
```

#### Add to Prism.UI.csproj

```xml
<PropertyGroup>
  <!-- Enable Windows Forms interop for NotifyIcon -->
  <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

#### MainWindow.xaml.cs - System Tray Implementation

```csharp
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;

namespace Prism.UI;

public partial class MainWindow : Window
{
    private NotifyIcon? _trayIcon;
    private bool _isExiting = false;

    public MainWindow()
    {
        InitializeComponent();
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            // Use embedded resource or file path
            Icon = new Icon(SystemIcons.Shield, 40, 40), // Or load from resources
            Text = "Prism - App Blocking Active",
            Visible = false
        };

        // Create context menu for tray icon
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Open Prism", null, (s, e) => ShowFromTray());
        contextMenu.Items.Add("Exit Completely", null, (s, e) => ExitApplication());
        _trayIcon.ContextMenuStrip = contextMenu;

        // Double-click to restore
        _trayIcon.DoubleClick += (s, e) => ShowFromTray();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;  // Cancel the close
            MinimizeToTray();
        }
        
        base.OnClosing(e);
    }

    private void MinimizeToTray()
    {
        Hide();
        _trayIcon!.Visible = true;
        _trayIcon.ShowBalloonTip(
            3000,
            "Prism",
            "App blocking is still active. Right-click tray icon to exit.",
            ToolTipIcon.Info
        );
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _trayIcon!.Visible = false;
    }

    private void ExitApplication()
    {
        _isExiting = true;
        _trayIcon?.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }
}
```

#### Tray Icon with Custom Icon (Resources)

```csharp
// Load icon from embedded resource
var iconStream = Application.GetResourceStream(
    new Uri("pack://application:,,,/Assets/tray-icon.ico")
)?.Stream;

if (iconStream != null)
{
    _trayIcon.Icon = new Icon(iconStream);
}
```

---

### Option 2: Close Warning Dialog

Warn the user before closing that blocking will stop:

```csharp
protected override void OnClosing(CancelEventArgs e)
{
    // Check if blocking is active
    var blockingService = (Application.Current as App)?.BlockingService;
    var hasBlockedApps = blockingService?.HasBlockedApps ?? false;

    if (hasBlockedApps && !_isExiting)
    {
        var result = MessageBox.Show(
            "⚠️ Closing Prism will disable app blocking.\n\n" +
            "Blocked applications will be able to run freely.\n\n" +
            "Do you want to:\n" +
            "• Yes - Minimize to system tray (blocking stays active)\n" +
            "• No - Exit completely (blocking stops)\n" +
            "• Cancel - Stay in app",
            "Blocking Will Stop",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning
        );

        switch (result)
        {
            case MessageBoxResult.Yes:
                e.Cancel = true;
                MinimizeToTray();
                break;
            case MessageBoxResult.No:
                _isExiting = true;
                break;
            case MessageBoxResult.Cancel:
                e.Cancel = true;
                break;
        }
    }

    base.OnClosing(e);
}
```

#### Simpler Warning (No Tray)

```csharp
protected override void OnClosing(CancelEventArgs e)
{
    var result = MessageBox.Show(
        "⚠️ App blocking will stop when you close Prism.\n\n" +
        "Are you sure you want to exit?",
        "Confirm Exit",
        MessageBoxButton.YesNo,
        MessageBoxImage.Warning
    );

    if (result == MessageBoxResult.No)
    {
        e.Cancel = true;
    }

    base.OnClosing(e);
}
```

---

### Option 3: Windows Service (Always-On Blocking)

For truly persistent blocking that survives reboots and doesn't require the GUI:

#### Create a New Worker Service Project

```bash
dotnet new worker -n Prism.BlockingService -o Prism.BlockingService
dotnet sln add Prism.BlockingService
```

#### Prism.BlockingService/Worker.cs

```csharp
using System.Diagnostics;
using System.Management;

namespace Prism.BlockingService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private ManagementEventWatcher? _processWatcher;
    private List<string> _blockedApps = new();
    private readonly object _lock = new();

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Prism Blocking Service starting...");

        // Load blocked apps from shared config/database
        await LoadBlockedAppsAsync();

        // Start WMI watcher
        StartProcessWatcher();

        // Keep running until stopped
        while (!stoppingToken.IsCancellationRequested)
        {
            // Periodically reload blocked apps in case they changed
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            await LoadBlockedAppsAsync();
        }
    }

    private async Task LoadBlockedAppsAsync()
    {
        try
        {
            // Read from shared database or config file
            var configPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Prism",
                "blocked-apps.txt"
            );

            if (File.Exists(configPath))
            {
                var apps = await File.ReadAllLinesAsync(configPath);
                lock (_lock)
                {
                    _blockedApps = apps.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
                }
                _logger.LogInformation("Loaded {Count} blocked apps", _blockedApps.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blocked apps");
        }
    }

    private void StartProcessWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _processWatcher = new ManagementEventWatcher(query);
            _processWatcher.EventArrived += OnProcessStarted;
            _processWatcher.Start();
            _logger.LogInformation("Process watcher started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process watcher");
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processName = e.NewEvent.Properties["ProcessName"].Value?.ToString() ?? "";
            var processId = Convert.ToInt32(e.NewEvent.Properties["ProcessID"].Value);
            var normalized = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

            List<string> snapshot;
            lock (_lock)
            {
                snapshot = new List<string>(_blockedApps);
            }

            foreach (var blocked in snapshot)
            {
                if (blocked.Equals(normalized, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var proc = Process.GetProcessById(processId);
                        proc.Kill();
                        _logger.LogInformation("Blocked: {Process} (PID: {PID})", processName, processId);
                    }
                    catch { }
                    break;
                }
            }
        }
        catch { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _processWatcher?.Stop();
        _processWatcher?.Dispose();
        _logger.LogInformation("Prism Blocking Service stopped");
        await base.StopAsync(cancellationToken);
    }
}
```

#### Prism.BlockingService/Program.cs

```csharp
using Prism.BlockingService;

var builder = Host.CreateApplicationBuilder(args);

// Add Windows Service support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Prism App Blocking Service";
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

#### Prism.BlockingService.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <OutputType>exe</OutputType>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.0.0" />
    <PackageReference Include="System.Management" Version="8.0.0" />
  </ItemGroup>
</Project>
```

#### Install the Windows Service

```powershell
# Build and publish
dotnet publish Prism.BlockingService -c Release -r win-x64 --self-contained

# Install as Windows Service (elevated PowerShell)
sc.exe create "PrismBlockingService" binPath="C:\path\to\Prism.BlockingService.exe" start=auto
sc.exe description "PrismBlockingService" "Prism App Blocking Service - blocks specified applications"
sc.exe start PrismBlockingService

# Or using New-Service
New-Service -Name "PrismBlockingService" -BinaryPathName "C:\path\to\Prism.BlockingService.exe" -StartupType Automatic -Description "Blocks specified applications"
Start-Service PrismBlockingService
```

#### Uninstall

```powershell
sc.exe stop PrismBlockingService
sc.exe delete PrismBlockingService
```

---

### Comparison of Persistence Options

| Option | Persistence | User Experience | Complexity |
|--------|-------------|-----------------|------------|
| **System Tray** | While logged in | ✅ Familiar, low friction | 🟡 Medium |
| **Close Warning** | None (just warns) | ✅ Clear communication | 🟢 Easy |
| **Windows Service** | ✅ Always (survives reboot) | 🔴 No UI, admin needed | 🔴 High |
| **Startup + Tray** | While logged in, auto-starts | ✅ Best balance | 🟡 Medium |

### Recommended Approach

For a typical user:

```
1. System Tray + Startup (Option 1)
   - Minimize to tray on close
   - Add to Windows Startup (registry or Startup folder)
   - Shows notification "Blocking still active"

2. Close Warning (Option 2)
   - Warn before exit
   - Let user choose: Tray or Exit

3. Windows Service (Option 3)
   - For enterprise/parental control use cases
   - Runs without user login
   - Can't be easily killed by users
```

> [!TIP]
> For most users, **System Tray + Startup** provides the best balance of persistence and usability.

---

## Future Enhancements

| Feature | Description |
|---------|-------------|
| **Grace Period** | Show a 5-second warning before termination |
| **Usage Tracking** | Log blocked app launch attempts |
| **Schedule Integration** | Only block during focus sessions |
| **Gentle Mode** | Show overlay instead of killing (let user save work) |
| **System Tray** | Run in background with minimize-to-tray |
| **Windows Service** | Persistent blocking that survives reboots |
