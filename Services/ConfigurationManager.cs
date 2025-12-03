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
                _configPath = AppStorageService.GetPath(ConfigFileName);
                _config = LoadConfiguration();
                
                // Only subscribe to logger after initialization is complete
                Utils.AppLogger.MegaLogger.Subscribe(Logger,_config.PrintToScreen);
                Logger.AddLog("ConfigurationManager initialized successfully");
            }
            catch (Exception ex)
            {
                // Fallback to console if logging isn't available yet
                Console.WriteLine($"Error initializing ConfigurationManager: {ex.Message}");
                throw;
            }
        }

        
        #region Configuration Properties

        public string DefaultDownloadDirectory
        {
            get => _config.DefaultDownloadDirectory;
            set
            {
                Logger.AddLog($"changed Default Download Directory value, old: {_config.DefaultDownloadDirectory} , new: {value} ");
                _config.DefaultDownloadDirectory = value;
                SaveConfiguration();
            }
        }
        public string DefaulterHistoryFIle
        {
            get => _config.DefaulterHistoryFIle;
            set
            {
                Logger.AddLog($"changed Defaulter History File value, old: {_config.DefaulterHistoryFIle} , new: {value} ");
                _config.DefaulterHistoryFIle = value;
                SaveConfiguration();
            }
        }
        public int MaxConcurrentDownloads
        {
            get => _config.MaxConcurrentDownloads;
            set
            {
                Logger.AddLog($"changed Max Concurrent Downloads value, old: {_config.MaxConcurrentDownloads} , new: {value} ");
                _config.MaxConcurrentDownloads = Math.Max(1, Math.Min(10, value));
                SaveConfiguration();
            }
        }

        public int MaxRetryAttempts
        {
            get => _config.MaxRetryAttempts;
            set
            {
                Logger.AddLog($"changed Max Retry Attempts value, old: {_config.MaxRetryAttempts} , new: {value} ");
                _config.MaxRetryAttempts = Math.Max(1, Math.Min(10, value));
                SaveConfiguration();
            }
        }
        public int MinTimeAfterError
        {
            get => _config.MinTimeAfterError;
            set
            {
                Logger.AddLog($"changed Min Time After Error value, old: {_config.MinTimeAfterError} , new: {value} ");
                _config.MinTimeAfterError = Math.Max(1, Math.Min(10, value));
                SaveConfiguration();
            }
        }
        public int CacheExpirationHours
        {
            get => _config.CacheExpirationHours;
            set
            {
                Logger.AddLog($"changed Cache Expiration Hours value, old: {_config.CacheExpirationHours} , new: {value} ");
                _config.CacheExpirationHours = Math.Max(1, Math.Min(168, value)); // 1 hour to 1 week
                SaveConfiguration();
            }
        }

        public bool MinimizeBrowserWindow
        {
            get => _config.MinimizeBrowserWindow;
            set
            {
                Logger.AddLog($"changed Minimize Browser Window value, old: {_config.MinimizeBrowserWindow} , new: {value} ");
                _config.MinimizeBrowserWindow = value;
                SaveConfiguration();
            }
        }

        public bool AutoStartDownloads
        {
            get => _config.AutoStartDownloads;
            set
            {
                Logger.AddLog($"changed Auto Start Downloads value, old: {_config.AutoStartDownloads} , new: {value} ");
                _config.AutoStartDownloads = value;
                SaveConfiguration();
            }
        }

        public bool ShowNotifications
        {
            get => _config.ShowNotifications;
            set
            {
                Logger.AddLog($"changed Show Notifications value, old: {_config.ShowNotifications} , new: {value} ");
                _config.ShowNotifications = value;
                SaveConfiguration();
            }
        }

        public string DefaulterLoggerFile
        {
            get => _config.DefaulterLoggerFile;
            set
            {
                Logger.AddLog($"changed Default Logger File value, old: {_config.DefaulterLoggerFile} , new: {value} ");
                _config.DefaulterLoggerFile = value;
                SaveConfiguration();
            }
        }

        public bool PrintToScreen
        {
            get => _config.PrintToScreen;
            set
            {
                Logger.AddLog($"changed Print To Screen value, old: {_config.PrintToScreen} , new: {value} ");
                _config.PrintToScreen = value;
                Utils.AppLogger.MegaLogger.UpdatePrintLogs(value);
                SaveConfiguration();

            }
        }

        public LogLevel DebugLogLevel
        {
            get => _config.DebugLogLevel;
            set
            {
                Logger.AddLog($"changed Debug Log Level, old: {_config.DebugLogLevel} , new: {value} ");
                _config.DebugLogLevel = value;
                SaveConfiguration();

            }
        }
        public int PageLoadTimeoutSeconds
        {
            get => _config.PageLoadTimeoutSeconds;
            set
            {
                Logger.AddLog($"changed Page Load Timeout Seconds, old: {_config.PageLoadTimeoutSeconds} , new: {value} ");
                _config.PageLoadTimeoutSeconds = value;
                SaveConfiguration();

            }
        }

        public bool RequiresRestart
        {
            get => _config.RequiresRestart;
            private set
            {
                Logger.AddLog($"changed Requires Restart, old: {_config.RequiresRestart} , new: {value} ");
                _config.RequiresRestart = value;
                SaveConfiguration();
            }
        }
        public string UserAgent
        {
            get => _config.UserAgent;
            set
            {
                Logger.AddLog($"changed User Agent, old: {_config.UserAgent} , new: {value} ");
                _config.UserAgent = value;
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
                        // Don't log here to prevent circular dependency
                        return config;
                    }
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
                PrintToScreen = true,
                MinTimeAfterError = 5,
                UserAgent ="",
                PageLoadTimeoutSeconds =5,
                DebugLogLevel= LogLevel.Debug
                
                
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
    internal class AppConfiguration
    {
        internal string DefaultDownloadDirectory { get; set; } = "";
        internal int MaxConcurrentDownloads { get; set; } = 5;
        internal int MaxRetryAttempts { get; set; } = 5;
        internal int CacheExpirationHours { get; set; } = 24;
        internal bool MinimizeBrowserWindow { get; set; } = true;
        internal bool AutoStartDownloads { get; set; } = true;
        internal bool ShowNotifications { get; set; } = true;
        internal string DefaulterHistoryFIle{get;set;} = "";
        internal string DefaulterLoggerFile{get;set;} = "";
        internal bool PrintToScreen { get; set; } = true;
        internal int MinTimeAfterError{ get; set; } = 5;
        internal int PageLoadTimeoutSeconds { get; set; } = 5;
        internal string UserAgent { get; set; } = "";
        internal LogLevel DebugLogLevel { get; set; } = LogLevel.Debug;
        internal bool RequiresRestart { get; set; }
    }
}