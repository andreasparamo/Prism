using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

namespace Prism.UI.ViewModels;

/// <summary>
/// ViewModel for managing blocked applications with real-time process termination.
/// Uses WMI events to detect and terminate blocked apps immediately when they launch.
/// </summary>
public partial class BlockedApplicationsViewModel : ObservableObject, IDisposable
{
    private readonly Action _navigateBack;
    private readonly Prism.Persistence.Services.DatabaseService? _databaseService;
    private readonly Dispatcher _dispatcher;
    
    // WMI process watcher for event-driven blocking
    private ManagementEventWatcher? _processWatcher;
    
    // Thread-safe snapshot for background thread access
    private List<string> _blockedAppsSnapshot = new();
    private readonly object _snapshotLock = new();

    [ObservableProperty]
    private string _newApplicationName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Indicates if WMI process watcher is running.
    /// False if WMI failed (e.g., not running as admin).
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddApplicationCommand))]
    private bool _isBlockingActive = false;

    [ObservableProperty]
    private bool _isInitializing = true;

    public ObservableCollection<string> BlockedApplications { get; } = new();

    public BlockedApplicationsViewModel(Action navigateBack, Prism.Persistence.Services.DatabaseService? databaseService = null)
    {
        _navigateBack = navigateBack;
        _databaseService = databaseService;
        _dispatcher = Application.Current.Dispatcher;

        // Start async initialization
        _ = InitializeAsync();
    }

    #region Initialization

    /// <summary>
    /// Async initialization ensures proper ordering:
    /// 1. Load data → 2. Create snapshot → 3. Kill running blocked apps → 4. Start watcher
    /// </summary>
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
                SetStatus($"Checking for {BlockedApplications.Count} blocked apps...");
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

    private async Task LoadDataAsync()
    {
        var apps = await Task.Run(() =>
            _databaseService?.GetBlockedApplications() ?? new List<string>()
        );

        _dispatcher.Invoke(() =>
        {
            BlockedApplications.Clear();
            foreach (var app in apps)
            {
                BlockedApplications.Add(app);
            }
        });
    }

    #endregion

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

    #region Commands

    [RelayCommand]
    private void NavigateBack() => _navigateBack();

    private bool CanAddApplication() => IsBlockingActive || !IsInitializing;

    [RelayCommand(CanExecute = nameof(CanAddApplication))]
    private void AddApplication()
    {
        if (string.IsNullOrWhiteSpace(NewApplicationName)) return;

        if (!BlockedApplications.Contains(NewApplicationName))
        {
            BlockedApplications.Add(NewApplicationName);
            _databaseService?.AddBlockedApplication(NewApplicationName);
            UpdateSnapshot();

            // Immediately terminate if already running
            TerminateApplicationByName(NewApplicationName);
            SetStatus($"🚫 Blocked: {NewApplicationName}");
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
            UpdateSnapshot();
            SetStatus($"✅ Unblocked: {application}");
        }
    }

    #endregion

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
        catch (UnauthorizedAccessException)
        {
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

    /// <summary>
    /// Called on BACKGROUND THREAD when a new process starts.
    /// Uses thread-safe snapshot to avoid cross-thread collection access.
    /// </summary>
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
                    Debug.WriteLine($"🚫 Blocked: {processName} (matched: {blockedPattern})");
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
            // Process already exited
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

    private void KillAlreadyRunningBlockedApps()
    {
        var snapshot = GetBlockedAppsSnapshot();
        if (snapshot.Count == 0) return;

        foreach (var app in snapshot)
        {
            TerminateApplicationByName(app);
        }
    }

    #endregion

    #region Pattern Matching

    /// <summary>
    /// Checks if a process name matches a blocked pattern.
    /// Supports exact match and wildcards (* and ?).
    /// </summary>
    private bool IsProcessBlocked(string processName, string blockedPattern)
    {
        var normalizedProcess = NormalizeForComparison(processName);
        var normalizedPattern = NormalizeForComparison(blockedPattern);

        if (normalizedPattern.Contains('*') || normalizedPattern.Contains('?'))
        {
            return MatchesWildcard(normalizedProcess, normalizedPattern);
        }

        return normalizedProcess == normalizedPattern;
    }

    private string NormalizeForComparison(string name)
    {
        name = name.Trim().ToLowerInvariant();
        if (name.EndsWith(".exe"))
            name = name.Substring(0, name.Length - 4);
        return name;
    }

    private string ExtractBaseName(string appName)
    {
        appName = appName.Replace("*", "").Replace("?", "");
        appName = appName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);

        var slashIndex = appName.LastIndexOfAny(new[] { '\\', '/' });
        if (slashIndex >= 0)
            appName = appName.Substring(slashIndex + 1);

        return appName.Trim();
    }

    private bool MatchesWildcard(string input, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _processWatcher?.Stop();
        _processWatcher?.Dispose();
        _processWatcher = null;
    }

    #endregion
}

