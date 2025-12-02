using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AnimeBingeDownloader.Models;


public sealed partial class EpisodeTask : INotifyPropertyChanged
{
    private EpisodeState _state;
    private long _totalSize;
    private long _downloadedSize;

    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string ParentTaskId { get; }
    private TaskViewModel Parent{get;set;}
    public string EpisodeNumber { get; }
    public string FilePath { get; }
    public string AnimeName { get; }
    public string DownloadLink { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastRetryTime { get; set; }
    private readonly Logger _logger;

    public string StatusStr => EnumTranslator.TranslateEnumToStr(_state);

    public EpisodeState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusStr));
        }
    }

    public long TotalSize
    {
        get => _totalSize;
        set
        {
            _totalSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercent));
            OnPropertyChanged(nameof(SizeDisplay));
        }
    }

    public long DownloadedSize
    {
        get => _downloadedSize;
        set
        {
            _downloadedSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProgressPercent));
        }
    }

    public double ProgressPercent =>
        TotalSize > 0 ? (double)DownloadedSize / TotalSize * 100.0 : 0.0;

    public string SizeDisplay
    {
        get
        {
            return TotalSize switch
            {
                < 1024 * 1024 => $"{TotalSize / 1024.0:0.00} KB",
                < 1024L * 1024L * 1024L => $"{TotalSize / (1024.0 * 1024.0):0.00} MB",
                _ => $"{TotalSize / (1024.0 * 1024.0 * 1024.0):0.00} GB"
            };
        }
    }

    public EpisodeTask(string parentTaskId, string episodeNumber, string filePath, string animeName,string downloadLink, TaskViewModel parent)
    {
        ParentTaskId = parentTaskId;
        EpisodeNumber = episodeNumber;
        FilePath = filePath;
        AnimeName = animeName;
            
        State = EpisodeState.WaitingForLink;
        DownloadLink = downloadLink;
        Parent = parent;
        _logger = new Logger($"[TASK {ParentTaskId}] [EPISODE {episodeNumber}] ");
    }

    public void AddLog(string message)
    {
        _logger.AddLog(message);
    }
    public Logger GetLogger() => _logger;
  

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    [GeneratedRegex(@"[\\/:*?""<>|]")]
    private static partial Regex MyRegex();

    #endregion

    public  TaskViewModel GetParentTask()
    {
        return Parent;
    }

    public async Task<EpisodeDownloadResult> Download(HttpClient client,CancellationToken cancellationToken)
    {
        /* <summary>
        /// Download the episode (resume-aware) – equivalent to Python download_episode.
         </summary>
        */
        
        var prefix = $"[E{EpisodeNumber.PadLeft(2, '0')}]";

        if (cancellationToken.IsCancellationRequested)
        {
            AddLog($"{prefix} CANCELED: Skipping download before start.");
            State = EpisodeState.Interrupted;
            return EpisodeDownloadResult.Cancelled;
        }

        Directory.CreateDirectory(FilePath);

        // Get extension from link
        var match = MyRegex1().Match(DownloadLink);
        var extension = match.Success ? "." + match.Groups[1].Value : ".mp4";

        var safeAnimeName = MyRegex().Replace(AnimeName, "").Trim();
        var fileName = $"{safeAnimeName} - E{EpisodeNumber.PadLeft(2, '0')}{extension}";
        var fullPath = Path.Combine(FilePath, fileName);

        long downloadedSize = 0;
        var request = new HttpRequestMessage(HttpMethod.Get, DownloadLink);

        var mode = FileMode.Create;

        if (File.Exists(fullPath))
        {
            downloadedSize = new FileInfo(fullPath).Length;
            if (downloadedSize > 0)
            {
                // Try to resume
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(downloadedSize, null);
                mode = FileMode.Append;
                AddLog($"{prefix} Resuming download from {downloadedSize} bytes.");
            }
            else
            {
                File.Delete(fullPath);
                AddLog($"{prefix} Found empty file, starting new download.");
            }
        }

        AddLog($"{prefix} Starting download for {fileName}...");
        State = EpisodeState.Downloading;
        Parent.Status = Parent.Status switch
        {
            TaskStatus.Scraping => TaskStatus.ScrapingAndDownloading,
            TaskStatus.FinishedScrapingAndDownloads or TaskStatus.FinishedScraping or TaskStatus.Completed => TaskStatus
                .Downloading,
            _ => Parent.Status
        };

        try
        {
            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            long totalSize = 0;

            if (response.Content.Headers.ContentRange != null)
            {
                totalSize = response.Content.Headers.ContentRange.Length ?? 0;
            }
            else if (response.Content.Headers.ContentLength.HasValue)
            {
                totalSize = response.Content.Headers.ContentLength.Value;
            }

            if (TotalSize < totalSize)
            {
                TotalSize = totalSize;
            }

            // Already complete?
            if (totalSize > 0 && downloadedSize >= totalSize && State == EpisodeState.Completed)
            {
                AddLog($"{prefix} File already exists and is complete. Skipping.");
                return EpisodeDownloadResult.Completed;
            }

            // If server ignored Range and returns full content (200)
            if (mode == FileMode.Append && response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                AddLog($"{prefix} Server ignored resume request. Restarting from scratch.");
                downloadedSize = 0;
                mode = FileMode.Create;
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }

            const int bufferSize = 8192;

            await using (var fs = new FileStream(fullPath, mode, FileAccess.Write, FileShare.None, bufferSize, true))
            await using (var stream = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                var buffer = new byte[bufferSize];
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fs.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    downloadedSize += read;
                    DownloadedSize = downloadedSize;
                    if (!cancellationToken.IsCancellationRequested) continue;
                    AddLog("Download aborted. cancelling.");
                    State = EpisodeState.Interrupted;
                    return EpisodeDownloadResult.Cancelled;
                    // Small optimization: you could throttle logs here if you want.
                }
            }

            var isComplete = totalSize > 0 && downloadedSize == totalSize;

            if (cancellationToken.IsCancellationRequested)
            {
                AddLog($"{prefix} INTERRUPTED: Partial file saved ({downloadedSize} bytes).");
                State = EpisodeState.Interrupted;
                return EpisodeDownloadResult.Cancelled;
            }

            if (isComplete)
            {
                AddLog($"{prefix} Successfully downloaded and saved.");
                State = EpisodeState.Completed;
                Parent.EpisodesCompleted += 1;
                return EpisodeDownloadResult.Completed;
            }

            AddLog($"{prefix} Download ended unexpectedly at {downloadedSize}/{totalSize} bytes. Retrying.");
            State = EpisodeState.Error;
            return EpisodeDownloadResult.Error;
        }
        catch (OperationCanceledException)
        {
            AddLog($"{prefix} INTERRUPTED: Partial file saved.");
            State = EpisodeState.Interrupted;
            return EpisodeDownloadResult.Error;
        }
        catch (HttpRequestException ex)
        {
            AddLog($"{prefix} Request failed: {ex.Message}");
            State = EpisodeState.Error;
            return EpisodeDownloadResult.Error;
        }
        catch (Exception ex)
        {
            AddLog($"{prefix} Critical error: {ex}");
            State = EpisodeState.Error;
            return EpisodeDownloadResult.Error;
        }
    }

    [GeneratedRegex(@"\.(\w{3,4})(?=\?|$)")]
    private static partial Regex MyRegex1();
}
