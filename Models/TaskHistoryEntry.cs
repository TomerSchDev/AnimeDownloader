using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnimeBingeDownloader.Models;

public class TaskHistoryEntry
    : INotifyPropertyChanged
{
    private string _id;
    private string _title;
    
    private int _episodesCompleted = 0;
    private int _episodesFound = 0;
    private TimeSpan _elapsedTime;
    public TaskPriority _priority;
    private readonly DateTime _startTime;
    private TaskStatus _Status {get; set; }
    public string Url { get; } 
    public string? Directory { get; set; }

    public TaskHistoryEntry(TaskViewModel task)
    {
        _id = task.Id;
        _title = task.Title;
        _episodesCompleted = task.EpisodesCompleted;
        _episodesFound = task.EpisodesFound;
        _startTime = task.StartTime;
        _elapsedTime = DateTime.Now - task.StartTime;
        _priority = task.Priority;
        _Status = task.Status;
        Directory = task.Directory;
        Url = task.Url;
    }
    public string Id
    {
        get => _id;
        set
        {
            _id = value;
            OnPropertyChanged();
        }
    }

    public void UpdateTime()
    {
        _elapsedTime = DateTime.Now - _startTime ;
    }

    public string Priority => EnumTranslator.TranslateEnumToStr(_priority);

    public void UpdateEpisodesFound(int episodesFound)
    {
        _episodesFound = episodesFound;
    }

    public void UpdateEpisodesCompleted(int episodesCompleted)
    {
        _episodesCompleted = episodesCompleted;
    }
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    public int EpisodesCompleted
    {
        get => _episodesCompleted;
        set
        {
            _episodesCompleted = value;
            OnPropertyChanged();
        }
    }

    public int EpisodesFound
    {
        get => _episodesFound;
        set
        {
            _episodesFound = value;
            OnPropertyChanged();
        }
    }

    public TaskPriority GetPriority()
    {
        return _priority;
    }
    public void SetPriority(TaskPriority priority)
    {
        _priority = priority;
        OnPropertyChanged(nameof(_priority));
        OnPropertyChanged(nameof(Priority));
    }

    public void SetStatus(TaskStatus status)
    {
        _Status = status;
        OnPropertyChanged(nameof(_Status));
        OnPropertyChanged(nameof(Status));
    }

    public TaskStatus getStatus()
    {
        return _Status;
    }
    public string Status => EnumTranslator.TranslateEnumToStr(_Status);

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