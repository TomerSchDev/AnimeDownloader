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

        public MainWindow()
        {
            InitializeComponent();
            
            // Initialize task collection
            _tasks = [];
            TaskDataGrid.ItemsSource = _tasks;

            // Set default download directory
            var defaultDownloadDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads",
                "AnimeHeaven"
            );
            DirectoryTextBox.Text = defaultDownloadDirectory;

            // Start periodic UI update timer
            var updateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        #region Event Handlers - New Task Tab

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = DirectoryTextBox.Text,
                Description = "Select download directory"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DirectoryTextBox.Text = dialog.SelectedPath;
            }
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
            var task = new TaskViewModel(url, directory);
            _tasks.Add(task);
            _taskDictionary.Add(task.Id, task);
            // Log message
            LogMessage($"Task {task.Id} started. Monitoring in Task Manager.");

            // Switch to Task Manager tab
            MainTabControl.SelectedIndex = 1;

            // Start the task processing in background
            _ = Task.Run(async () =>
            {
                var coordinator = new TaskCoordinator(task);
                await coordinator.ExecuteTaskAsync();
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

            // 2. Toggle the visibility of the detail panel.
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
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string formattedMessage = $"[{timestamp}] [GLOBAL] {message}\n";
            
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
            }

            // If viewing a task log, refresh it
            if (MainTabControl.SelectedIndex == 2 && _selectedTask != null)
            {
                UpdateLogView();
            }
        }
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            foreach (var task in _tasks)
            {
                task.CleanScrape();
            }
            DownloadService.Instance.Shutdown();
            base.OnClosing(e);
        }

        #endregion
    }
}