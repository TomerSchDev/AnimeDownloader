using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Services;
using Microsoft.Win32;

namespace AnimeBingeDownloader.Views
{
    
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<TaskViewModel> _tasks;
        private readonly Dictionary<string,TaskViewModel> _taskDictionary = new();
        private TaskViewModel? _selectedTask;
        private readonly ConfigurationManager _configManager;
        private readonly TaskHistoryManager _historyManager;
        private string logCallBack;
        public static MegaLogger Logger = new();
        

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize managers
            _configManager = ConfigurationManager.Instance;
            _historyManager = TaskHistoryManager.Instance;
            
            // Initialize task collection
            _tasks = [];
            TaskDataGrid.ItemsSource = _tasks;

            // Set default download directory from configuration
            DirectoryTextBox.Text = _configManager.DefaultDownloadDirectory;
            List<Logger> loggers =
            [
                CacheService.Logger,
                ConfigurationManager.Instance.Logger,
                PeriodicCallerService.Instance.Logger,
                TaskHistoryManager.Instance.Logger,
            ];
           
            // Start periodic UI update timer
            var updateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
            //logCallBack = PeriodicCallerService.Instance.AddNewCall(LogCallBack, null, 0, 1);

            // Load previous session (optional - uncomment if you want to restore tasks)
            // LoadPreviousSession();
        }

        private void LogCallBack(object? o)
        {
            var logFile = ConfigurationManager.Instance.DefaulterLoggerFile;
            var logs = string.Join(Environment.NewLine, Logger.GetNewSortedMegaLog());
            File.WriteAllText(logFile, logs);
        }

        public void SaveHistory()
        {
            foreach (var task in _tasks)
            {
                _historyManager.SaveTask(task);
            }
        }
        #region Event Handlers - New Task Tab

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = DirectoryTextBox.Text,
                Description = "Select download directory"
            };

            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            DirectoryTextBox.Text = dialog.SelectedPath;
                
            // Update default directory in config
            _configManager.DefaultDownloadDirectory = dialog.SelectedPath;
        }

        private async void StartTaskButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text.Trim();
            var directory = DirectoryTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show(
                    "Please enter a valid Anime URL.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // Create new task
            var task = new TaskViewModel(url, directory,Logger);
            _tasks.Add(task);
            _taskDictionary.Add(task.Id, task);
            
            // Save to history immediately
            _historyManager.SaveTask(task);
            
            // Log message
            LogMessage($"Task {task.Id} started. Monitoring in Task Manager.");

            // Switch to Task Manager tab
            MainTabControl.SelectedIndex = 1;
            
            // Start the task processing in background
            _ = Task.Run(async () =>
            {
                var coordinator = new TaskCoordinator(task);
                await coordinator.ExecuteTaskAsync();
                
                // Save to history when completed
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _historyManager.SaveTask(task);
                });
            });
        }

        public TaskViewModel GetTask(string id)
        {
            return _taskDictionary[id];
        }
        #endregion

        #region Event Handlers - Task Manager Tab

        private void TaskDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TaskDataGrid.SelectedItem is TaskViewModel task)
            {
                _selectedTask = task;
            }
        }

        private void TaskDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedTask == null ||TaskDataGrid.SelectedItem == null) return;

            // Toggle the visibility of the detail panel.
            if (TaskDetailPanel.Visibility == Visibility.Visible)
            {
                // Hide the panel on a second double-click.
                //TaskDetailPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                var selected = TaskDataGrid.SelectedItem;
                if (selected is EpisodeTask) return;
                var task = (TaskViewModel)selected;
                TaskDetailPanel.ItemsSource = task.EpisodeTasks;
                TaskDetailPanel.Visibility = Visibility.Visible;
            }
        }

        private void CancelTask_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show(
                    "Please select a task from the list first.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            if (_selectedTask.IsRunning)
            {
                _selectedTask.Cancel();
                LogMessage($"Cancellation requested for task {_selectedTask.Id}.");
                
                // Save updated state to history
                _historyManager.SaveTask(_selectedTask);
            }
            else
            {
                MessageBox.Show(
                    $"Task {_selectedTask.Id} is already {_selectedTask.Status}.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void ViewLog_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show(
                    "Please select a task from the list first.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            // Switch to log tab and update content
            MainTabControl.SelectedIndex = 2;
            UpdateLogView();
        }

        #endregion

        #region Event Handlers - Context Menu

        private void SetPriorityHigh_Click(object sender, RoutedEventArgs e)
        {
            SetTaskPriority(TaskPriority.High);
        }

        private void SetPriorityMedium_Click(object sender, RoutedEventArgs e)
        {
            SetTaskPriority(TaskPriority.Medium);
        }

        private void SetPriorityLow_Click(object sender, RoutedEventArgs e)
        {
            SetTaskPriority(TaskPriority.Low);
        }

        private void TogglePause_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show(
                    "Please select a task first.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            if (_selectedTask.IsTerminalState)
            {
                MessageBox.Show(
                    $"Task {_selectedTask.Id} is already in a terminal state ({_selectedTask.Status}).",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            if (_selectedTask.Priority == TaskPriority.Pause)
            {
                // Resume
                SetTaskPriority(TaskPriority.Medium);
                _selectedTask.AddLog("Task resumed from PAUSE (set to MEDIUM priority).");
            }
            else
            {
                // Pause
                SetTaskPriority(TaskPriority.Pause);
                _selectedTask.AddLog("Task PAUSED. Will be skipped by workers.");
            }
            
            // Save updated state
            _historyManager.SaveTask(_selectedTask);
        }

        #endregion

        #region Event Handlers - Log Tab

        private void ExportLog_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show(
                    "Please select a task from the list first.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            var dialog = new SaveFileDialog
            {
                FileName = $"task_{_selectedTask.Id}_log.txt",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt = ".txt"
            };

            if (dialog.ShowDialog() != true) return;
            try
            {
                File.WriteAllText(dialog.FileName, _selectedTask.GetFullLog());
                MessageBox.Show(
                    $"Log exported successfully to {dialog.FileName}",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error exporting log: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        #endregion

        #region Private Helper Methods

        private void SetTaskPriority(TaskPriority newPriority)
        {
            if (_selectedTask == null)
            {
                MessageBox.Show(
                    "Please select a task first.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            if (_selectedTask.IsTerminalState)
            {
                MessageBox.Show(
                    $"Cannot change priority for task in status: {_selectedTask.Status}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            _selectedTask.Priority = newPriority;
            _selectedTask.AddLog($"Priority changed to {newPriority}.");
            _selectedTask.UpdateTaskPriority();
        }

        private void UpdateLogView()
        {
            if (_selectedTask == null)
                return;

            LogTextBox.Text = _selectedTask.GetFullLog();
            LogTextBox.ScrollToEnd();
        }

        private void LogMessage(string message)
        {
            // Global log message (shown in log tab)
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var formattedMessage = $"[{timestamp}] [GLOBAL] {message}\n";
            
            LogTextBox.AppendText(formattedMessage);
            LogTextBox.ScrollToEnd();
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Update elapsed time for running tasks
            foreach (var task in _tasks)
            {
                if (task.IsRunning)
                {
                    task.UpdateElapsedTime();
                }
                
                // Periodically save running tasks to history
                if (task.IsRunning && DateTime.Now.Second % 30 == 0)
                {
                    _historyManager.SaveTask(task);
                }
            }

            // If viewing a task log, refresh it
            if (MainTabControl.SelectedIndex == 2 && _selectedTask != null)
            {
                UpdateLogView();
            }
        }
        
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Save all tasks to history before closing
            foreach (var task in _tasks)
            {
                task.CleanScrape();
                _historyManager.SaveTask(task);
            }
            
            DownloadService.Instance.Shutdown();
            TaskHistoryManager.Instance.Close();
            PeriodicCallerService.Instance.RemoveCall(logCallBack);
            PeriodicCallerService.Instance.ClearCalls();
            base.OnClosing(e);
        }

        // Optional: Load previous session tasks
        private void LoadPreviousSession()
        {
            var history = _historyManager.GetAllHistory();
            var recentTasks = history.Take(10); // Load last 10 tasks
            
            // You can implement UI to show these and allow resuming
            LogMessage($"Found {history.Count} tasks in history.");
        }

        #endregion

        #region Menu Event Handlers

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow
            {
                Owner = this
            };

            if (settingsWindow.ShowDialog() != true) return;
            // Settings were saved, update UI if needed
            DirectoryTextBox.Text = _configManager.DefaultDownloadDirectory;
            LogMessage("Settings updated successfully.");
        }

        private void ViewHistory_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new HistoryWindow
            {
                Owner = this
            };
            
            historyWindow.ShowDialog();
        }

        private void ExportHistory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                FileName = $"task_history_{DateTime.Now:yyyyMMdd_HHmmss}.json",
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt = ".json"
            };

            if (dialog.ShowDialog() != true) return;
            try
            {
                _historyManager.ExportHistory(dialog.FileName);
                MessageBox.Show(
                    $"History exported successfully to {dialog.FileName}",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error exporting history: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Anime Downloader v1.0\n\n" +
                "A powerful WPF application for downloading anime episodes.\n\n" +
                "Features:\n" +
                "• Multi-threaded downloads\n" +
                "• Priority queue management\n" +
                "• Smart caching\n" +
                "• Resume support\n" +
                "• Task history tracking\n\n" +
                "For educational purposes only.",
                "About Anime Downloader",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}