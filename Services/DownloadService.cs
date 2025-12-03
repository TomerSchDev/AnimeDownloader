using System.Net.Http;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Utils;


namespace AnimeBingeDownloader.Services
{

    public class DownloadService
    {
        private readonly Logger _logger = new("[DownloadService] ");
        private readonly NotificationService _notificationService;

        private readonly HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        
        public static DownloadService Instance { get; } = new();

        private DownloadService()
        {
            _notificationService = NotificationService.Instance;
            _maxConcurrentDownloads = ConfigurationManager.Instance.MaxConcurrentDownloads;
            _concurrencyLimiter = new SemaphoreSlim(_maxConcurrentDownloads, _maxConcurrentDownloads);
            _workerTasks = new Task[_maxConcurrentDownloads];
            
            // Subscribe to configuration changes
            ConfigurationManager.Instance.PropertyChanged += OnConfigurationChanged;
        }
        
        private void OnConfigurationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConfigurationManager.MaxConcurrentDownloads))
            {
                UpdateMaxConcurrentDownloads(ConfigurationManager.Instance.MaxConcurrentDownloads);
            }
        }
        
        private void UpdateMaxConcurrentDownloads(int newMax)
        {
            if (newMax <= 0) return;
            
            lock (_queueLock)
            {
                if (newMax == _maxConcurrentDownloads) return;
                
                _logger.Info($"Updating max concurrent downloads from {_maxConcurrentDownloads} to {newMax}");
                
                // Update semaphore
                if (newMax > _maxConcurrentDownloads)
                {
                    // If increasing, release more slots
                    _concurrencyLimiter.Release(newMax - _maxConcurrentDownloads);
                }
                else
                {
                    // If decreasing, we'll let the semaphore naturally reduce as workers complete
                    // We can't directly reduce the semaphore count, so we'll just let it naturally adjust
                }
                
                // Update worker tasks if they've been started
                if (_workersStarted)
                {
                    if (newMax > _maxConcurrentDownloads)
                    {
                        // Add more workers
                        var oldTasks = _workerTasks;
                        _workerTasks = new Task[newMax];
                        Array.Copy(oldTasks, _workerTasks, oldTasks.Length);
                        
                        for (var i = _maxConcurrentDownloads; i < newMax; i++)
                        {
                            var workerId = i;
                            _workerTasks[i] = Task.Run(() => WorkersLoop(workerId));
                        }
                    }
                    // Note: We don't reduce the number of workers when decreasing the limit
                    // The extra workers will complete their current work and then exit naturally
                }
                
                _maxConcurrentDownloads = newMax;
            }
        }

        private readonly IndexedPriorityQueue<EpisodeTask, TaskPriority> _queue = new();
        private int _maxConcurrentDownloads;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly Lock _queueLock = new();
        private Task[] _workerTasks;
        private bool _workersStarted;
        private readonly Lock _startLock = new();
        private readonly CancellationTokenSource _shutdownTokenSource = new();
        
        

        private void EnsureWorkersStarted()
        {
            if (_workersStarted) return;

            lock (_startLock)
            {
                if (_workersStarted) return;

                for (var i = 0; i < _maxConcurrentDownloads; i++)
                {
                    var workerId = i;
                    _workerTasks[i] = Task.Run(() => WorkersLoop(workerId));
                }

                _workersStarted = true;
            }
        }

        private async Task WorkersLoop(int workerId)
        {
            var shutdownToken = _shutdownTokenSource.Token;
            
            try
            {
                while (!shutdownToken.IsCancellationRequested)
                {
                    var acquiredConcurrency = false;
                    
                    try
                    {
                        // Try to get work from queue
                        EpisodeTask episodeTask;
                        lock (_queueLock)
                        {
                            if (_queue.Count > 0)
                            {
                                episodeTask = _queue.Dequeue();
                            }
                            else
                            {
                                continue;
                            }
                        }
                        
                        // No work available, wait a bit and continue

                        if (episodeTask.RetryCount > 0)
                        {
                            var lastTime = (DateTime.Now - episodeTask.LastRetryTime)?.Seconds ?? ConfigurationManager.Instance.MinTimeAfterError;
                            if (lastTime <ConfigurationManager.Instance.MinTimeAfterError)
                            {
                                Thread.Sleep((ConfigurationManager.Instance.MinTimeAfterError-lastTime)*1000);
                            }
                        }
                            
                        var parentTask = episodeTask.GetParentTask();
                        
                        // Wait for available download slot
                        await _concurrencyLimiter.WaitAsync(shutdownToken);
                        acquiredConcurrency = true;
                        
                        var result = await episodeTask.Download(_httpClient, parentTask.CancellationToken);
                        
                        // Re-queue if not completed
                        if (result != EpisodeDownloadResult.Completed)
                        {
                            var priority = parentTask.Priority;
                            if (result == EpisodeDownloadResult.Error)
                            {
                                episodeTask.RetryCount++;
                                episodeTask.LastRetryTime= DateTime.Now;
                                if (episodeTask.RetryCount >= ConfigurationManager.Instance.MaxRetryAttempts)
                                {
                                    var message = $"Failed to download {parentTask.Title} - Episode {episodeTask.EpisodeNumber} after {episodeTask.RetryCount} attempts";
                                    episodeTask.AddLog(message, LogLevel.Error);
                                    _notificationService.ShowNotification(message, NotificationType.Error);
                                    parentTask.AddError(episodeTask.EpisodeNumber);
                                    continue;
                                }
                            }
                            lock (_queueLock)
                            {
                                _queue.Enqueue(episodeTask, priority);
                            }
                        }
                        
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutdown requested
                        break;
                    }
                    catch (Exception ex)
                    {
                        // Log error if possible
                        try
                        {
                            AppLogger.Logger.Error($"Worker {workerId} error: {ex.Message}");
                        }
                        catch
                        {
                            System.Diagnostics.Debug.WriteLine($"Worker {workerId} error: {ex}");
                        }
                    }
                    finally
                    {
                        // Only release if we actually acquired it
                        if (acquiredConcurrency)
                        {
                            _concurrencyLimiter.Release();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                // Critical error in worker thread
                System.Diagnostics.Debug.WriteLine($"Worker {workerId} crashed: {e}");
            }
        }

        public void AddToQueue(EpisodeTask episodeTask, TaskPriority priority)
        {
            EnsureWorkersStarted(); // Start workers on first use
            
            lock (_queueLock)
            {
                _queue.Enqueue(episodeTask, priority);
            }
        }

        public void UpdatePriority(EpisodeTask episodeTask, TaskPriority priority)
        {
            lock (_queueLock)
            {
                _queue.UpdatePriority(episodeTask, priority);
            }
        }
        
        public void Shutdown()
        {
            try
            {
                // Unsubscribe from configuration changes
                ConfigurationManager.Instance.PropertyChanged -= OnConfigurationChanged;
                
                _shutdownTokenSource.Cancel();
                
                // Filter out null tasks and wait for them to complete
                var tasksToWait = _workerTasks?.Where(t => t != null && !t.IsCompleted).ToArray() ?? Array.Empty<Task>();
                
                if (tasksToWait.Length > 0)
                {
                    _logger.Info($"Waiting for {tasksToWait.Length} download workers to complete...");
                    Task.WaitAll(tasksToWait, TimeSpan.FromSeconds(10));
                }
                
                // Clean up resources
                _concurrencyLimiter?.Dispose();
                _shutdownTokenSource.Dispose();
                
                _logger.Info("Download service shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error during download service shutdown: {ex.Message}");
                throw;
            }
        }
    }
    
}