using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Forms.VisualStyles;
using AnimeBingeDownloader.Models;

namespace AnimeBingeDownloader.Services
{
    public class CacheService
    {
        private static CacheService Instance { get; } = new();
        private const string CacheFileName = "anime_links_cache.json";
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);
        private Dictionary<string, CachedAnimeData>? _cachedAnimeData = null;
        private readonly Logger _logger = new("[CacheService] ");
        private readonly Lock _cacheLock = new Lock();

        private CacheService()
        {
            var cacheFilePath = AppStorageService.GetPath(CacheFileName);
            _cachedAnimeData=new Dictionary<string, CachedAnimeData>();
            if (!File.Exists(CacheFileName))
            {
                _logger.AddLog("Cache file not found, using default");
                return;
            }
            var jsonContent = File.ReadAllTextAsync(cacheFilePath).GetAwaiter().GetResult();
            var allCache = JsonSerializer.Deserialize<Dictionary<string, CachedAnimeData>>(jsonContent);
            if (allCache == null || allCache.Count == 0)
            {
                _logger.AddLog("Cache file not found, using default");
                return;
            }
            foreach (var (key, animeData) in  allCache)
            {
                var createTime = DateTime.FromFileTime(animeData.Timestamp);
                if (createTime - DateTime.Now > _cacheExpiration)
                {
                    _logger.AddLog($"Anime : {animeData.Title} with link {key} cached at {createTime} with is expired, continue");
                    continue;   
                }
                _cachedAnimeData.Add(key, animeData);
            }
            
        }

        private async Task<CachedAnimeData?> LoadData(string url)
        {
            var animeData = new CachedAnimeData();
            if (_cachedAnimeData != null && !_cachedAnimeData.TryGetValue(url, out animeData)) return null;
            var createTime = DateTime.FromFileTime(animeData.Timestamp);
            return createTime - DateTime.Now >= _cacheExpiration ? null : animeData;
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
        private void SaveData(string url, string title, Dictionary<string,EpisodeInfo> episodes)
        {
            var animeData = new CachedAnimeData
            {
                Title = title,
                Links = episodes,
                Timestamp = DateTimeOffset.UtcNow.ToFileTime()
            };
            var jsonContent = JsonSerializer.Serialize(animeData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var cacheFilePath = AppStorageService.GetPath(CacheFileName);
            lock (_cacheLock)
            {
                _cachedAnimeData![url] = animeData;
                File.WriteAllTextAsync(cacheFilePath, jsonContent);
            }
        }

    }

    public class CachedAnimeData
    {
        public string Title { get; set; }
        public Dictionary<string,EpisodeInfo> Links { get; set; }
        public long Timestamp { get; set; }
    }
}