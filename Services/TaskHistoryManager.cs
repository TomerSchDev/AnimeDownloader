using System.IO;
using System.Text.Json;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Views;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;

namespace AnimeBingeDownloader.Services;

public class TaskHistoryManager
{
    public static TaskHistoryManager Instance { get; } = new();
    private readonly Dictionary<string,TaskHistoryEntry> _history = new();
    public Logger Logger { get; } = new("TaskHistoryManager");

    private bool _updatedFlag = false;
    private readonly string _exportCall;

    private TaskHistoryManager()
    {
        Utils.AppLogger.MegaLogger.Subscribe(Logger);
        LoadHistory(null);
        _exportCall = PeriodicCallerService.Instance.AddNewCall(PeriodicExport, null,1000,5000);;
        
    }
    public List<TaskHistoryEntry> GetAllHistory()
    {
        return _history.Values.ToList();
    }
        
    public List<TaskHistoryEntry> GetHistoryByStatus(TaskStatus status)
    {
        List<TaskHistoryEntry> ret = [];
        ret.AddRange(_history.Values.Where(item => item._taskStatus == status));
        return ret;
    }

    public void Close()
    {
        PeriodicCallerService.Instance.RemoveCall(_exportCall);
        ClearHistory();
    }
    public void ClearHistory()
    {
        _history.Clear();
        var filePath = AppStorageService.GetPath(ConfigurationManager.Instance.DefaulterHistoryFIle);
        if (!File.Exists(filePath))
        {
            Logger.AddLog($"No history file found at {filePath}");
            return;
        }
        File.Delete(filePath);
        Logger.AddLog("Removed old history");

    }

    public bool DeleteTask(string entryId)
    {
        if (_history.Remove(entryId))
        {
            Logger.AddLog($"Removed entry {entryId}");
            return true;
        }
        Logger.AddLog("No task found with id " + entryId);
        return false;

    }

    private void LoadHistory(string? filePath)
    {
        try
        {
            filePath ??= AppStorageService.GetPath(ConfigurationManager.Instance.DefaulterHistoryFIle);
            Logger.AddLog($"Loading history from: {filePath}");
            
            if (!File.Exists(filePath))
            {
                Logger.AddLog($"No history file found at {filePath}");
                return;
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonString = File.ReadAllText(filePath);
            var historyEntries = JsonSerializer.Deserialize<Dictionary<string, TaskHistoryEntry>>(jsonString, jsonOptions);

            if (historyEntries == null || historyEntries.Count == 0)
            {
                Logger.AddLog("No valid history data found in the file");
                return;
            }

            _history.Clear();
            foreach (var entry in historyEntries)
            {
                if (entry.Value != null && !string.IsNullOrEmpty(entry.Key))
                {
                    _history[entry.Key] = entry.Value;
                }
            }
            
            Logger.AddLog($"Successfully loaded {_history.Count} history entries");
        }
        catch (Exception ex)
        {
            Logger.AddLog($"Error loading history: {ex.Message}");
            // Optionally rethrow or handle the error as needed
        }
    }
    public void SaveTask(TaskViewModel task)
    {
        _updatedFlag = true;
        _history[task.Id] = new TaskHistoryEntry(task);
       
    }

    private void PeriodicExport(object? o)
    {
        if (!_updatedFlag) return;
        
        try
        {
            var path = AppStorageService.GetPath(ConfigurationManager.Instance.DefaulterHistoryFIle);
            var directory = Path.GetDirectoryName(path);
            
            // Ensure directory exists
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var jsonString = JsonSerializer.Serialize(_history, jsonOptions);
            File.WriteAllText(path, jsonString);
            
            _updatedFlag = false;
            Logger.AddLog($"History saved to {path}");
        }
        catch (Exception ex)
        {
            Logger.AddLog($"Error saving history: {ex.Message}");
        }
    }

    public void ExportHistory(string filePath)
    {
        try
        {
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            
            var jsonString = JsonSerializer.Serialize(_history, jsonOptions);
            File.WriteAllText(filePath, jsonString);
            Logger.AddLog($"History exported to {filePath}");
        }
        catch (Exception ex)
        {
            Logger.AddLog($"Error exporting history: {ex.Message}");
            throw; // Re-throw to allow caller to handle the error
        }
    }
}