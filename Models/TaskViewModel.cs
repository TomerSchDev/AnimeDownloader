using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.Swift;
using System.Text;
using System.Windows;
using AnimeBingeDownloader.Services;

namespace AnimeBingeDownloader.Models;

public class TaskViewModel : INotifyPropertyChanged
    {
        private string _id = null!;
        private string _url;
        private string _directory;
        private string _title;
        private int _episodesFound;
        private int _episodesCompleted;
        private int _episodesScraped;
        private TaskStatus _status;
        private TaskPriority _priority;
        private readonly DateTime _startTime;
        private string _elapsedTime;
        private readonly Logger _logger;
        private readonly List<string> _episodeErrors;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private ScrapingService? _scraper = null;
        public ObservableCollection<EpisodeTask> EpisodeTasks { get; } = new();
        public readonly MegaLogger _megaLogger;

        public string StatusStr => EnumTranslator.TranslateEnumToStr(_status);
        public string PriorityStr => EnumTranslator.TranslateEnumToStr(_priority);
        public DateTime StartTime => _startTime;
        public TaskViewModel(string url, string directory, MegaLogger megaLogger)
        {
            var s = Guid.NewGuid().ToString("N");
            if (s.Length >= 8) _id = s[..8];
            _url = url;
            _directory = directory;
            _title = "N/A";
            _episodesFound = 0;
            _episodesCompleted = 0;
            _episodesScraped = 0;
            _status = TaskStatus.Queued;
            _priority = TaskPriority.Medium;
            _startTime = DateTime.Now;
            _elapsedTime = "--:--:--";
            _episodeErrors = [];
            _logger = new Logger($"[TASK {Id}] ");
            _megaLogger = megaLogger;
            _megaLogger.Subscribe(_logger);
            _logger.AddLog($"--- LOG FOR TASK {Id}: {Title} ---");
           

        }

        #region Properties

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                OnPropertyChanged();
            }
        }

        public string Directory
        {
            get => _directory;
            set
            {
                _directory = value;
                OnPropertyChanged();
            }
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

        public int EpisodesFound
        {
            get => _episodesFound;
            set
            {
                _episodesFound = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EpisodesScrapedDisplay));
                OnPropertyChanged(nameof(EpisodesDownloadedDisplay));
            }
        }

        public int EpisodesCompleted
        {
            get => _episodesCompleted;
            set
            {
                _episodesCompleted = value;
                CheckIfDownloadAll();
                OnPropertyChanged();
                OnPropertyChanged(nameof(EpisodesDownloadedDisplay));
            }
        }

        private void CheckIfDownloadAll()
        {
            if (_episodesCompleted < _episodesFound) return;
            UpdateStatus(TaskStatus.Completed,"Finished Downloading all episodes");
            var endtime = DateTime.Now;
            var workTime = endtime - _startTime;
            AddLog($"Task finished in {workTime}");
            AddLog("-------------------------------");
            AddLog($"Episodes that had errors: {_episodeErrors.Count}");
            

        }

        public int EpisodesScraped
        {
            get => _episodesScraped;
            set
            {
                _episodesScraped = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EpisodesScrapedDisplay));
            }
        }

        public TaskStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusStr));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsTerminalState));
            }
        }

        public TaskPriority Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PriorityStr));
            }
        }

        public string ElapsedTime
        {
            get => _elapsedTime;
            set
            {
                _elapsedTime = value;
                OnPropertyChanged();
            }
        }

        public string EpisodesScrapedDisplay => $"{EpisodesScraped}/{EpisodesFound}";
        
        public string EpisodesDownloadedDisplay => $"{EpisodesCompleted}/{EpisodesFound}";

        public bool IsRunning => Status != TaskStatus.Completed && 
                                 Status != TaskStatus.Canceled && 
                                 Status != TaskStatus.Error;

        public bool IsTerminalState => Status is TaskStatus.Completed or TaskStatus.Canceled or TaskStatus.Error;

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        #endregion

        #region Methods

        public void AddLog(string message)
        {
           _logger.AddLog(message);
        }

        public void UpdateStatus(TaskStatus newStatus, string? logMessage = null)
        {
            Status = newStatus;
            
            if (!string.IsNullOrEmpty(logMessage))
            {
                AddLog(logMessage);
            }

            if (!IsTerminalState) return;
            var endTime = DateTime.Now;
            var elapsed = endTime - _startTime;
                
            AddLog($"Task Finished Scrapping ended at {endTime:HH:mm:ss}");
            AddLog($"Total elapsed time: {elapsed:hh\\:mm\\:ss}");
            AddLog("----------------------------------------");
            AddLog($"Total errors episodes: {_episodeErrors.Count}");
                
            var errorEpisodes = _episodeErrors.Count > 0 
                ? string.Join(", E", _episodeErrors) 
                : "None";
            AddLog($"Episodes with errors: {errorEpisodes}");
        }

        public void AddError(string episodeNumber)
        {
            _episodeErrors.Add(episodeNumber);
            _episodesCompleted++;
            OnPropertyChanged(nameof(_episodeErrors));
        }

        public void UpdateElapsedTime()
        {
            if (_startTime == default) return;
            var elapsed = DateTime.Now - _startTime;
            ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
        }

        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
            UpdateStatus(TaskStatus.Canceled, "Cancellation requested.");
        }

        public List<Logger> Loggers()
        {
            var loggers = EpisodeTasks
                .Select(episode => episode.GetLogger())
                .ToList();
            if (_scraper is not null) loggers.Add(_scraper?.Logger!);
            loggers.Add(_logger);
            return loggers;
        }
        public string GetFullLog()
        {
            
            var logs = Logger.GetMargeLoggers(Loggers());
            return string.Join(Environment.NewLine, logs);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        public void AddEpisode(EpisodeTask episode)
        {
            _megaLogger.Subscribe(episode.GetLogger());
            // CRITICAL: The Dispatcher ensures this code runs on the UI thread.
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 1. Safely add the episode to the ObservableCollection.
                    // This triggers the DataGrid to update.
                    EpisodeTasks.Add(episode);
                    
                    // 2. Safely increment the counter.
                    // This triggers the OnPropertyChanged for EpisodesScraped and EpisodesScrapedDisplay.
                    EpisodesScraped++; 
                });
            }
            else
            {
                // Fallback for non-WPF environments (shouldn't happen here, but safe)
                EpisodeTasks.Add(episode);
                EpisodesScraped++;
            }
        }
        public void UpdateTaskPriority()
        {
            var downloadServiceInstance = DownloadService.Instance;
            foreach (var episode in EpisodeTasks)
            {
                downloadServiceInstance.UpdatePriority(episode, Priority);
            }
        }

        public void AddScrapper(ScrapingService scraper)
        {
            _scraper = scraper;
        }

        public void CleanScrape()
        {
            if (_scraper == null) return;
            _scraper.Dispose();
            _scraper = null;
        }
    }