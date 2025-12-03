using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Views;

namespace AnimeBingeDownloader.Services
{
    public class CacheService
    {
        private static CacheService Instance { get; } = new();
        private const string CacheFileName = "anime_links_cache.json";
        private readonly Dictionary<string, CachedAnimeData>? _cachedAnimeData;
        private readonly Logger _logger = new("[CacheService] ");
        private readonly Lock _cacheLock = new Lock();
        private CacheService()
        {
            MainWindow.Logger.Subscribe(_logger);
            _cachedAnimeData = new Dictionary<string, CachedAnimeData>();
            
            try
            {
                var cacheFilePath = AppStorageService.GetPath(CacheFileName);
                _logger.Info($"Loading cache from: {cacheFilePath}");
                
                if (!File.Exists(cacheFilePath))
                {
                    _logger.Info("Cache file not found, starting with empty cache");
                    return;
                }
                
                var jsonContent = File.ReadAllText(cacheFilePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                
                var allCache = JsonSerializer.Deserialize<Dictionary<string, CachedAnimeData>>(jsonContent, options);
                
                if (allCache == null || allCache.Count == 0)
                {
                    _logger.Info("Cache file is empty or invalid");
                    return;
                }
                
                var expiredCount = 0;
                var validCount = 0;
                
                foreach (var (key, animeData) in allCache)
                {
                    try
                    {
                        var createTime = DateTime.FromFileTime(animeData.Timestamp);
                        var age = DateTime.Now - createTime;
                        
                        if (age.Hours > ConfigurationManager.Instance.CacheExpirationHours)
                        {
                            _logger.Debug($"Skipping expired cache entry: {animeData.Title} (age: {age.TotalHours:F1} hours)");
                            expiredCount++;
                            continue;
                        }
                        
                        _cachedAnimeData[key] = animeData;
                        validCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error processing cache entry {key}: {ex.Message}");
                    }
                }
                
                _logger.Info($"Cache loaded: {validCount} valid entries, {expiredCount} expired entries");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error initializing cache: {ex.Message}");
                // Continue with empty cache
                _cachedAnimeData.Clear();
            }
        }

        private Task<CachedAnimeData?> LoadData(string url)
        {
            try
            {
                if (_cachedAnimeData == null)
                    return Task.FromResult<CachedAnimeData?>(null);

                lock (_cacheLock)
                {
                    if (!_cachedAnimeData.TryGetValue(url, out var animeData))
                        return Task.FromResult<CachedAnimeData?>(null);

                    var createTime = DateTime.FromFileTime(animeData.Timestamp);
                    var age = DateTime.Now - createTime;

                    if (age.Hours <= ConfigurationManager.Instance.CacheExpirationHours)
                        return Task.FromResult<CachedAnimeData?>(animeData);
                    _logger.Debug($"Cache entry for {url} is expired (age: {age.TotalHours:F1}h)");
                    _cachedAnimeData.Remove(url);
                    return Task.FromResult<CachedAnimeData?>(null);

                }
            }
            catch (Exception ex)
            {
                lock (_cacheLock)
                {
                    _logger.Error($"Error loading cache for {url}: {ex.Message}");
                }

                return Task.FromResult<CachedAnimeData?>(null);
            }
        }
            
        
        public static async Task<CachedAnimeData?> LoadCacheAsync(string url)
        {
            var instance = Instance;
            return await instance.LoadData(url);
        }
        public static void SaveCacheAsync (string url, string title, Dictionary<string,EpisodeInfo> episodes)
        {
            var instance = Instance;
            instance.SaveData(url,title, episodes);
        }
        private void SaveData(string url, string title, Dictionary<string, EpisodeInfo> episodes)
        {
            try
            {
                if (string.IsNullOrEmpty(url))
                {
                    lock (_cacheLock)
                    {
                        _logger.Error("Cannot save cache: URL is null or empty");
                    }

                    return;
                }

                var animeData = new CachedAnimeData(title, episodes, DateTimeOffset.UtcNow.ToFileTime());
                
                var jsonOptions = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                };

                var tempFile = Path.GetTempFileName();
                var cacheFilePath = AppStorageService.GetPath(CacheFileName);
                var directory = Path.GetDirectoryName(cacheFilePath);

                if (string.IsNullOrEmpty(directory))
                {
                    lock (_cacheLock)
                    {
                        _logger.Error($"Invalid cache directory path: {cacheFilePath}");
                    }

                    return;
                }

                try
                {
                    // Ensure directory exists
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        lock (_cacheLock)
                        {
                            _logger.Debug($"Created cache directory: {directory}");
                        }
                    }

                    // Serialize to temp file first
                    var jsonContent = JsonSerializer.Serialize(_cachedAnimeData, jsonOptions);
                    File.WriteAllText(tempFile, jsonContent);

                    // Atomically replace the old file
                    lock (_cacheLock)
                    {
                        _cachedAnimeData![url] = animeData;
                        
                        // On Windows, we need to delete the target file first if it exists
                        if (File.Exists(cacheFilePath))
                        {
                            File.Delete(cacheFilePath);
                        }
                        
                        File.Move(tempFile, cacheFilePath);
                        _logger.Debug($"Saved cache for {url} to {cacheFilePath}");
                    }
                }
                finally
                {
                    // Clean up temp file if it still exists
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); }
                        catch { /* Ignore cleanup errors */ }
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_cacheLock)
                {
                    _logger.Error($"Error saving cache for {url}: {ex.Message}");
                }
            }
        }

    }

    public class CachedAnimeData
    {
        // Parameterless constructor for deserialization
        public CachedAnimeData() 
        {
            Title = string.Empty;
            Links = new Dictionary<string, EpisodeInfo>();
            Timestamp = DateTimeOffset.UtcNow.ToFileTime();
        }

        public CachedAnimeData(string? title, Dictionary<string, EpisodeInfo>? links, long timestamp)
        {
            Title = title ?? string.Empty;
            Links = links ?? new Dictionary<string, EpisodeInfo>();
            Timestamp = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToFileTime();
        }

        [JsonPropertyName("title")]
        [JsonInclude]
        public string Title { get; set; }
        
        [JsonPropertyName("links")]
        [JsonInclude]
        public Dictionary<string, EpisodeInfo> Links { get; set; }
        
        [JsonPropertyName("timestamp")]
        [JsonInclude]
        public long Timestamp { get; set; }
    }
}