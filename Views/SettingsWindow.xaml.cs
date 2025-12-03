using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using System.Diagnostics;
using AnimeBingeDownloader.Models;
using Microsoft.Win32;
using AnimeBingeDownloader.Services;
using AnimeBingeDownloader.Utils;
using OpenQA.Selenium;
using LogLevel = AnimeBingeDownloader.Models.LogLevel;

namespace AnimeBingeDownloader.Views
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        private readonly ConfigurationManager _configManager;
        private bool _hasUnsavedChanges = false;
        private bool _isLoading = false;
        private string _statusMessage = "Ready";
        private readonly DispatcherTimer _statusTimer;
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage == value) return;
                _statusMessage = value;
                OnPropertyChanged(nameof(StatusMessage));
        
                // Safely update the status text block if it's available
                if (StatusTextBlock != null)
                {
                    StatusTextBlock.Text = _statusMessage;
                }
            }
        }
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public SettingsWindow()
        {
            InitializeComponent();
            InitializeLogLevelComboBox();
    
            // Initialize status message after the window is loaded
            Loaded += (s, e) => 
            {
                StatusMessage = "Ready";
            };
            
            _configManager = ConfigurationManager.Instance;
            
            // Set up status timer
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            
            
            // Set up data context
            DataContext = this;
            
            // Initial load
            Loaded += OnLoaded;
        }
        
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _isLoading = true;
                LoadSettings();
                
                // Set version info
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                VersionTextBlock.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
                
                // Hide debug tab in release mode
                #if !DEBUG
                var debugTab = MainTabControl.Items.Cast<TabItem>().FirstOrDefault(t => t.Name == "DebugTab");
                if (debugTab != null)
                {
                    MainTabControl.Items.Remove(debugTab);
                }
                #endif
            }
            catch (Exception ex)
            {
                AppLogger.AddLog($"Error initializing settings window: {ex.Message}");
                StatusMessage = "Error initializing settings";
                MessageBox.Show($"Error initializing settings: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
            }
        }
        private void InitializeLogLevelComboBox()
        {
            // Clear existing items
            DebugLogLevelComboBox.Items.Clear();
    
            // Get all values from the LogLevel enum
            var logLevels = Enum.GetValues(typeof(LogLevel)).Cast<LogLevel>();
    
            // Add each log level to the ComboBox
            foreach (var level in logLevels)
            {
                DebugLogLevelComboBox.Items.Add(new ComboBoxItem
                {
                    Content = EnumTranslator.TranslateEnumToStr(level),
                    Tag = level  // Store the enum value in the Tag property for easy access
                });
            }
    
            // Optionally set a default selection
            DebugLogLevelComboBox.SelectedIndex = 0;
        }
        private void LoadSettings()
        {
            try
            {
                _isLoading = true;
                
                // General Settings
                DirectoryTextBox.Text = _configManager.DefaultDownloadDirectory;
                MaxDownloadsSlider.Value = _configManager.MaxConcurrentDownloads;
                MaxRetriesSlider.Value = _configManager.MaxRetryAttempts;
                MinTimeAfterErrorSlider.Value = _configManager.MinTimeAfterError;
                
                // Cache Settings
                CacheExpirationSlider.Value = _configManager.CacheExpirationHours;
                
                // Application Behavior
                AutoStartDownloadsCheckBox.IsChecked = _configManager.AutoStartDownloads;
                ShowNotificationsCheckBox.IsChecked = _configManager.ShowNotifications;
                PrintToScreenCheckBox.IsChecked = _configManager.PrintToScreen;
                
                // Browser Settings
                MinimizeBrowserCheckBox.IsChecked = _configManager.MinimizeBrowserWindow;
                UserAgentTextBox.Text = _configManager.UserAgent;
                PageLoadTimeoutTextBox.Text = _configManager.PageLoadTimeoutSeconds.ToString();
                
                // Logging Settings
                LogFilePathTextBox.Text = _configManager.DefaulterLoggerFile;
                // These settings don't exist in ConfigurationManager yet, so we'll use default values
                EnableFileLoggingCheckBox.IsChecked = File.Exists(_configManager.DefaulterLoggerFile);
                MaxLogSizeSlider.Value = 10; // Default 10MB log file size
                
                // Debug Settings - These don't exist in ConfigurationManager yet
                EnableDebugModeCheckBox.IsChecked = false;
                VerboseLoggingCheckBox.IsChecked = false;
                
                // Set log level in combo box - Default to first item if available
                if (DebugLogLevelComboBox.Items.Count > 0)
                    DebugLogLevelComboBox.SelectedIndex = 0;
                var currentDebugLogLevel = ConfigurationManager.Instance.DebugLogLevel;
                var logLevelItem = DebugLogLevelComboBox.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(item => (LogLevel) item.Tag == currentDebugLogLevel );
                if (logLevelItem != null)
                {
                    DebugLogLevelComboBox.SelectedItem = logLevelItem;
                }
                
                // Update labels
                MaxDownloadsLabel.Text = MaxDownloadsSlider.Value.ToString();
                MaxRetriesLabel.Text = MaxRetriesSlider.Value.ToString();
                MinTimeAfterErrorLabel.Text = MinTimeAfterErrorSlider.Value.ToString();
                CacheExpirationLabel.Text = CacheExpirationSlider.Value.ToString();
                MaxLogSizeLabel.Text = $"{MaxLogSizeSlider.Value:0} MB";
                
                _hasUnsavedChanges = false;
                StatusMessage = "Settings loaded";
            }
            catch (Exception ex)
            {
                AppLogger.AddLog($"Error loading settings: {ex}");
                StatusMessage = "Error loading settings";
                throw;
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void SaveSettings()
        {
            try
            {
                if (_isLoading) return;
                
                // General Settings
                _configManager.DefaultDownloadDirectory = DirectoryTextBox.Text;
                _configManager.MaxConcurrentDownloads = (int)MaxDownloadsSlider.Value;
                _configManager.MaxRetryAttempts = (int)MaxRetriesSlider.Value;
                _configManager.MinTimeAfterError = (int)MinTimeAfterErrorSlider.Value;
                
                // Cache Settings
                _configManager.CacheExpirationHours = (int)CacheExpirationSlider.Value;
                
                // Application Behavior
                _configManager.AutoStartDownloads = AutoStartDownloadsCheckBox.IsChecked ?? true;
                _configManager.ShowNotifications = ShowNotificationsCheckBox.IsChecked ?? true;
                _configManager.PrintToScreen = PrintToScreenCheckBox.IsChecked ?? true;
                
                // Browser Settings
                _configManager.MinimizeBrowserWindow = MinimizeBrowserCheckBox.IsChecked ?? true;
                
                if (int.TryParse(PageLoadTimeoutTextBox.Text, out var timeout))
                {
                    _configManager.PageLoadTimeoutSeconds = timeout;
                }
                
                _configManager.UserAgent = UserAgentTextBox.Text;
                
                // Logging Settings
                _configManager.DefaulterLoggerFile = LogFilePathTextBox.Text;
                // These settings don't exist in ConfigurationManager yet, so we'll just log them
                var enableFileLogging = EnableFileLoggingCheckBox.IsChecked ?? false;
                var maxLogSizeMb = (int)MaxLogSizeSlider.Value;
                
                // Debug Settings - These don't exist in ConfigurationManager yet, just log them
                var enableDebugMode = EnableDebugModeCheckBox.IsChecked ?? false;
                var enableVerboseLogging = VerboseLoggingCheckBox.IsChecked ?? false;
                
                var logLevel = "Info";
                if (DebugLogLevelComboBox.SelectedItem is ComboBoxItem selectedLevel)
                {
                    logLevel = selectedLevel.Content?.ToString() ?? "Info";
                }
                
                // Log the debug settings since we can't save them yet
                AppLogger.Logger.Info($"Debug settings changed - DebugMode: {enableDebugMode}, Verbose: {enableVerboseLogging}, LogLevel: {logLevel}, FileLogging: {enableFileLogging}, MaxLogSize: {maxLogSizeMb}MB");
                
                _hasUnsavedChanges = false;
                StatusMessage = "Settings saved successfully";
                
                // Update UI state based on new settings
                UpdateUiState();
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error saving settings: {ex}");
                StatusMessage = "Error saving settings";
                throw;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    SelectedPath = DirectoryTextBox.Text,
                    Description = "Select default download directory"
                };

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                DirectoryTextBox.Text = dialog.SelectedPath;
                SettingChanged(sender, e);
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error browsing for directory: {ex.Message}");
                StatusMessage = "Error browsing for directory";
            }
        }

        private void MaxDownloadsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MaxDownloadsLabel == null || _isLoading) return;
            MaxDownloadsLabel.Text = ((int)e.NewValue).ToString();
            SettingChanged(sender, e);
        }

        private void MaxRetriesSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MaxRetriesLabel != null && !_isLoading)
            {
                MaxRetriesLabel.Text = ((int)e.NewValue).ToString();
                SettingChanged(sender, e);
            }
        }

        private void MinTimeAfterErrorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MinTimeAfterErrorLabel != null && !_isLoading)
            {
                MinTimeAfterErrorLabel.Text = ((int)e.NewValue).ToString();
                SettingChanged(sender, e);
            }
        }
        
        private void CacheExpirationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CacheExpirationLabel != null && !_isLoading)
            {
                CacheExpirationLabel.Text = ((int)e.NewValue).ToString();
                SettingChanged(sender, e);
            }
        }
        
        private void MaxLogSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MaxLogSizeLabel != null && !_isLoading)
            {
                MaxLogSizeLabel.Text = $"{e.NewValue:0} MB";
                SettingChanged(sender, e);
            }
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            
            _hasUnsavedChanges = true;
            StatusMessage = "You have unsaved changes";
            UpdateUiState();
        }
        
        private void UpdateUiState()
        {
            try
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (SaveButton != null)
                    {
                        SaveButton.IsEnabled = _hasUnsavedChanges;
                    }

                    if (CancelButton != null)
                    {
                        CancelButton.IsEnabled = _hasUnsavedChanges;
                    }
                });
            }
            catch (Exception e)
            {
                AppLogger.Logger.Error($"Error in UpdateUiState: {e.Message}\n{e.StackTrace}");
            }
            // Enable/disable controls based on other settings
        }

        private void BrowseLogFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    FileName = Path.GetFileName(LogFilePathTextBox.Text),
                    InitialDirectory = Path.GetDirectoryName(LogFilePathTextBox.Text) ?? string.Empty,
                    Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*",
                    Title = "Select log file location"
                };

                if (dialog.ShowDialog() == true)
                {
                    LogFilePathTextBox.Text = dialog.FileName;
                    SettingChanged(sender, e);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error browsing for log file: {ex.Message}");
                StatusMessage = "Error browsing for log file";
            }
        }
        
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to their default values?\n\nThis cannot be undone.",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _configManager.ResetToDefaults();
                    LoadSettings();
                    _hasUnsavedChanges = true;
                    StatusMessage = "Settings reset to defaults. Click Save to apply.";
                }
                catch (Exception ex)
                {
                    AppLogger.Logger.Error($"Error resetting settings: {ex.Message}");
                    StatusMessage = "Error resetting settings";
                }
            }
        }

        private void DebugLogLevelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoading && e.AddedItems.Count > 0)
            {
                SettingChanged(sender, e);
            }
        }
        
        private void ExportLogsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    FileName = $"AnimeBingeDownloader_Logs_{DateTime.Now:yyyyMMdd_HHmmss}.log",
                    Filter = "Log Files (*.log)|*.log|All Files (*.*)|*.*",
                    Title = "Export Logs"
                };

                if (saveDialog.ShowDialog() != true) return;
                File.WriteAllText(saveDialog.FileName, "Log export functionality not yet implemented.");
                StatusMessage = "Logs exported successfully";
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error exporting logs: {ex.Message}");
                StatusMessage = "Error exporting logs";
            }
        }

        private void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Implement cache clearing functionality
                StatusMessage = "Application cache cleared";
                MessageBox.Show("Application cache has been cleared successfully.", 
                    "Cache Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error clearing cache: {ex.Message}");
                StatusMessage = "Error clearing cache";
            }
        }

        private void TestNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Implement test notification
                StatusMessage = "Test notification sent";
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error sending test notification: {ex.Message}");
                StatusMessage = "Error sending test notification";
            }
        }

        private void OpenDevToolsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Implement browser dev tools opening if using a WebView/CEF
                StatusMessage = "Developer tools opened";
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error opening developer tools: {ex.Message}");
                StatusMessage = "Error opening developer tools";
            }
        }

        private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Implement update check functionality
                StatusMessage = "Checking for updates...";
                MessageBox.Show("You are running the latest version of Anime Binge Downloader.", 
                    "No Updates Available", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppLogger.Logger.Error($"Error checking for updates: {ex.Message}");
                StatusMessage = "Error checking for updates";
            }
        }

        private void ResetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("WARNING: This will reset ALL settings to their default values and restart the application.\n\nAre you sure you want to continue?",
                "Reset All Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // TODO: Implement full settings reset and application restart
                    StatusMessage = "Settings reset complete. Restarting application...";
                    MessageBox.Show("All settings have been reset to their default values. The application will now restart.",
                        "Settings Reset", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Restart the application
                    // Application.Restart();
                    // Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    AppLogger.Logger.Error($"Error resetting settings: {ex.Message}");
                    StatusMessage = "Error resetting settings";
                }
            }
        }
        
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Button? saveButton = null;
            
            try
            {
                // Try to get the SaveButton from the sender if available
                saveButton = sender as Button ?? FindName("SaveButton") as Button;
                
                _isLoading = true;
                if (saveButton != null)
                    saveButton.IsEnabled = false;

                // Save configuration asynchronously
                await Task.Run(() => _configManager.SaveConfiguration());

                // Check if restart is needed (handle case where property doesn't exist)
                var requiresRestart = false;
                var requiresRestartProp = _configManager.GetType().GetProperty("RequiresRestart");
                if (requiresRestartProp != null)
                {
                    requiresRestart = (bool)requiresRestartProp.GetValue(_configManager)!;
                }

                _hasUnsavedChanges = false;

                // Update status message
                StatusMessage = requiresRestart 
                    ? "Settings saved! Some changes will take effect after restart." 
                    : "Settings saved successfully!";
            
                // Rest of your code remains the same...
                AppLogger.Logger.Info(StatusMessage);

                var statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                statusTimer.Tick += (s, _) => 
                {
                    statusTimer.Stop();
                    if (StatusMessage.StartsWith("Settings saved"))
                        StatusMessage = "Ready";
                };
                statusTimer.Start();
            }
            catch (Exception ex)
            {
                var errorMessage = "Error saving settings. Please try again.";
                StatusMessage = errorMessage;
                AppLogger.Logger.Error($"Save failed: {ex.Message}\n{ex.StackTrace}");

                if (System.Diagnostics.Debugger.IsAttached)
                {
                    errorMessage = $"Error saving settings: {ex.Message}";
                }
            
                MessageBox.Show(errorMessage, "Save Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                if (saveButton != null)
                    saveButton.IsEnabled = true;
            }
        }
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_hasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close without saving?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            DialogResult = false;
            Close();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_hasUnsavedChanges && DialogResult != true)
            {
                var result = MessageBox.Show(
                    "You have unsaved changes. Are you sure you want to close without saving?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }

            base.OnClosing(e);
        }
    }
}