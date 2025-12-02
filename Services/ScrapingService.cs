using System.Diagnostics;
using System.IO;
using AnimeBingeDownloader.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using WebDriverManager;
using WebDriverManager.DriverConfigs.Impl;
using WebDriverManager.Helpers;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;

namespace AnimeBingeDownloader.Services
{
    public class ScrapingService(string taskId) : IDisposable
    {
        private IWebDriver? _driver = null;
        private const string AnimeTimePlaceholder = "AnimeHeavenVideo";
        private readonly Logger _logger = new($"[ScrapingService] {taskId} ");

        [Obsolete("Obsolete")]
        public async Task<ScrapingResult> ScrapeLinksAsync(TaskViewModel task,CancellationToken cancellationToken,TaskCoordinator taskCoordinator)
        {
            var result = new ScrapingResult();

            try
            {
                // Check cache first
                task.AddLog("Checking cache for existing links...");
               
                task.AddLog("Cache Miss or Expired. Starting full Selenium scraping process...");
                task.UpdateStatus(TaskStatus.Scraping, "Initializing WebDriver (running in VISIBLE mode)...");
                var animeCache = await CacheService.LoadCacheAsync(task.Url);
                Dictionary<string,EpisodeInfo>? links = null;
                
                // Initialize Chrome WebDriver  
                InitializeWebDriver();
                if (_driver is null) return result;
                task.AddLog($"Navigating to main page: {task.Url}");
                await _driver.Navigate().GoToUrlAsync(task.Url);

                // Wait for page to load
                await Task.Delay(1000, cancellationToken);
                var animeTitle = "";
                if (animeCache != null)
                {
                    animeTitle = animeCache.Title;
                    links = animeCache.Links;
                }
                else
                {
                    animeTitle = ScrapeAnimeTitle(task);
                }
                // 1. Scrape Anime Title
                task.Title = animeTitle;
                taskCoordinator.SetDownloadDirectory();
                // 2. Find episode elements
                var episodeElements = GetEpisodeElements();
                
                var episodeIds = new List<(string id, string number, int index)>();

                task.AddLog($"Found {episodeElements.Count} potential episodes to scrape.");
                
                // 3. Extract episode IDs and numbers
                for (var i = 0; i < episodeElements.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        task.AddLog("Cancellation requested. Halting scraping.");
                        break;
                    }

                    try
                    {
                        var element = episodeElements[i];
                        var episodeNumberElement = element.FindElement(By.CssSelector(".watch2.bc"));
                        var episodeNumber = episodeNumberElement.Text;

                        // Skip special episodes (e.g., 12.5)
                        if (episodeNumber.Contains(".5"))
                        {
                            task.AddLog($"Skipping special episode {episodeNumber}.");
                            continue;
                        }

                        var episodeId = element.GetAttribute("id");
                        episodeIds.Add((episodeId, episodeNumber, i));
                    }
                    catch (Exception ex)
                    {
                        task.AddLog($"Error extracting episode number from element index {i}: {ex.Message}");
                        continue;
                    }
                }

                task.AddLog($"Filtered to {episodeIds.Count} valid episodes for scraping.");
                task.EpisodesFound = episodeIds.Count;
                // 4. Scrape each episode link
                var scrapedLinks = new Dictionary<string,EpisodeInfo>();

                foreach (var (episodeId, episodeNumber, index) in episodeIds)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        task.AddLog("Stop requested. Halting scraping loop.");
                        break;
                    }

