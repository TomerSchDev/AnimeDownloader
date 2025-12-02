using System.Windows;
using AnimeBingeDownloader.Services;

namespace AnimeBingeDownloader.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly ConfigurationManager _configManager;
        private bool _hasUnsavedChanges = false;

        public SettingsWindow()
        {
            InitializeComponent();
            _configManager = ConfigurationManager.Instance;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load current settings
            DirectoryTextBox.Text = _configManager.DefaultDownloadDirectory;
            MaxDownloadsSlider.Value = _configManager.MaxConcurrentDownloads;
            MaxRetriesSlider.Value = _configManager.MaxRetryAttempts;
            CacheExpirationSlider.Value = _configManager.CacheExpirationHours;
            MinimizeBrowserCheckBox.IsChecked = _configManager.MinimizeBrowserWindow;
            AutoStartDownloadsCheckBox.IsChecked = _configManager.AutoStartDownloads;
            ShowNotificationsCheckBox.IsChecked = _configManager.ShowNotifications;

            // Update labels
            MaxDownloadsLabel.Text = MaxDownloadsSlider.Value.ToString();
            MaxRetriesLabel.Text = MaxRetriesSlider.Value.ToString();
            CacheExpirationLabel.Text = CacheExpirationSlider.Value.ToString();
            
            _hasUnsavedChanges = false;
        }

        private void SaveSettings()
        {
            _configManager.DefaultDownloadDirectory = DirectoryTextBox.Text;
            _configManager.MaxConcurrentDownloads = (int)MaxDownloadsSlider.Value;
            _configManager.MaxRetryAttempts = (int)MaxRetriesSlider.Value;
            _configManager.CacheExpirationHours = (int)CacheExpirationSlider.Value;
            _configManager.MinimizeBrowserWindow = MinimizeBrowserCheckBox.IsChecked ?? true;
            _configManager.AutoStartDownloads = AutoStartDownloadsCheckBox.IsChecked ?? true;
            _configManager.ShowNotifications = ShowNotificationsCheckBox.IsChecked ?? true;

            _hasUnsavedChanges = false;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                SelectedPath = DirectoryTextBox.Text,
                Description = "Select default download directory"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DirectoryTextBox.Text = dialog.SelectedPath;
                _hasUnsavedChanges = true;
            }
        }

        private void MaxDownloadsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MaxDownloadsLabel != null)
            {
                MaxDownloadsLabel.Text = ((int)e.NewValue).ToString();
                _hasUnsavedChanges = true;
            }
        }

        private void MaxRetriesSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MaxRetriesLabel != null)
            {
                MaxRetriesLabel.Text = ((int)e.NewValue).ToString();
                _hasUnsavedChanges = true;
            }
        }

        private void CacheExpirationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CacheExpirationLabel != null)
            {
                CacheExpirationLabel.Text = ((int)e.NewValue).ToString();
                _hasUnsavedChanges = true;
            }
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            _hasUnsavedChanges = true;
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to default values?",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                _configManager.ResetToDefaults();
                LoadSettings();
                
                MessageBox.Show(
                    "Settings have been reset to default values.",
                    "Settings Reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            
            MessageBox.Show(
                "Settings saved successfully!",
                "Success",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            
            DialogResult = true;
            Close();
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