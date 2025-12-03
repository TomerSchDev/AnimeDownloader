using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace AnimeBingeDownloader.Models;

public class TaskHistoryEntry : INotifyPropertyChanged
{
    [JsonPropertyName("id")]
    public string _id { get; set; } = string.Empty;
    
    [JsonPropertyName("title")]
    public string _title { get; set; } = string.Empty;
    
    [JsonPropertyName("episodesCompleted")]
    public int _episodesCompleted { get; set; }
    
    [JsonPropertyName("episodesFound")]
    public int _episodesFound { get; set; }
    
    [JsonPropertyName("elapsedTime")]
    public TimeSpan _elapsedTime { get; set; }
    
    [JsonPropertyName("priority")]
    public TaskPriority _taskPriority { get; set; }
    
    [JsonPropertyName("startTime")]
    public DateTime _startTime { get; set; }
    
    [JsonPropertyName("status")]
    public TaskStatus _taskStatus { get; set; }
    
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
    
    [JsonPropertyName("directory")]
    public string? Directory { get; set; }


    // Parameterless constructor for JSON deserialization
    [JsonConstructor]
    public TaskHistoryEntry()
    {
        _id = Guid.NewGuid().ToString();
        _startTime = DateTime.Now;
    }
    
    public TaskHistoryEntry(TaskViewModel task) : this()
    {
        _id = task.Id;
        _title = task.Title;
        _episodesCompleted = task.EpisodesCompleted;
        _episodesFound = task.EpisodesFound;
        _startTime = task.StartTime;
        _elapsedTime = DateTime.Now - task.StartTime;
        _taskPriority = task.Priority;
        _taskStatus = task.Status;
        Directory = task.Directory;
        Url = task.Url;
    }
    
    

    public void UpdateTime()
    {
        _elapsedTime = DateTime.Now - _startTime ;
        OnPropertyChanged(nameof(_elapsedTime));
    }

    
    // Method to create a copy of the entry
    public TaskHistoryEntry Clone()
    {
        return (TaskHistoryEntry)this.MemberwiseClone();
    }

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        if (propertyName != nameof(_elapsedTime))
        {
            UpdateTime();
        }
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}