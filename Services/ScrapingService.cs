using System.IO;
using System.Text.Json.Serialization;
using AnimeBingeDownloader.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;
using LogLevel = AnimeBingeDownloader.Models.LogLevel;
using TaskStatus = AnimeBingeDownloader.Models.TaskStatus;

namespace AnimeBingeDownloader.Services
{
    public class ScrapingService : IDisposable
    {
        private IWebDriver? _driver;
        private const string AnimeTimePlaceholder = "AnimeHeavenVideo";
        public Logger Logger { get; }

        public ScrapingService(string taskId)
        {
            Logger = new Logger($"[ScrapingService] [TASK {taskId}]");
            Utils.AppLogger.MegaLogger.Subscribe(Logger);
        }
        [Obsolete("Obsolete")]
        public async Task<ScrapingResult> ScrapeLinksAsync(TaskViewModel task,CancellationToken cancellationToken,TaskCoordinator taskCoordinator)
        {
            var result = new ScrapingResult(task.Title,false,"");

            try
            {
                // Check cache first
                task.AddLog("Checking cache for existing links...",LogLevel.Debug);
               
                task.AddLog("Cache Miss or Expired. Starting full Selenium scraping process...",LogLevel.Debug);
                task.UpdateStatus(TaskStatus.Scraping, "Initializing WebDriver (running in VISIBLE mode)...");
                var animeCache = await CacheService.LoadCacheAsync(task.Url);
                Dictionary<string,EpisodeInfo>? links = null;
                
                // Initialize Chrome WebDriver  
                InitializeWebDriver();
                if (_driver is null) return result;
                task.AddLog($"Navigating to main page: {task.Url}",LogLevel.Debug);
                await _driver.Navigate().GoToUrlAsync(task.Url);

                // Wait for page to load
                await Task.Delay(1000, cancellationToken);
                string animeTitle;
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

                task.AddLog($"Found {episodeElements.Count} potential episodes to scrape.",LogLevel.Debug);
                
                // 3. Extract episode IDs and numbers
                for (var i = 0; i < episodeElements.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        task.AddLog("Cancellation requested. Halting scraping.",LogLevel.Info);
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
                            task.AddLog($"Skipping special episode {episodeNumber}.",LogLevel.Debug);
                            continue;
                        }

                        var episodeId = element.GetAttribute("id");
                        episodeIds.Add((episodeId, episodeNumber, i));
                    }
                    catch (Exception ex)
                    {
                        task.AddLog($"Error extracting episode number from element index {i}: {ex.Message}",LogLevel.Error);
                    }
                }

                task.AddLog($"Filtered to {episodeIds.Count} valid episodes for scraping.",LogLevel.Info);
                task.EpisodesFound = episodeIds.Count;
                // 4. Scrape each episode link
                var scrapedLinks = new Dictionary<string,EpisodeInfo>();

