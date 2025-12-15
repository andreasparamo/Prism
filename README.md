# Prism

> **Status:** Foundation Complete (Ready for Feature Development)

Prism is a **local-only, privacy-first** Windows desktop application designed to help users manage their screen time through interventions. It is built as a modular .NET 8 application using WPF for the UI and SQLite for local persistence.

## Privacy & Architecture

Prism is designed with strict constraints to ensure user privacy and security:

*   **Local-Only:** No data ever leaves the user's machine. There are no cloud services, accounts, or sync features.
*   **No Telemetry:** No usage data or crash reports are sent to external servers.
*   **Privacy-First:** No logging of keystrokes, mouse input, or screen capture. Monitoring is strictly limited to metadata (e.g., "active window title") via Win32 APIs.

## Prerequisites

*   **Operating System:** Windows 10 or 11 (x64)
*   **.NET SDK:** Version 8.0 or later (`dotnet --version`)
*   **IDE:** Visual Studio 2022 (Community or higher) with:
    *   .NET desktop development workload
    *   Desktop development with C++ (required for Win32 headers)

## Getting Started

1.  **Clone the Repository** (if you haven't already):
    ```powershell
    git clone <repository-url>
    cd Prism
    ```

2.  **Build the Solution**:
    ```powershell
    dotnet build
    ```

3.  **Run the Application**:
    ```powershell
    dotnet run --project Prism.UI
    ```
    *Note: Currently, the application will launch a blank WPF window. This confirms the toolchain and dependencies are correctly configured.*

## Project Structure

The solution is divided into modular projects to separate concerns:

| Project | Type | Description |
| :--- | :--- | :--- |
| **Prism.UI** | WPF App | The compiled executable and UI host. Handles the System Tray icon, Windows Toast notifications, and the main configuration window. |
| **Prism.Core** | Class Lib | Contains shared interfaces, domain models, and business logic definitions. |
| **Prism.Monitoring** | Class Lib | Responsible for detecting user activity. Currently contains `Win32Hooks.cs` with P/Invoke stubs for `SetWinEventHook`, `GetForegroundWindow`, etc. |
| **Prism.Interventions** | Class Lib | Will contain the logic for "interventions" (e.g., blocking a specific app, showing a reminder). |
| **Prism.Persistence** | Class Lib | Handles data storage using SQLite (`Microsoft.Data.Sqlite`). |

## Development Guide

### Win32 Monitoring
The file `Prism.Monitoring/Win32Hooks.cs` contains the necessary P/Invoke signatures to interact with the Windows API. Feature development should start by implementing the logic to poll or subscribe to these events to detect the active window.

### Persistence
The `Prism.Persistence` project references `Microsoft.Data.Sqlite`. You will need to implement a repository pattern here to store application settings and usage logs locally.

### Interventions
Logic for determining *when* to interrupt the user belongs in `Prism.Interventions`. This should remain decoupled from the specific UI implementation (handled by `Prism.UI`).

## Constraints

When adding features, adhere to the following:
1.  **No Web Stack**: Do not add ASP.NET, React, or Blazor components.
2.  **No Admin Rights**: The app should run comfortably in user mode.
3.  **No Cloud APIs**: Do not add dependencies that require internet connectivity for core functionality.
