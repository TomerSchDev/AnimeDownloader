using System.IO;
using AnimeBingeDownloader.Models;
using AnimeBingeDownloader.Services;

namespace AnimeBingeDownloader.Models;

public class Logger(string prefix)
{
    private readonly List<LogMessage> _logMessages = [];
    private IEnumerable<LogMessage> LogRecords => _logMessages;
    private LogLevel _minLogLevel = LogLevel.Info;
    
    internal delegate void LogAddedEventHandler(LogMessage newLog);
    internal event LogAddedEventHandler? LogAdded ;
    
    public string Prefix => prefix;
    public void SetMinimumLogLevel(LogLevel level)
    {
        _minLogLevel = level;
    }
    
    private void AddLog(string message, LogLevel level = LogLevel.Info)
    {
        if (level < _minLogLevel)
            return;
            
        var logMessage = new LogMessage($"[{level}] {message}", DateTime.Now, level);
        _logMessages.Add(logMessage);
        LogAdded?.Invoke(logMessage);
    }
    
    public void Trace(string message) => AddLog(message, LogLevel.Trace);
    public void Debug(string message) => AddLog(message, LogLevel.Debug);
    public void Info(string message) => AddLog(message, LogLevel.Info);
    public void Warning(string message) => AddLog(message, LogLevel.Warning);
    public void Error(string message) => AddLog(message, LogLevel.Error);
    public void Critical(string message) => AddLog(message, LogLevel.Critical);

    
    public static List<string> GetMargeLoggers(List<Logger>?loggers)
    {
        if (loggers == null || loggers.Count == 0)
        {
            return [];
        }

        // 1. Flatten and Transform using SelectMany:
        // This takes the array of Loggers (Logger[]) and flattens them into 
        // a single sequence of prefixed log strings (IEnumerable<string>).
        var allPrefixedLogs = loggers.SelectMany(logger =>
        {
            // For each logger, take its LogRecords and transform each one
            return logger.LogRecords.Select(log => new 
            {
                // Create an anonymous object containing the necessary data for sorting and output
                FormattedLog = FormatLogMessage(log,logger.Prefix),
                LogTime = log.Time
            });
        });

        // 2. OrderBy:
        // Sort the sequence by the LogTime property (ascending).
        var sortedLogs = allPrefixedLogs
            .OrderBy(item => item.LogTime)
            .ToList(); // Executes the query and stores the sorted results

        // 3. Select Final String Output:
        // Convert the sorted anonymous objects into the final List<string> required by the signature.
        return sortedLogs
            .Select(item => item.FormattedLog)
            .ToList();
    }

    internal static string FormatLogMessage(LogMessage logMessage, string prefix)
    {
        return $"[{logMessage.Time}] [{logMessage.Level}] {prefix} {logMessage.Log}";
    }
}

public class MegaLogger
{
    private readonly List<LogMessage> _megaAllLogMessages = [];
    private readonly List<LogMessage> _megaNewLogMessages = [];
    private readonly Dictionary<LogMessage, string> _prefixMap = new();
    private bool _enableFileOutput = false;
    private string _logFileName;