                foreach (var (episodeId, episodeNumber, index) in episodeIds)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        task.AddLog("Stop requested. Halting scraping loop.",LogLevel.Debug);
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
                                ep = new EpisodeInfo(episodeNumber, link);
                            }
                            else
                            {
                                task.AddLog($"Failed to scrape episode {episodeNumber}",LogLevel.Error);
                            }
                        }  
                        catch (Exception ex)
                        {
                            task.AddLog($"Error scraping episode {episodeNumber}: {ex.Message}",LogLevel.Error);
                        }
                    }
                    if (ep == null) continue;
                    scrapedLinks.Add(episodeNumber, ep);
                    task.EpisodesScraped++;
                    task.AddLog($"Extracted link for episode {episodeNumber}",LogLevel.Debug);
                    taskCoordinator.AddEpisodeTask(episodeNumber,ep.DownloadLink);
                }
                task.AddLog("Scraping complete. Saving to cache...",LogLevel.Debug);
                CacheService.SaveCacheAsync(task.Url, animeTitle, scrapedLinks);
                result.Title = animeTitle;
                result.Episodes = scrapedLinks;
                result.Success = true;
            }
            catch (Exception ex)
            {
                task.AddLog($"Fatal error during scraping: {ex.Message}",LogLevel.Error);
                task.UpdateStatus(TaskStatus.Error, $"Fatal Scraping Error: {ex.Message}");
                result.Success = false;
            }
            finally
            {
                Dispose();
            }

            return result;
        }

        private void InitializeWebDriver()
        {
            var config = ConfigurationManager.Instance;
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var driversDir = Path.Combine(appDir, "Drivers");
            
            try
            {
                switch (config.BrowserType)
                {
                    case BrowserType.Chrome:
                        InitializeChromeDriver(driversDir, config);
                        break;
                    case BrowserType.Firefox:
                        InitializeFirefoxDriver(driversDir, config);
                        break;
                    case BrowserType.Edge:
                        InitializeEdgeDriver(driversDir, config);
                        break;
                    default:
                        throw new NotSupportedException($"Browser type {config.BrowserType} is not supported");
                }

                if (_driver == null) return;
                _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(config.PageLoadTimeout);
                    
                if (config.HeadlessMode)
                {
                    _driver.Manage().Window.Position = new System.Drawing.Point(-2000, 0);
                }
                else
                {
                    _driver.Manage().Window.Minimize();
                }
                    
                Logger.Info($"Initialized {config.BrowserType} WebDriver with Headless={(config.HeadlessMode ? "Yes" : "No")}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to initialize WebDriver: {ex.Message}");
                throw;
            }
        }

        private void InitializeChromeDriver(string driversDir, ConfigurationManager config)
        {
            Logger.Debug("Initializing Chrome WebDriver");
            
            var options = new ChromeOptions();
            var service = ChromeDriverService.CreateDefaultService(driversDir);
            
            ConfigureCommonBrowserOptions(options, config);
            
            // Chrome-specific options
            options.AddArgument("--disable-gpu");
            options.AddArgument("--disable-dev-shm-usage");
            
            if (!string.IsNullOrEmpty(config.UserDataDir))
            {
                var userDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AnimeBingeDownloader",
                    "ChromeData");
                
                Directory.CreateDirectory(userDataDir);
                options.AddArgument($"--user-data-dir={userDataDir}");
            }
            
            service.HideCommandPromptWindow = true;
            _driver = new ChromeDriver(service, options);
        }

        private void InitializeFirefoxDriver(string driversDir, ConfigurationManager config)
        {
            Logger.Debug("Initializing Firefox WebDriver");
            
            var options = new FirefoxOptions();
            var service = FirefoxDriverService.CreateDefaultService(driversDir);
            
            ConfigureCommonBrowserOptions(options, config);
            
            if (!string.IsNullOrEmpty(config.UserDataDir))
            {
                var profileManager = new FirefoxProfileManager();
                var profile = profileManager.GetProfile("AnimeBingeDownloader") ?? new FirefoxProfile();
                options.Profile = profile;
            }
            
            service.HideCommandPromptWindow = true;
            _driver = new FirefoxDriver(service, options);
        }

        private void InitializeEdgeDriver(string driversDir, ConfigurationManager config)
        {
            Logger.Debug("Initializing Edge WebDriver");
            
            var options = new EdgeOptions();
            var service = EdgeDriverService.CreateDefaultService(driversDir);
            
            ConfigureCommonBrowserOptions(options, config);
            
            if (!string.IsNullOrEmpty(config.UserDataDir))
            {
                var userDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AnimeBingeDownloader",
                    "EdgeData");
                
                Directory.CreateDirectory(userDataDir);
                options.AddArgument($"--user-data-dir={userDataDir}");
            }
            
            service.HideCommandPromptWindow = true;
            _driver = new EdgeDriver(service, options);
        }
        
        private static void ConfigureCommonBrowserOptions<T>(T options, ConfigurationManager config) where T : DriverOptions
        {
            // Common configuration for all browsers
            if (config.HeadlessMode)
            {
                switch (options)
                {
                    case ChromeOptions chromeOptions:
                        chromeOptions.AddArgument("--headless");
                        break;
                    case FirefoxOptions firefoxOptions:
                        firefoxOptions.AddArgument("--headless");
                        break;
                    case EdgeOptions edgeOptions:
                        edgeOptions.AddArgument("--headless");
                        break;
                }
            }
            
            if (config.DisableImages)
            {
                switch (options)
                {
                    case ChromeOptions chromeOptions:
                        chromeOptions.AddArgument("--blink-settings=imagesEnabled=false");
                        chromeOptions.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
                        break;
                    case FirefoxOptions firefoxOptions:
                        firefoxOptions.SetPreference("permissions.default.image", 2);
                        break;
                    case EdgeOptions edgeOptions:
                        edgeOptions.AddArgument("--blink-settings=imagesEnabled=false");
                        edgeOptions.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
                        break;
                }
            }
            
            if (config.DisableJavaScript)
            {
                switch (options)
                {
                    case ChromeOptions chromeOptions:
                        chromeOptions.AddArgument("--disable-javascript");
                        chromeOptions.AddUserProfilePreference("javascript.enabled", false);
                        break;
                    case FirefoxOptions firefoxOptions:
                        firefoxOptions.SetPreference("javascript.enabled", false);
                        break;
                    case EdgeOptions edgeOptions:
                        edgeOptions.AddArgument("--disable-javascript");
                        edgeOptions.AddUserProfilePreference("javascript.enabled", false);
                        break;
                }
            }
            
            // Set page load timeout
            options.PageLoadStrategy = PageLoadStrategy.Normal;
            
            // Set user agent if specified
            if (!string.IsNullOrEmpty(config.UserAgent))
            {
                switch (options)
                {
                    case ChromeOptions chromeOptions:
                        chromeOptions.AddArgument($"--user-agent={config.UserAgent}");
                        break;
                    case FirefoxOptions firefoxOptions:
                        firefoxOptions.SetPreference("general.useragent.override", config.UserAgent);
                        break;
                    case EdgeOptions edgeOptions:
                        edgeOptions.AddArgument($"--user-agent={config.UserAgent}");
                        break;
                }
            }

            switch (options)
            {
                // Disable automation flags that might trigger bot detection
                case ChromeOptions co:
                    co.AddExcludedArgument("enable-automation");
                    co.AddAdditionalOption("useAutomationExtension", false);
                    break;
                case EdgeOptions eo:
                    eo.AddExcludedArgument("enable-automation");
                    eo.AddAdditionalOption("useAutomationExtension", false);
                    break;
            }
        }

        private string ScrapeAnimeTitle(TaskViewModel task)
        {
            var fallbackTitle = $"{AnimeTimePlaceholder} ({task.Url})";
            try
            {
                var titleElement = _driver?.FindElement(By.XPath("/html/body/div[3]/div/div[2]/div[1]"));
                var title = titleElement?.Text.Replace("Watch ", "").Trim();
                task.AddLog($"Identified Anime Title: {title}",LogLevel.Info);
                return title ?? fallbackTitle;
            }
            catch
            {
                task.AddLog("Could not find page title element dynamically. Using placeholder.",LogLevel.Warning);
                return fallbackTitle;
            }
        }

        private List<IWebElement> GetEpisodeElements()
        {
            try
            {
                const string mainContainerXPath = "/html/body/div[4]";
                var mainContainer = _driver?.FindElement(By.XPath(mainContainerXPath));
                var allInnerDivs = mainContainer?.FindElements(By.XPath("./div")).ToList();

                IWebElement? episodesContainer;

                switch (allInnerDivs!.Count)
                {
                    case > 2:
                        episodesContainer = allInnerDivs[1];
                        Logger.Debug(
                            "Found multiple potential episode containers. Using the second inner div (index 1).");
                        break;
                    case 2:
                        episodesContainer = allInnerDivs[0];
                        Logger.Debug("Found a single inner div. Using it as the episode container.");
                        break;
                    default:
                        episodesContainer = mainContainer;
                        Logger.Debug(
                            "No inner divs found. Using the main div[4] as a fallback episode container.");
                        break;
                }

                return episodesContainer?.FindElements(By.TagName("a")).ToList() ?? new List<IWebElement>();
            }
            catch (Exception ex)
            {
                Logger.Error($"Error finding episode container: {ex.Message}");
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
                    episodeElement = _driver?.FindElement(By.XPath(targetXPath)) ?? throw new Exception("Element not found");
                }
                catch
                {
                    Logger.Error("Error locating episode element by XPath, trying by index instead.");
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
                    Logger.Error($"Could not find download link for episode {episodeNumber}");
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
                Logger.Error($"Error while waiting for link element for episode {episodeNumber}: {ex.Message}");
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
                Logger.Debug("WebDriver cleanup complete.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error closing WebDriver: {ex.Message}");
            }
        }
    }

    public class ScrapingResult(string title, bool success, string errorMessage)
    {
        public string Title { get; set; } = title;
        public Dictionary<string,EpisodeInfo> Episodes { get; set; } = new();
        public bool Success { get; set; } = success;
        public string ErrorMessage { get; } = errorMessage;
    }

    public class EpisodeInfo
    {
        // Parameterless constructor for deserialization
        public EpisodeInfo() { }

        public EpisodeInfo(string episodeNumber, string downloadLink)
        {
            EpisodeNumber = episodeNumber;
            DownloadLink = downloadLink;
        }

        [JsonPropertyName("episodeNumber")]
        public string EpisodeNumber { get; set; } = string.Empty;
        
        [JsonPropertyName("downloadLink")]
        public string DownloadLink { get; set; } = string.Empty;
    }
}