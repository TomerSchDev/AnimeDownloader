using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Services;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;

namespace AnimeBingeDownloader.Views
{
    public partial class HistoryWindow : Window
    {
        private readonly TaskHistoryManager _historyManager;
        private readonly ObservableCollection<TaskHistoryEntry> _displayedHistory;

        public HistoryWindow()
        {
            InitializeComponent();
            _historyManager = TaskHistoryManager.Instance;
            _displayedHistory = new ObservableCollection<TaskHistoryEntry>();
            HistoryDataGrid.ItemsSource = _displayedHistory;
            
            LoadHistory();
        }

        private void LoadHistory()
        {
            var history = _historyManager.GetAllHistory();
            _displayedHistory.Clear();
            
            foreach (var entry in history)
            {
                _displayedHistory.Add(entry);
            }
            
            UpdateStatusBar();
        }

        private void UpdateStatusBar()
        {
            StatusTextBlock.Text = $"Total entries: {_displayedHistory.Count}";
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void StatusFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (StatusFilterComboBox.SelectedItem is not ComboBoxItem selectedItem) return;
            var filterText = selectedItem.Content.ToString();
            List<TaskHistoryEntry> filteredHistory;

            if (filterText == "All")
            {
                filteredHistory = _historyManager.GetAllHistory();
            }
            else
            {
                var status = (TaskStatus) EnumTranslator.Parse(TaskStatus.Completed,filterText);
                filteredHistory = _historyManager.GetHistoryByStatus(status);
            }

            _displayedHistory.Clear();
            foreach (var entry in filteredHistory)
            {
                _displayedHistory.Add(entry);
            }

            UpdateStatusBar();
        }

        private void DeleteEntry_Click(object sender, RoutedEventArgs e)
        {
            if (HistoryDataGrid.SelectedItem is TaskHistoryEntry entry)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete the entry for '{entry.Title}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result != MessageBoxResult.Yes) return;
                if (!_historyManager.DeleteTask(entry.Id)) return;
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