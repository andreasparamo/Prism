# System Tray Implementation

## Problem

When user clicks X → app closes → WMI process watcher stops → blocked apps can run freely.

## Solution

Intercept the close event, hide the window instead of closing, show a tray icon. Process stays alive, blocking continues.

---

## Prerequisites

Add to `Prism.UI.csproj`:

```xml
<PropertyGroup>
  <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

The project already has `Hardcodet.NotifyIcon.Wpf` which is the WPF-native alternative. Use whichever is available.

---

## Implementation

### MainWindow.xaml.cs

```csharp
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

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
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "Prism - App Blocking Active",
            Visible = false
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Prism", null, (s, e) => RestoreFromTray());
        menu.Items.Add("Exit", null, (s, e) => ExitCompletely());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            e.Cancel = true;      // Block the close
            Hide();               // Hide window
            _trayIcon!.Visible = true;
            _trayIcon.ShowBalloonTip(2000, "Prism", 
                "App blocking still active", ToolTipIcon.Info);
            return;
        }
        base.OnClosing(e);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        _trayIcon!.Visible = false;
    }

    private void ExitCompletely()
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

---

## Flow Diagram

```
User clicks X
     │
     ▼
OnClosing fires
     │
     ├─ _isExiting == false?
     │         │
     │         ▼ YES
     │    e.Cancel = true
     │    Hide()
     │    Show tray icon
     │    [RETURN - window hidden, process alive]
     │
     └─ _isExiting == true?
               │
               ▼ YES
          base.OnClosing(e)
          Application terminates
```

---

## Key Variables

| Variable | Purpose |
|----------|---------|
| `_isExiting` | `false` = minimize to tray; `true` = actually close |
| `_trayIcon` | The system tray icon instance |
| `e.Cancel` | Set to `true` to prevent window from closing |

---

## Behavior Summary

| Action | Result |
|--------|--------|
| Click X | Window hides, tray icon appears, process continues |
| Double-click tray | Window restores |
| Right-click tray → Exit | App terminates completely |

---

## Why This Matters

Without tray behavior:
- User clicks X → App closes → WMI watcher stops → Blocking fails

With tray behavior:
- User clicks X → Window hides → Process alive → WMI watcher active → Blocking continues ✅

---

## Testing Checklist

- [ ] Click X → Window hides, tray icon appears
- [ ] Tray tooltip shows "Prism - App Blocking Active"
- [ ] Double-click tray → Window restores
- [ ] Right-click tray → "Exit" terminates app
- [ ] While in tray, blocked apps still get terminated
