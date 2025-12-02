using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Services;


namespace AnimeBingeDownloader.Services
{

    public class DownloadService
    {

        private readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
        
        public static DownloadService Instance { get; } = new DownloadService();

        private readonly IndexedPriorityQueue<EpisodeTask, TaskPriority> _queue = new();
        private readonly SemaphoreSlim _concurrencyLimiter = new SemaphoreSlim(Configuration.MaxWorkers, Configuration.MaxWorkers);
        private readonly Lock _queueLock = new Lock();
        private readonly Task[] _workerTasks;
        private bool _workersStarted = false;
        private readonly Lock _startLock = new Lock();
        private readonly CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();

        private DownloadService()
        {
            _workerTasks = new Task[Configuration.MaxWorkers];
        }

        public void EnsureWorkersStarted()
        {
            if (_workersStarted) return;

            lock (_startLock)
            {
                if (_workersStarted) return;

                for (var i = 0; i < Configuration.MaxWorkers; i++)
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
                    EpisodeTask episodeTask = null;
                    var acquiredConcurrency = false;
                    
                    try
                    {
                        // Try to get work from queue
                        lock (_queueLock)
                        {
                            if (_queue.Count > 0)
                            {
                                episodeTask = _queue.Dequeue();
                            }
                        }
                        
                        // No work available, wait a bit and continue
                        if (episodeTask == null)
                        {
                            await Task.Delay(100, shutdownToken);
                            continue;
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
                                if (episodeTask.RetryCount >= Configuration.RetryTimes)
                                {
                                    episodeTask.AddLog($"Got error retry too many times, removing");
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
                            episodeTask?.AddLog($"Worker {workerId} error: {ex.Message}");
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
            _shutdownTokenSource.Cancel();
            Task.WaitAll(_workerTasks.Where(t => t != null).ToArray(), TimeSpan.FromSeconds(5));
        }
    }
    
}