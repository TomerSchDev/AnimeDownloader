using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Utils;
using AnimeBingeDownloader.Views;

namespace AnimeBingeDownloader.Services
{
    /// <summary>
    /// Manages application configuration and user preferences
    /// </summary>
    public enum BrowserType
    {
        Chrome,
        Firefox,
        Edge
    }

    public sealed class ConfigurationManager : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private static ConfigurationManager? _instance;
        private static readonly Lock InstanceLock = new();
        
        private const string ConfigFileName = "app_config.json";
        private AppConfiguration _config;
        private readonly string _configPath;
        private Logger Logger { get; } = new Logger("ConfigurationManager");

        public static ConfigurationManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                lock (InstanceLock)
                {
                    _instance ??= new ConfigurationManager();
                }
                return _instance;
            }
        }

        private ConfigurationManager()
        {
            try
            {
                // Store just the filename, not the full path
                _configPath = ConfigFileName;
                _config = LoadConfiguration();
                
                // Only subscribe to logger after initialization is complete
                AppLogger.MegaLogger.Subscribe(Logger,_config.PrintToScreen,_config.LogToFile,_config.DefaulterLoggerFile);
                Logger.Info("ConfigurationManager initialized successfully");
                Logger.Info($"Configuration file location: {AppStorageService.GetPath(_configPath)}");
            }
            catch (Exception ex)
            {
                // Fallback to console if logging isn't available yet
                Console.WriteLine($"Error initializing ConfigurationManager: {ex.Message}");
                throw;
            }
        }

        private void LogPropertyChange<T>(string propertyName,T oldValue, T newValue)
        {
            if(oldValue!.Equals(newValue)) return;
            Logger.Debug($"Property {propertyName} changed from {oldValue} to {newValue}");
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            SaveConfiguration();
        }

        #region Browser Settings

        public BrowserType BrowserType
        {
            get => _config.BrowserType;
            set
            {
                LogPropertyChange(nameof(BrowserType), _config.BrowserType, value);
                _config.BrowserType = value;
                OnPropertyChanged();
            }
        }

        public bool HeadlessMode
        {
            get => _config.HeadlessMode;
            set
            {
                LogPropertyChange(nameof(HeadlessMode), _config.HeadlessMode, value);
                _config.HeadlessMode = value;
                OnPropertyChanged();
            }
        }

        public bool DisableImages
        {
            get => _config.DisableImages;
            set
            {
                LogPropertyChange(nameof(DisableImages), _config.DisableImages, value);
                _config.DisableImages = value;
                OnPropertyChanged();
            }
        }

        public bool DisableJavaScript
        {
            get => _config.DisableJavaScript;
            set
            {
                LogPropertyChange(nameof(DisableJavaScript), _config.DisableJavaScript, value);
                _config.DisableJavaScript = value;
                OnPropertyChanged();
            }
        }

        public int PageLoadTimeout
        {
            get => _config.PageLoadTimeout;
            set
            {
                var newValue = Math.Max(10, Math.Min(300, value)); // Limit between 10 and 300 seconds
                LogPropertyChange(nameof(PageLoadTimeout), _config.PageLoadTimeout, newValue);
                _config.PageLoadTimeout = newValue;
                OnPropertyChanged();
            }
        }

        public string UserDataDir
        {
            get => _config.UserDataDir;
            set
            {
                LogPropertyChange(nameof(UserDataDir), _config.UserDataDir, value);
                _config.UserDataDir = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Configuration Properties

        public string DefaultDownloadDirectory
        {
            get => _config.DefaultDownloadDirectory;
            set
            {
                LogPropertyChange(nameof(DefaultDownloadDirectory), _config.DefaultDownloadDirectory, value);
                _config.DefaultDownloadDirectory = value;
                OnPropertyChanged();
            }
        }
        public string DefaulterHistoryFIle
        {
            get => _config.DefaulterHistoryFIle;
            set
            {
                LogPropertyChange(nameof(DefaulterHistoryFIle), _config.DefaulterHistoryFIle, value);
                _config.DefaulterHistoryFIle = value;
                OnPropertyChanged();
            }
        }
        public int MaxConcurrentDownloads
        {
            get => _config.MaxConcurrentDownloads;
            set
            {
                var newValue = Math.Max(1, Math.Min(10, value));
                LogPropertyChange(nameof(MaxConcurrentDownloads), _config.MaxConcurrentDownloads, newValue);
                _config.MaxConcurrentDownloads = newValue;
                OnPropertyChanged();
            }
        }

        public int MaxRetryAttempts
        {
            get => _config.MaxRetryAttempts;
            set
            {
                var newValue = Math.Max(1, Math.Min(10, value));
                LogPropertyChange(nameof(MaxRetryAttempts), _config.MaxRetryAttempts, newValue);
                _config.MaxRetryAttempts = newValue;
                OnPropertyChanged();
            }
        }
        public int MinTimeAfterError
        {
            get => _config.MinTimeAfterError;
            set
            {
                var newValue = Math.Max(1, Math.Min(10, value));
                LogPropertyChange(nameof(MinTimeAfterError), _config.MinTimeAfterError, newValue);

                _config.MinTimeAfterError = newValue;
                OnPropertyChanged();
            }
        }
        public int CacheExpirationHours
        {
            get => _config.CacheExpirationHours;
            set
            {
                
                var newValue = Math.Max(1, Math.Min(168, value)); // 1 hour to 1 week
                LogPropertyChange(nameof(CacheExpirationHours), _config.CacheExpirationHours, newValue);
                _config.CacheExpirationHours = newValue;
                OnPropertyChanged();
            }
        }

        public bool MinimizeBrowserWindow
        {
            get => _config.MinimizeBrowserWindow;
            set
            {
                LogPropertyChange(nameof(MinimizeBrowserWindow), _config.MinimizeBrowserWindow, value);
                _config.MinimizeBrowserWindow = value;
                OnPropertyChanged();
            }
        }

        public bool AutoStartDownloads
        {
            get => _config.AutoStartDownloads;
            set
            {                
                LogPropertyChange(nameof(AutoStartDownloads), _config.AutoStartDownloads, value);
                _config.AutoStartDownloads = value;
                OnPropertyChanged();
            }
        }

        public bool ShowNotifications
        {
            get => _config.ShowNotifications;
            set
            {
                LogPropertyChange(nameof(ShowNotifications), _config.ShowNotifications, value);
                _config.ShowNotifications = value;
                OnPropertyChanged();
            }
        }

        public string DefaulterLoggerFile
        {
            get => _config.DefaulterLoggerFile;
            set
            {
                LogPropertyChange(nameof(DefaulterLoggerFile), _config.DefaulterLoggerFile, value);
                _config.DefaulterLoggerFile = value;
                AppLogger.MegaLogger.UpdateFileOutput(_config.LogToFile, _config.DefaulterLoggerFile);
                OnPropertyChanged();
            }
        }

        public bool PrintToScreen
        {
            get => _config.PrintToScreen;
            set
            {
                LogPropertyChange(nameof(PrintToScreen), _config.PrintToScreen, value);
                _config.PrintToScreen = value;
                AppLogger.MegaLogger.UpdatePrintLogs(value);
                OnPropertyChanged();
            }
        }

        public LogLevel DebugLogLevel
        {
            get => _config.DebugLogLevel;
            set
            {
                LogPropertyChange(nameof(DebugLogLevel), _config.DebugLogLevel, value);
                _config.DebugLogLevel = value;
                AppLogger.Logger.SetMinimumLogLevel(value);
                OnPropertyChanged();
            }
        }
        public int PageLoadTimeoutSeconds
        {
            get => _config.PageLoadTimeoutSeconds;
            set
            {
                LogPropertyChange(nameof(PageLoadTimeoutSeconds), _config.PageLoadTimeoutSeconds, value);
                _config.PageLoadTimeoutSeconds = value;
                OnPropertyChanged();
            }
        }

        public bool RequiresRestart
        {
            get => _config.RequiresRestart;
            private set
            {
                if (_config.RequiresRestart == value) return;
                LogPropertyChange(nameof(RequiresRestart), _config.RequiresRestart, value);
                _config.RequiresRestart = value;
                OnPropertyChanged();
            }
        }
        public string UserAgent
        {
            get => _config.UserAgent;
            set
            {
                if (_config.UserAgent == value) return;
                LogPropertyChange(nameof(UserAgent), _config.UserAgent, value);
                _config.UserAgent = value;
                OnPropertyChanged();
            }
        }

        public bool LogToFile
        {
            get => _config.LogToFile;
            set
            {
                if (_config.LogToFile == value) return;
                LogPropertyChange(nameof(LogToFile), _config.LogToFile, value);
                _config.LogToFile = value;
                AppLogger.MegaLogger.UpdateFileOutput(_config.LogToFile, _config.DefaulterLoggerFile);
                OnPropertyChanged();
            }
        }
        
        public int MaxLogSizeMb
        {
            get => _config.MaxLogSizeMb;
            set
            {
                var newValue = Math.Max(1, Math.Min(100, value)); // Limit between 1MB and 100MB
                if (_config.MaxLogSizeMb == newValue) return;
                LogPropertyChange(nameof(MaxLogSizeMb), _config.MaxLogSizeMb, newValue);
                _config.MaxLogSizeMb = newValue;
                OnPropertyChanged();
            }
        }


        #endregion

        #region Load/Save Configuration

        private AppConfiguration LoadConfiguration()
        {
            try
            {
                var configPath = AppStorageService.GetPath(_configPath);
                Logger.Debug($"Loading configuration from: {configPath}");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json);

                    if (config != null)
                    {
                        Logger.Debug("Successfully loaded configuration");
                        return config;
                    }
                    else
                    {
                        Logger.Warning("Configuration file exists but could not be deserialized");
                    }
                }
                else
                {
                    Logger.Info("No configuration file found, using defaults");
                }
            }
            catch (Exception ex)
            {
                // Use Debug.WriteLine instead of logger to prevent circular dependency
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
            }

            // Return default configuration
            return CreateDefaultConfiguration();
        }

        public void SaveConfiguration()
        {
            var configPath = string.Empty;
            try
            {
                // Create serialization options
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
                };

                // Get the full config path
                configPath = AppStorageService.GetPath(_configPath);
                Logger.Debug($"Saving configuration to: {configPath}");

                // Serialize the configuration
                string json;
                try
                {
                    json = JsonSerializer.Serialize(_config, options);
                    Logger.Debug("Configuration serialized successfully");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to serialize configuration: {ex}");
                    throw;
                }

                // Ensure the directory exists
                var directory = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Logger.Debug($"Creating directory: {directory}");
                    Directory.CreateDirectory(directory);
                }
                
                // Write to a temporary file first
                var tempFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                Logger.Debug($"Writing to temporary file: {tempFile}");
                
                File.WriteAllText(tempFile, json);
                
                // Replace the original file atomically
                Logger.Debug("Replacing original configuration file");
                File.Replace(tempFile, configPath, null);
                
                Logger.Info($"Configuration successfully saved to: {configPath}");
            }
            catch (Exception ex)
            {
                var errorMessage = $"Error saving configuration to {configPath}: {ex}";
                Logger.Error(errorMessage);
                throw new InvalidOperationException(errorMessage, ex);
            }
        }
        
        private static AppConfiguration CreateDefaultConfiguration()
        {
            return new AppConfiguration
            {
                DefaultDownloadDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads",
                    "AnimeHeaven"
                ),
                // General Settings
                MaxConcurrentDownloads = 5,
                MaxRetryAttempts = 5,
                MinTimeAfterError = 5,
                CacheExpirationHours = 24,
                MinimizeBrowserWindow = true,
                AutoStartDownloads = true,
                ShowNotifications = true,
                
                // Browser Settings
                BrowserType = BrowserType.Chrome,
                HeadlessMode = false,
                DisableImages = false,
                DisableJavaScript = false,
                PageLoadTimeout = 30, // seconds
                UserDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AnimeBingeDownloader",
                    "BrowserData"
                ),
                
                // Logging Settings
                DefaulterHistoryFIle = "Anime_Downloader_Default.json",
                DefaulterLoggerFile = "log.txt",
                PrintToScreen = true,
                PageLoadTimeoutSeconds = 30,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
                DebugLogLevel = LogLevel.Debug,
                RequiresRestart = false,
                LogToFile = false,
                MaxLogSizeMb = 10 // 10MB default log size
            };
        }

        #endregion

        #region Reset Configuration

        public void ResetToDefaults()
        {
            _config = CreateDefaultConfiguration();
            SaveConfiguration();
        }

        #endregion
    }

    /// <summary>
    /// Application configuration data model
    /// </summary>

    public class AppConfiguration
    {
        public string DefaultDownloadDirectory { get; set; } = string.Empty;
        public int MaxConcurrentDownloads { get; set; } = 5;
        public int MaxRetryAttempts { get; set; } = 5;
        public int CacheExpirationHours { get; set; } = 24;
        public bool MinimizeBrowserWindow { get; set; } = true;
        public bool AutoStartDownloads { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        
        // Browser Settings
        public BrowserType BrowserType { get; set; } = BrowserType.Chrome;
        public bool HeadlessMode { get; set; } = false;
        public bool DisableImages { get; set; } = false;
        public bool DisableJavaScript { get; set; } = false;
        public int PageLoadTimeout { get; set; } = 30; // seconds
        public string UserDataDir { get; set; } = string.Empty;
        
        public string DefaulterHistoryFIle { get; set; } = "Anime_Downloader_Default.json";
        public string DefaulterLoggerFile { get; set; } = string.Empty;
        public bool PrintToScreen { get; set; } = true;
        public int MinTimeAfterError { get; set; } = 5;
        public int PageLoadTimeoutSeconds { get; set; } = 5;
        public string UserAgent { get; set; } = string.Empty;
        public LogLevel DebugLogLevel { get; set; } = LogLevel.Debug;
        public bool RequiresRestart { get; set; }
        public bool LogToFile { get; set; }
        public int MaxLogSizeMb { get; set; }
        
        
    }
}