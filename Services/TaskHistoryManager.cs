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
    private readonly Logger _logger = new("TaskHistoryManager");
    public Logger Logger => _logger;

    private bool _updatedFlag = false;
    private string _exportCall;

    private TaskHistoryManager()
    {
        MainWindow.Logger.Subscribe(_logger);
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
        ret.AddRange(_history.Values.Where(item => item.getStatus() == status));
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
    }

    public bool DeleteTask(string entryId)
    {
        if (_history.Remove(entryId))
        {
            _logger.AddLog($"Removed entry {entryId}");
            return true;
        }
        _logger.AddLog("No task found with id " + entryId);
        return false;

    }

    public void LoadHistory(string? filePath)
    {
        filePath ??= AppStorageService.GetPath(ConfigurationManager.Instance.DefaulterHistoryFIle);
        if (!File.Exists(filePath))
        {
            _logger.AddLog($"No task found with path {filePath}");
            return;
        }
        var fileData = File.ReadAllText(filePath);
        var temp =
            JsonSerializer.Deserialize<Dictionary<string, TaskHistoryEntry>>(fileData);
        if (temp == null || temp.Count == 0)
        {
            _logger.AddLog($"No history found in path " + filePath);
            return;
        }
        _history.Clear();
        foreach (var id  in temp.Keys)
        {
            _history[id]=temp[id];
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
        var path = ConfigurationManager.Instance.DefaulterHistoryFIle;
        ExportHistory(path);
        _updatedFlag = false;
    }

    public void ExportHistory(string dialogFileName)
    {
       var j = JsonSerializer.Serialize(_history);
       File.WriteAllTextAsync(dialogFileName, j);
    }
}