using Microsoft.Data.Sqlite;
using Prism.Core.Enums;
using Prism.Core.Models;
using System.IO;

namespace Prism.Persistence.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Prism");
        Directory.CreateDirectory(folder);
        var dbPath = Path.Combine(folder, "prism.db");
        _connectionString = $"Data Source={dbPath}";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText =
        @"
            CREATE TABLE IF NOT EXISTS UsageLogs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                ProcessName TEXT NOT NULL,
                WindowTitle TEXT
            );

            CREATE TABLE IF NOT EXISTS AppConfig (
                ProcessName TEXT PRIMARY KEY,
                Category INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS BlockedItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Value TEXT NOT NULL,
                Type INTEGER NOT NULL
            );
        ";
        command.ExecuteNonQuery();
    }

    public void LogActivity(string processName, string windowTitle)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO UsageLogs (Timestamp, ProcessName, WindowTitle) VALUES ($time, $process, $title)";
        command.Parameters.AddWithValue("$time", DateTime.Now.ToString("o"));
        command.Parameters.AddWithValue("$process", processName);
        command.Parameters.AddWithValue("$title", windowTitle ?? string.Empty);
        command.ExecuteNonQuery();
    }

    public void SetAppCategory(string processName, AppCategory category)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO AppConfig (ProcessName, Category) VALUES ($process, $category)
            ON CONFLICT(ProcessName) DO UPDATE SET Category = $category";
        command.Parameters.AddWithValue("$process", processName);
        command.Parameters.AddWithValue("$category", (int)category);
        command.ExecuteNonQuery();
    }

    public AppCategory GetAppCategory(string processName)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Category FROM AppConfig WHERE ProcessName = $process";
        command.Parameters.AddWithValue("$process", processName);
        
        var result = command.ExecuteScalar();
        if (result != null && result != DBNull.Value)
        {
            return (AppCategory)Convert.ToInt32(result);
        }
        return AppCategory.Neutral; // Default
    }

    public List<AppUsage> GetDailyStats(DateTime date)
    {
        var stats = new List<AppUsage>();
        // NOTE: This is a simplified logic. In a real app we'd need to aggregate logs into durations.
        // For now, we will return empty or mock data until the aggregation logic is fully defined.
        return stats; 
    }

    // --- Blocked Items ---

    public List<string> GetBlockedApplications()
    {
        return GetBlockedItems(0);
    }

    public void AddBlockedApplication(string appName)
    {
        AddBlockedItem(appName, 0);
    }

    public void RemoveBlockedApplication(string appName)
    {
        RemoveBlockedItem(appName, 0);
    }

    public List<string> GetBlockedWebsites()
    {
        return GetBlockedItems(1);
    }

    public void AddBlockedWebsite(string url)
    {
        AddBlockedItem(url, 1);
    }

    public void RemoveBlockedWebsite(string url)
    {
        RemoveBlockedItem(url, 1);
    }

    private List<string> GetBlockedItems(int type)
    {
        var items = new List<string>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM BlockedItems WHERE Type = $type";
        command.Parameters.AddWithValue("$type", type);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            items.Add(reader.GetString(0));
        }
        return items;
    }

    private void AddBlockedItem(string value, int type)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Check if exists
        var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(1) FROM BlockedItems WHERE Value = $value AND Type = $type";
        checkCmd.Parameters.AddWithValue("$value", value);
        checkCmd.Parameters.AddWithValue("$type", type);
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());

        if (count == 0)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO BlockedItems (Value, Type) VALUES ($value, $type)";
            command.Parameters.AddWithValue("$value", value);
            command.Parameters.AddWithValue("$type", type);
            command.ExecuteNonQuery();
        }
    }

    private void RemoveBlockedItem(string value, int type)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM BlockedItems WHERE Value = $value AND Type = $type";
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$type", type);
        command.ExecuteNonQuery();
    }
}
