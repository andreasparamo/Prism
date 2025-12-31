# Prism

> **Status:** Alpha / Prototype 
> **Theme:** Opal-Inspired (Dark Mode, Gradients, Glassmorphism)

Prism is a **local-only, privacy-first** Windows desktop application designed to help users manage their screen time through interventions. It is inspired by the "Opal" iOS app, bringing a gem-like aesthetic and strict focus tools to the Windows ecosystem.

It is built as a modular .NET 8 application using WPF for the UI and SQLite for local persistence.

## Features

### Opal-Inspired UI
*   **Deep Midnight Blue** theme with vibrant Purple-to-Blue gradients.
*   **Modern WPF** implementations using `MaterialDesignThemes` and `MahApps.Metro.IconPacks`.
*   **Custom Navigation Shell** with a clean sidebar and MVVM-driven view switching.

### Focus Protection (The Blocker)
*   **Real-time Monitoring:** detects active window changes immediately using Win32 API hooks.
*   **Intervention Overlay:** When a distracting app is detected (configurable), a full-screen, topmost overlay appears.
*   **"Conscious" Unblocking:** Users can click "I really need to access this" to bypass blocks (dependent on difficulty settings).

### Local Analytics
*   **Privacy-First:** All activity data (active window, duration) is stored locally in an SQLite database.
*   **Data Aggregation:** (In Progress) Calculating "Focus Score" based on productive vs. distracting app usage.

---

## Architecture

The solution uses a Clean Architecture / Modular Monolith approach:

| Project | Type | Description |
| :--- | :--- | :--- |
| **Prism.UI** | WPF App | The compiled executable. Uses **MVVM** (CommunityToolkit) for logic separation. Handles the MainWindow, Navigation, and Overlay logic. |
| **Prism.Core** | Class Lib | Contains shared Domain Models (`AppUsage`, `AppCategory`) and Interfaces. |
| **Prism.Monitoring** | Class Lib | Wrapped Win32 APIs (`User32.dll`) to poll active windows and process names safely. |
| **Prism.Interventions** | Class Lib | (Future) Advanced blocking strategies and difficult-to-bypass logic. |
| **Prism.Persistence** | Class Lib | SQLite implementation. Handles `UsageLogs` and `AppConfig` tables. |

---

## Getting Started



### Prerequisites

*   **OS:** Windows 10/11 (x64)
*   **.NET SDK:** .NET 8.0 ([Download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0))

### running the App

1.  **Clone the Repository**:
    ```powershell
    git clone <repository-url>
    cd Prism
    ```

2.  **Build**:
    ```powershell
    dotnet build
    ```

3.  **Run**:
    ```powershell
    dotnet watch --project Prism.UI
    ```

---

## Roadmap

*   [ ] **Schedule System:** Allow users to set specific hours for blocking.
*   [ ] **App Categorization Page:** UI for users to manage their own Productive/Distracting lists.
*   [ ] **Analytics Dashboard:** Real charts to visualize time spent.
*   [ ] **Strict Mode:** Prevent the Overlay from being closed via Task Manager (requires specialized `Interventions` logic).

---

## Privacy & Constraints

*   **Local-Only:** No data ever leaves the user's machine.
*   **No Cloud APIs:** No accounts, no sync.
*   **No Admin Rights:** Designed to run in standard User Mode.

---

### Clearing the Welcome Page

To clear the welcome page use this command:
```powershell
Remove-Item "$env:LOCALAPPDATA\Prism\welcome_completed" -ErrorAction SilentlyContinue
```
### Resetting Application Data

To completely reset the application (delete database and settings):
```powershell
Remove-Item -Recurse -Force "$env:LOCALAPPDATA\Prism"
```
