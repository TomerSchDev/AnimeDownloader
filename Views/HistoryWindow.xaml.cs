using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Services;
using AnimeBingeDownloader.Views.Converters;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;

namespace AnimeBingeDownloader.Views
{
    public partial class HistoryWindow : Window, INotifyPropertyChanged
    {
        private readonly TaskHistoryManager _historyManager;
        private readonly ObservableCollection<TaskHistoryEntry> _displayedHistory;
        private readonly DispatcherTimer _updateTimer;
        private string _currentFilter = "All";
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public HistoryWindow()
        {
            InitializeComponent();
            
            _historyManager = TaskHistoryManager.Instance;
            _displayedHistory = new ObservableCollection<TaskHistoryEntry>();
            
            // Set up the data binding
            HistoryDataGrid.ItemsSource = _displayedHistory;
            
            // Set up the filter combobox
            var statuses = new List<string> { "All" };
            statuses.AddRange(Enum.GetValues(typeof(TaskStatus))
                .Cast<TaskStatus>()
                .Select(s => EnumTranslator.TranslateEnumToStr(s))
                .Distinct()
                .OrderBy(s => s));
            
            StatusFilterComboBox.ItemsSource = statuses;
            StatusFilterComboBox.SelectedIndex = 0;
            
            // Set up the update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            
            // Initial load
            LoadHistory();
        }
        
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            // Update the elapsed time for all entries
            foreach (var entry in _displayedHistory)
            {
                entry.UpdateTime();
            }
        }
        
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            _updateTimer.Start();
        }
        
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            _updateTimer.Stop();
        }

        private void LoadHistory()
        {
            try
            {
                var history = _historyManager.GetAllHistory();
                _displayedHistory.Clear();
                
                // Sort by start time (newest first)
                var sortedHistory = history.OrderByDescending(h => h._startTime);
                
                foreach (var entry in sortedHistory)
                {
                    _displayedHistory.Add(entry);
                }
                
                // Subscribe to property changes for live updates
                foreach (var entry in _displayedHistory)
                {
                    entry.PropertyChanged += Entry_PropertyChanged;
                }
                
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading history: {ex.Message}");
            }
        }
        
        private void Entry_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If the status changed, we might need to update the filtered view
            if (e.PropertyName == nameof(TaskHistoryEntry._taskStatus))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyFilter();
                }), DispatcherPriority.Background);
            }
        }

        private void UpdateStatusBar()
        {
            try
            {
                if (StatusTextBlock != null)
                {
                    var total = _displayedHistory?.Count ?? 0;
                    var statusText = _currentFilter == "All" 
                        ? $"Showing all {total} entries"
                        : $"Showing {total} entries with status: {_currentFilter}";
                    
                    StatusTextBlock.Text = statusText;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating status bar: {ex.Message}");
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) // Only apply filter if the window is loaded
            {
                ApplyFilter();
            }
        }

        private void ApplyFilter()
        {
            if (StatusFilterComboBox?.SelectedItem == null) return;
            
            _currentFilter = StatusFilterComboBox.SelectedItem.ToString();
            
            try
            {
                IEnumerable<TaskHistoryEntry> filteredHistory;
                
                if (string.IsNullOrEmpty(_currentFilter) || _currentFilter == "All")
                {
                    filteredHistory = _historyManager.GetAllHistory();
                }
                else
                {
                    var status = (TaskStatus)EnumTranslator.Parse(TaskStatus.Completed, _currentFilter);
                    filteredHistory = _historyManager.GetHistoryByStatus(status);
                }
                
                // Get the current selection to restore it after update
                var selectedItem = HistoryDataGrid.SelectedItem as TaskHistoryEntry;
                
                // Update the collection
                _displayedHistory.Clear();
                foreach (var entry in filteredHistory.OrderByDescending(h => h._startTime))
                {
                    _displayedHistory.Add(entry);
                }
                
                // Restore selection if possible
                if (selectedItem != null && _displayedHistory.Contains(selectedItem))
                {
                    HistoryDataGrid.SelectedItem = selectedItem;
                    HistoryDataGrid.ScrollIntoView(selectedItem);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying filter: {ex.Message}");
            }
            finally
            {
                UpdateStatusBar();
            }
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryDataGrid.SelectedItem is TaskHistoryEntry entry)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the entry for '{entry._title}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result != MessageBoxResult.Yes) return;
                if (!_historyManager.DeleteTask(entry._id)) return;
                _displayedHistory.Remove(entry);
                UpdateStatusBar();
                        
                MessageBox.Show(
                    "Entry deleted successfully.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            else
            {
                MessageBox.Show(
                    "Please select an entry to delete.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryDataGrid.SelectedItem is TaskHistoryEntry entry)
            {
                try
                {
                    Clipboard.SetText(entry.Url);
                    MessageBox.Show(
                        "URL copied to clipboard!",
                        "Success",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error copying URL: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            else
            {
                MessageBox.Show(
                    "Please select an entry first.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void OpenDirectory_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryDataGrid.SelectedItem is TaskHistoryEntry entry)
            {
                try
                {
                    if (Directory.Exists(entry.Directory))
                    {
                        Process.Start("explorer.exe", entry.Directory);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Directory not found: {entry.Directory}",
                            "Directory Not Found",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"Error opening directory: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
            else
            {
                MessageBox.Show(
                    "Please select an entry first.",
                    "No Selection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to clear all history? This action cannot be undone.",
                "Confirm Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result != MessageBoxResult.Yes) return;
            _historyManager.ClearHistory();
            _displayedHistory.Clear();
            UpdateStatusBar();
                
            MessageBox.Show(
                "History cleared successfully.",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}