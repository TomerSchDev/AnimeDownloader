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
            Logger.Warning($"No history file found at {filePath}");
            return;
        }
        File.Delete(filePath);
        Logger.Info("Removed old history");

    }

    public bool DeleteTask(string entryId)
    {
        if (_history.Remove(entryId))
        {
            Logger.Info($"Removed entry {entryId}");
            return true;
        }
        Logger.Warning("No task found with id " + entryId);
        return false;

    }

    private void LoadHistory(string? filePath = null)
    {
        try
        {
            filePath ??= AppStorageService.GetPath(ConfigurationManager.Instance.DefaulterHistoryFIle);
            Logger.Info($"Loading history from: {filePath}");
            
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (directory == null)
            {
                Logger.Error("Invalid file path for history");
                return;
            }

            // Create directory if it doesn't exist
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Logger.Info($"Created directory: {directory}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating directory {directory}: {ex.Message}");
                return;
            }

            if (!File.Exists(filePath))
            {
                Logger.Info($"No history file found at {filePath}, starting with empty history");
                _history.Clear();
                return;
            }

            try
            {
                var jsonContent = File.ReadAllText(filePath);
                var historyEntries = JsonSerializer.Deserialize<Dictionary<string, TaskHistoryEntry>>(jsonContent);

                if (historyEntries == null || historyEntries.Count == 0)
                {
                    Logger.Info("History file is empty");
                    _history.Clear();
                    return;
                }

                // Clear existing history and add new entries
                _history.Clear();
                foreach (var entry in historyEntries.Where(entry => !string.IsNullOrEmpty(entry.Key)))
                {
                    _history[entry.Key] = entry.Value;
                }
                
                Logger.Info($"Successfully loaded {_history.Count} history entries");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error reading history file: {ex.Message}");
                // Continue with empty history
                _history.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Error in LoadHistory: {ex.Message}");
            _history.Clear();
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
        
        string? tempFile = null;
        string? targetFile = null;
        
        try
        {
            targetFile = AppStorageService.GetPath(ConfigurationManager.Instance.DefaulterHistoryFIle);
            var directory = Path.GetDirectoryName(targetFile);
            
            if (string.IsNullOrEmpty(directory))
            {
                Logger.Error("Invalid directory path for history file");
                return;
            }

            // Ensure directory exists
            try
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    Logger.Info($"Created directory: {directory}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating directory {directory}: {ex.Message}");
                return;
            }

            // Create a temporary file first
            tempFile = Path.Combine(directory, Path.GetRandomFileName());
            
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            // Serialize to memory first to avoid partial writes
            var jsonString = JsonSerializer.Serialize(_history, jsonOptions);
            
            // Write to temp file
            File.WriteAllText(tempFile, jsonString);
            
            // Now that we've successfully written the temp file, move it to the target location
            if (File.Exists(targetFile))
            {
                // On Windows, we need to delete the target file first if it exists
                File.Delete(targetFile);
            }
            
            File.Move(tempFile, targetFile);
            
            _updatedFlag = false;
            Logger.Info($"History saved to {targetFile}");
        }
        catch (Exception ex)
        {
            Logger.Error($"Error saving history to {targetFile ?? "unknown location"}: {ex.Message}");
            
            // Clean up temp file if it exists
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); }
                catch { /* Ignore cleanup errors */ }
            }
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
            Logger.Info($"History exported to {filePath}");
            //TODO add here notification
        }
        catch (Exception ex)
        {
            Logger.Error($"Error exporting history: {ex.Message}");
            throw; // Re-throw to allow caller to handle the error
        }
    }
}