    public void Subscribe(Logger logger)
    {
        // Subscribe the HandleNewLog method to the logger's LogAdded event
        logger.LogAdded += (newLog) => HandleNewLog(newLog, logger.Prefix);

        // The logger.Prefix is accessible because the Logger's prefix field
        // is likely available (or can be accessed via a property in the wrapper).
    }
    public void Subscribe(Logger logger,bool isToPrintToScreen,bool isToFile,string logFile)
    {
        _enableConsoleOutput = isToPrintToScreen;
        _enableFileOutput = isToFile;
        _logFileName = logFile;
        
        // Subscribe the HandleNewLog method to the logger's LogAdded event
        logger.LogAdded += (newLog) => HandleNewLog(newLog, logger.Prefix);

        // The logger.Prefix is accessible because the Logger's prefix field
        // is likely available (or can be accessed via a property in the wrapper).
    }
    private bool _enableConsoleOutput = true;  // Default to true for initialization
    private async void HandleNewLog(LogMessage newLog, string prefix)
    {
        try
        {
            try
            {
                // Format the log message with timestamp, log level, and prefix
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{newLog.Level}] [{prefix}] {newLog.Log}\n";

                // Output to console if enabled
                if (_enableConsoleOutput)
                {
                    Console.Write(logEntry);
                }
            
                // Write to file if enabled
                if (_enableFileOutput && !string.IsNullOrEmpty(_logFileName))
                {
                    await WriteLogToFileAsync(logEntry);
                }
            }
            catch (Exception ex)
            {
                // Log the error to debug output
                System.Diagnostics.Debug.WriteLine($"Error in HandleNewLog: {ex.Message}");
            }
        
            // Always store the log, even if output fails
            lock (_megaAllLogMessages)
            {
                _megaAllLogMessages.Add(newLog);
                _prefixMap[newLog] = prefix;
            }

            lock (_megaNewLogMessages)
            {
                _megaNewLogMessages.Add(newLog);
            }
        }
        catch (Exception ex)
        {
            // Log the error to debug output
            System.Diagnostics.Debug.WriteLine($"Error in HandleNewLog: {ex.Message}");
        }
    }
    private async Task WriteLogToFileAsync(string log)
    {
        if (string.IsNullOrEmpty(_logFileName) || !_enableFileOutput)
            return;

        var filePath = AppStorageService.GetPath(_logFileName);
        try
        {
            // Check if log file exists and exceeds max size
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Exists)
            {
                // Get max size from configuration (in bytes)
                var maxSizeBytes = ConfigurationManager.Instance.MaxLogSizeMb * 1024 * 1024;
                
                if (fileInfo.Length > maxSizeBytes)
                {
                    // Create backup of current log
                    var backupPath = $"{filePath}.1";
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    
                    File.Move(filePath, backupPath);
                }
            }

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Append the log entry
            await File.AppendAllTextAsync(filePath, log);
        }
        catch (Exception ex)
        {
            // If we can't write to the log file, at least write to debug output
            System.Diagnostics.Debug.WriteLine($"Failed to write to log file {filePath}: {ex.Message}");
        }
    }
    public void UpdatePrintLogs(bool toPrint)
    {
        _enableConsoleOutput = toPrint;
    }
    public void UpdateFileOutput(bool toFile,string fileName)
    {
        _enableFileOutput = toFile;
        _logFileName = fileName;
    }
    public List<string> GetAllSortedMegaLog()
    {
        // 1. Retrieve and format logs with their prefixes
        var allLogs = _megaAllLogMessages.Select(log => new
        {
            FormattedLog = $"[{log.Time}] [{log.Level}] {_prefixMap[log]} {log.Log}",
            LogTime = log.Time
        });

        // 2. Sort by time and return the final strings
        return allLogs
            .OrderBy(item => item.LogTime)
            .Select(item => item.FormattedLog)
            .ToList();
    }

    public List<string> GetNewSortedMegaLog()
    {
        List<string> ret;
        lock (_megaNewLogMessages)
        {
            var allLogs = _megaNewLogMessages.Select(log => new
            {
                FormattedLog = $"[{log.Time}] [{log.Level}] {_prefixMap[log]} {log.Log}",
                LogTime = log.Time
            });

            _megaNewLogMessages.Clear();
            ret = allLogs
                .OrderBy(item => item.LogTime)
                .Select(item => item.FormattedLog)
                .ToList();
        }

        return ret;
        // 2. Sort by time and return the final strings

    }
    
};

internal record LoggerData(string Prefix, IEnumerable<LogMessage> LogRecords);
internal record LogMessage(string Log,DateTime Time, LogLevel Level)
{
    public readonly string Log = Log;
    public readonly DateTime Time = Time;
    public readonly LogLevel Level = Level;
};
