using AnimeBingeDownloader.Models;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;


namespace AnimeBingeDownloader.Models
{
    public static class EnumTranslator
    {
        static string TranslateEnumToString(TaskStatus status)
        {
            return status switch
            {
                TaskStatus.Queued => "Queued",
                TaskStatus.Running => "Running",
                TaskStatus.Scraping => "Scraping",
                TaskStatus.Downloading => "Downloading",
                TaskStatus.ScrapingAndDownloading => "Scraping and Downloading",
                TaskStatus.FinishedScraping => "Finished Scraping",
                TaskStatus.FinishedScrapingAndDownloads => "Finished Scraping and Downloading",
                TaskStatus.Completed => "Completed",
                TaskStatus.Error => "Error",
                TaskStatus.Canceled => "Canceled",
                _ => "Unknown"
            };
        }

        static string TranslateEnumToString(EpisodeState status)
        {
            return status switch
            {
                EpisodeState.WaitingForLink => "Waiting For Link",
                EpisodeState.LinkScraped => "Link Scraped",
                EpisodeState.QueuedForDownload => "Queued For Download",
                EpisodeState.Downloading => "Downloading",
                EpisodeState.Interrupted => "Interrupted",
                EpisodeState.Skipped => "Skipped",
                EpisodeState.Completed => "Completed",
                EpisodeState.Error => "Error",
                _ => "Unknown"
            };
        }

        static string TranslateEnumToString(TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.Low => "Low",
                TaskPriority.Medium => "Medium",
                TaskPriority.High => "High",
                _ => "Unknown"
            };
        }

        static string TranslateEnumToString(EpisodeDownloadResult result)
        {
            return result switch
            {
                EpisodeDownloadResult.Completed => "Completed",
                EpisodeDownloadResult.Failed => "Failed",
                EpisodeDownloadResult.Skipped => "Skipped",
                EpisodeDownloadResult.Cancelled => "Cancelled",
                EpisodeDownloadResult.Partial => "Partial",
                EpisodeDownloadResult.Error => "Error",
                _ => "Unknown"
            };
        }
        private static string TranslateEnumToString<T>(T enumValue) where T : Enum
        {
            // Default behavior: just return the enum name and value as a string.
            return $"[DEFAULT:{typeof(T).Name}.{enumValue}]";
        }
        public static string TranslateEnumToStr<T>(T? enumValue) where T : Enum
        {
            return enumValue == null ? string.Empty : (string)
                // The key to dispatching:
                // Casting the value to 'dynamic' forces the C# runtime to perform method overload 
                // resolution based on the *actual runtime type* of the enumValue (e.g., LogLevel)
                // rather than the generic placeholder (T).
                TranslateEnumToString((dynamic)enumValue);
        }
    }
    
    public enum TaskStatus
    {
        Queued,
        Running,
        Scraping,
        Downloading,
        FinishedScraping,
        FinishedScrapingAndDownloads,
        ScrapingAndDownloading,
        Completed,
        Canceled,
        Error
    
    }

    public enum TaskPriority
    {
        High = 1,
        Medium = 5,
        Low = 10,
        Pause = 20
    }
    public enum EpisodeState
    {
        WaitingForLink,
        LinkScraped,
        QueuedForDownload,
        Downloading,
        Interrupted,
        Completed,
        Error,
        Skipped
    }

    public enum EpisodeDownloadResult
    {
        Completed,
        Failed,
        Skipped,
        Partial,
        Cancelled,
        Error
    
    }
}