                    EpisodeInfo? ep = null;
                    if (links != null && links.TryGetValue(episodeNumber, out var link1))
                    {
                         ep = link1;
                        
                    }
                    else
                    {
                        try
                        {
                            var link = await ScrapeEpisodeLinkAsync(episodeId, episodeNumber, index);

                            if (!string.IsNullOrEmpty(link))
                            {
                                ep = new EpisodeInfo
                                {
                                    DownloadLink = link,
                                    EpisodeNumber = episodeNumber
                                };

                            }
                        }  
                        catch (Exception ex)
                        {
                            task.AddLog($"Error scraping episode {episodeNumber}: {ex.Message}");
                        }
                    }
                    if (ep == null) continue;
                    scrapedLinks.Add(episodeNumber, ep);
                    task.EpisodesScraped++;
                    task.AddLog($"Extracted link for episode {episodeNumber}");
                    taskCoordinator.AddEpisodeTask(episodeNumber,ep.DownloadLink);
                }
                task.AddLog("Scraping complete. Saving to cache...");
                CacheService.SaveCacheAsync(task.Url, animeTitle, scrapedLinks);
                result.Title = animeTitle;
                result.Episodes = scrapedLinks;
                result.Success = true;
            }
            catch (Exception ex)
            {
                task.AddLog($"Fatal error during scraping: {ex.Message}");
                task.UpdateStatus(TaskStatus.Error, $"Fatal Scraping Error: {ex.Message}");
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }
            finally
            {
                Dispose();
            }

            return result;
        }

        private void InitializeWebDriver()
        {
            
            _logger.AddLog("Bypassing WebDriverManager; using bundled chromedriver.exe.");

            // 1. Calculate the path to the bundled driver directory
            // This will resolve to: C:\Program Files (x86)\Tomer Development Inc\Anime Downloader\Drivers
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var chromeDriverDirectory = Path.Combine(appDir, "Drivers");
            var driverExePath = Path.Combine(chromeDriverDirectory, "chromedriver.exe");

            if (!File.Exists(driverExePath))
            {
                _logger.AddLog($"FATAL: Chromedriver not found at expected path: {driverExePath}");
                _logger.AddLog("Please ensure chromedriver.exe is in the 'Drivers' subfolder.");
                return; // Exit if the bundled driver is missing
            }

            _logger.AddLog($"Found bundled chromedriver.exe at: {driverExePath}");

            // 2. Setup options and service
            var options = new ChromeOptions();
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-dev-shm-usage");

            // Pass the containing directory to the service constructor.
            // Selenium will look for "chromedriver.exe" inside this directory.
            var service = ChromeDriverService.CreateDefaultService(chromeDriverDirectory);
            service.HideCommandPromptWindow = true;

            _driver = new ChromeDriver(service, options);
            _driver.Manage().Window.Minimize();
        }

        private string ScrapeAnimeTitle(TaskViewModel task)
        {
            var fallbackTitle = $"{AnimeTimePlaceholder} ({task.Url})";
            try
            {
                var titleElement = _driver?.FindElement(By.XPath("/html/body/div[3]/div/div[2]/div[1]"));
                var title = titleElement?.Text.Replace("Watch ", "").Trim();
                task.AddLog($"Identified Anime Title: {title}");
                return title ?? fallbackTitle;
            }
            catch
            {
                task.AddLog("Could not find page title element dynamically. Using placeholder.");
                return fallbackTitle;
            }
        }

        private List<IWebElement> GetEpisodeElements()
        {
            try
            {
                const string mainContainerXPath = "/html/body/div[4]";
                var mainContainer = _driver.FindElement(By.XPath(mainContainerXPath));
                var allInnerDivs = mainContainer.FindElements(By.XPath("./div")).ToList();

                IWebElement episodesContainer;

                switch (allInnerDivs.Count)
                {
                    case > 2:
                        episodesContainer = allInnerDivs[1];
                        _logger.AddLog("Found multiple potential episode containers. Using the second inner div (index 1).");
                        break;
                    case 2:
                        episodesContainer = allInnerDivs[0];
                        _logger.AddLog("Found a single inner div. Using it as the episode container.");
                        break;
                    default:
                        episodesContainer = mainContainer;
                        _logger.AddLog("No inner divs found. Using the main div[4] as a fallback episode container.");
                        break;
                }

                return episodesContainer.FindElements(By.TagName("a")).ToList();
            }
            catch (Exception ex)
            {
                _logger.AddLog($"Error finding episode container: {ex.Message}");
                return [];
            }
        }

        [Obsolete("Obsolete")]
        private async Task<string?> ScrapeEpisodeLinkAsync(string episodeId, string episodeNumber, int index)
        {
            try
            {
                // Try to find element by ID first
                IWebElement episodeElement;
                var targetXPath = $"//a[@id='{episodeId}']";

                try
                {
                    episodeElement = _driver.FindElement(By.XPath(targetXPath));
                }
                catch
                {
                    _logger.AddLog("Error locating episode element by XPath, trying by index instead.");
                    var elements = GetEpisodeElements();
                    episodeElement = elements[index];
                }

                // Click the episode
                episodeElement.Click();

                // Wait for the link element to appear
                var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(5));
                const string linkElementXPath = "/html/body/div[3]/div[7]/a";

                var linkElement = wait.Until(driver =>
                {
                    try
                    {
                        return driver.FindElement(By.XPath(linkElementXPath));
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (linkElement == null)
                {
                    _logger.AddLog($"Could not find download link for episode {episodeNumber}");
                    return null;
                }

                var link = linkElement.GetAttribute("href");

                // Navigate back
                await _driver?.Navigate().BackAsync()!;

                // Small delay to let page settle
                await Task.Delay(500);

                return link;
            }
            catch (Exception ex)
            {
                _logger.AddLog($"Error while waiting for link element for episode {episodeNumber}: {ex.Message}");
                await _driver!.Navigate().BackAsync();
                return null;
            }
        }
        

        public void Dispose()
        {
            try
            {
                if (_driver == null) return;
                _driver.Quit();
                _driver.Dispose();
                _driver = null;
                _logger.AddLog("WebDriver cleanup complete.");
            }
            catch (Exception ex)
            {
                _logger.AddLog($"Error closing WebDriver: {ex.Message}");
            }
        }
    }

    public class ScrapingResult
    {
        public string Title { get; set; }
        public Dictionary<string,EpisodeInfo> Episodes { get; set; } = new();
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class EpisodeInfo
    {
        public string EpisodeNumber { get; set; }
        public string DownloadLink { get; set; }
    }
}