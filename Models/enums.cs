using AnimeBingeDownloader.Models;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;


namespace AnimeBingeDownloader.Models
{
    public static class EnumTranslator
    {
        private static string TranslateEnumToString(TaskStatus status)
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

        private static string TranslateEnumToString(EpisodeState status)
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

        private static string TranslateEnumToString(TaskPriority priority)
        {
            return priority switch
            {
                TaskPriority.Low => "Low",
                TaskPriority.Medium => "Medium",
                TaskPriority.High => "High",
                _ => "Unknown"
            };
        }

        private static string TranslateEnumToString(EpisodeDownloadResult result)
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

        private static TaskPriority ParseTaskPriority(string str)
        {
            return str switch
            {
                "Low" => TaskPriority.Low,
                "Medium" => TaskPriority.Medium,
                "High" => TaskPriority.High,
                _ => throw new ArgumentException($"Unknown TaskPriority string: {str}")
            };
        }
        private static EpisodeDownloadResult ParseEpisodeDownloadResult(string str)
        {
            return str switch
            {
                "Completed" => EpisodeDownloadResult.Completed,
                "Failed" => EpisodeDownloadResult.Failed,
                "Skipped" => EpisodeDownloadResult.Skipped,
                "Cancelled" => EpisodeDownloadResult.Cancelled,
                "Partial" => EpisodeDownloadResult.Partial,
                "Error" => EpisodeDownloadResult.Error,
                _ => throw new ArgumentException($"Unknown EpisodeDownloadResult string: {str}")
            };
        }
        private static EpisodeState ParseEpisodeState(string str)
        {
            return str switch
            {
                "Waiting For Link" => EpisodeState.WaitingForLink,
                "Link Scraped" => EpisodeState.LinkScraped,
                "Queued For Download" => EpisodeState.QueuedForDownload,
                "Downloading" => EpisodeState.Downloading,
                "Interrupted" => EpisodeState.Interrupted,
                "Skipped" => EpisodeState.Skipped,
                "Completed" => EpisodeState.Completed,
                "Error" => EpisodeState.Error,
                _ => throw new ArgumentException($"Unknown EpisodeState string: {str}")
            };
        }
        private static TaskStatus ParseTaskStatus(string str)
        {
            return str switch
            {
                "Queued" => TaskStatus.Queued,
                "Running" => TaskStatus.Running,
                "Scraping" => TaskStatus.Scraping,
                "Downloading" => TaskStatus.Downloading,
                "Scraping and Downloading" => TaskStatus.ScrapingAndDownloading,
                "Finished Scraping" => TaskStatus.FinishedScraping,
                "Finished Scraping and Downloading" => TaskStatus.FinishedScrapingAndDownloads,
                "Completed" => TaskStatus.Completed,
                "Error" => TaskStatus.Error,
                "Canceled" => TaskStatus.Canceled,
                _ => throw new ArgumentException($"Unknown TaskStatus string: {str}")
            };
        }

        public static Enum Parse<T>(T? enumType, string? statusString) where T :Enum
        {
            
            if (string.IsNullOrEmpty(statusString) || enumType == null) throw new ArgumentException($"Unknown TaskPriority string: {statusString}");
            return enumType switch
            {
                TaskPriority =>ParseTaskPriority(statusString),
                TaskStatus => ParseTaskStatus(statusString),
                EpisodeState=> ParseEpisodeState(statusString),
                EpisodeDownloadResult => ParseEpisodeDownloadResult(statusString),
                _ => throw new ArgumentException($"Unknown enum string: {enumType}")
                

            };

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



