using System.IO;
using System.Text.RegularExpressions;
using AnimeBingeDownloader.Models;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;


namespace AnimeBingeDownloader.Services
{
    public partial class TaskCoordinator(TaskViewModel task)
    {
        

        [Obsolete("Obsolete")]
        public async Task<bool> ExecuteTaskAsync()
        {
            task.UpdateStatus(TaskStatus.Running, "Task started.");

            try
            {
                // Step 1: Scrape episode links
                var scrapingResult = await ScrapeEpisodeLinksAsync();

                if (!scrapingResult.Success || task.CancellationToken.IsCancellationRequested)
                {
                    if (task.CancellationToken.IsCancellationRequested)
                    {
                        task.UpdateStatus(TaskStatus.Canceled, "Task was canceled by user.");
                    }

                    return false;
                }

                // Update task with scraped info
                task.Title = scrapingResult.Title;
                SetDownloadDirectory();

                // Step 2: Download episodes

                // Final status update
                if (task.CancellationToken.IsCancellationRequested)
                {
                    task.UpdateStatus(TaskStatus.Canceled, "Task was canceled by user.");
                }
                else if (task.EpisodesCompleted >= task.EpisodesFound)
                {
                    task.UpdateStatus(TaskStatus.Completed, "All episodes downloaded successfully.");
                }
                else if (task.Status == TaskStatus.ScrapingAndDownloading)
                {
                    task.UpdateStatus(TaskStatus.FinishedScrapingAndDownloads,
                        "Task completed scrapping with episodes.");
                }
                else
                {
                    task.UpdateStatus(TaskStatus.FinishedScraping,
                        "Task completed scrapping");
                }
            }
            catch (Exception ex)
            {
                task.UpdateStatus(TaskStatus.Error, $"Task error: {ex.Message}");
            }

            return true;
        }

        [Obsolete("Obsolete")]
        private async Task<ScrapingResult> ScrapeEpisodeLinksAsync()
        {
            task.UpdateStatus(TaskStatus.Scraping, "Starting scraping process...");

            using var scraper = new ScrapingService(task.Id,task._megaLogger);
            task.AddScrapper(scraper);
            var result = await scraper.ScrapeLinksAsync(task,task.CancellationToken,this);
            task.CleanScrape();
            return result;
        }

        public void AddEpisodeTask(string episodeNumber,string link)
        {
            var episode = new EpisodeTask(
                task.Id, episodeNumber,
                task.Directory,animeName:task.Title,link,task);
            task.AddEpisode(episode);
            DownloadService.Instance.AddToQueue(episode,task.Priority);
        }

        
        public void SetDownloadDirectory()
        {
            if (task.Title == "N/A") return;
            var safeTitle = MyRegex().Replace(task.Title, "").Trim();
            task.Directory = Path.Combine(task.Directory, safeTitle);

            if (!Directory.Exists(task.Directory))
            {
                Directory.CreateDirectory(task.Directory);
            }

            task.AddLog($"Download directory set to: {task.Directory}");
        }
        

        [GeneratedRegex(@"[\\/:*?""<>|]")]
        private static partial Regex MyRegex();
    }
}