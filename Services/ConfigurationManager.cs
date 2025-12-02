using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Views;

namespace AnimeBingeDownloader.Services
{
    /// <summary>
    /// Manages application configuration and user preferences
    /// </summary>
    public class ConfigurationManager
    {
        private static ConfigurationManager? _instance;
        private static readonly Lock InstanceLock = new();
        
        private const string ConfigFileName = "app_config.json";
        private AppConfiguration _config;
        private readonly string _configPath;
        private Logger _logger = new Logger("ConfigurationManager");
        public Logger Logger => _logger;
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
            MainWindow.Logger.Subscribe(_logger);
            _configPath = AppStorageService.GetPath(ConfigFileName);
            _config = LoadConfiguration();
        }

        
        #region Configuration Properties

        public string DefaultDownloadDirectory
        {
            get => _config.DefaultDownloadDirectory;
            set
            {
                _logger.AddLog($"changed Default Download Directory value, old: {_config.DefaultDownloadDirectory} , new: {value} ");
                _config.DefaultDownloadDirectory = value;
                SaveConfiguration();
            }
        }
        public string DefaulterHistoryFIle
        {
            get => _config.DefaulterHistoryFIle;
            set
            {
                _logger.AddLog($"changed Defaulter History File value, old: {_config.DefaulterHistoryFIle} , new: {value} ");
                _config.DefaulterHistoryFIle = value;
                SaveConfiguration();
            }
        }
        public int MaxConcurrentDownloads
        {
            get => _config.MaxConcurrentDownloads;
            set
            {
                _logger.AddLog($"changed Max Concurrent Downloads value, old: {_config.MaxConcurrentDownloads} , new: {value} ");
                _config.MaxConcurrentDownloads = Math.Max(1, Math.Min(10, value));
                SaveConfiguration();
            }
        }

        public int MaxRetryAttempts
        {
            get => _config.MaxRetryAttempts;
            set
            {
                _logger.AddLog($"changed Max Retry Attempts value, old: {_config.MaxRetryAttempts} , new: {value} ");
                _config.MaxRetryAttempts = Math.Max(1, Math.Min(10, value));
                SaveConfiguration();
            }
        }

        public int CacheExpirationHours
        {
            get => _config.CacheExpirationHours;
            set
            {
                _logger.AddLog($"changed Cache Expiration Hours value, old: {_config.CacheExpirationHours} , new: {value} ");
                _config.CacheExpirationHours = Math.Max(1, Math.Min(168, value)); // 1 hour to 1 week
                SaveConfiguration();
            }
        }

        public bool MinimizeBrowserWindow
        {
            get => _config.MinimizeBrowserWindow;
            set
            {
                _logger.AddLog($"changed Minimize Browser Window value, old: {_config.MinimizeBrowserWindow} , new: {value} ");
                _config.MinimizeBrowserWindow = value;
                SaveConfiguration();
            }
        }

        public bool AutoStartDownloads
        {
            get => _config.AutoStartDownloads;
            set
            {
                _logger.AddLog($"changed Auto Start Downloads value, old: {_config.AutoStartDownloads} , new: {value} ");
                _config.AutoStartDownloads = value;
                SaveConfiguration();
            }
        }

        public bool ShowNotifications
        {
            get => _config.ShowNotifications;
            set
            {
                _logger.AddLog($"changed Show Notifications value, old: {_config.ShowNotifications} , new: {value} ");
                _config.ShowNotifications = value;
                SaveConfiguration();
            }
        }

        public string DefaulterLoggerFile
        {
            get => _config.DefaulterLoggerFile;
            set
            {
                _logger.AddLog($"changed Default Logger File value, old: {_config.DefaulterLoggerFile} , new: {value} ");
                _config.DefaulterLoggerFile = value;
                SaveConfiguration();
            }
        }

        public bool PrintToScreen
        {
            get => _config.PrintToScreen;
            set
            {
                _logger.AddLog($"changed Print To Screen value, old: {_config.PrintToScreen} , new: {value} ");
                _config.PrintToScreen = value;
                SaveConfiguration();

            }
        }
        #endregion

        #region Load/Save Configuration

        private AppConfiguration LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json);
                    
                    if (config != null)
                    {
                        _logger.AddLog("Loaded configuration file");
                        return config;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}");
            }

            // Return default configuration
            return CreateDefaultConfiguration();
        }

        private void SaveConfiguration()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.Never
                };

                var json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving configuration: {ex.Message}");
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
                MaxConcurrentDownloads = 5,
                MaxRetryAttempts = 5,
                CacheExpirationHours = 24,
                MinimizeBrowserWindow = true,
                AutoStartDownloads = true,
                ShowNotifications = true,
                DefaulterHistoryFIle = AppStorageService.GetPath("Anime_Downloader_Default.json"),
                DefaulterLoggerFile = "log.txt",
                PrintToScreen = true
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
        public string DefaultDownloadDirectory { get; set; } = "";
        public int MaxConcurrentDownloads { get; set; } = 5;
        public int MaxRetryAttempts { get; set; } = 5;
        public int CacheExpirationHours { get; set; } = 24;
        public bool MinimizeBrowserWindow { get; set; } = true;
        public bool AutoStartDownloads { get; set; } = true;
        public bool ShowNotifications { get; set; } = true;
        public string DefaulterHistoryFIle{get;set;} = "";
        public string DefaulterLoggerFile{get;set;} = "";
        public bool PrintToScreen { get; set; } = true;
    }
}