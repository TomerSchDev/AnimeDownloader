using AnimeBingeDownloader.Models;

namespace AnimeBingeDownloader.Models;

public class Logger
{
    private readonly List<LogMessage> _logMessages = [];
    private IEnumerable<LogMessage> LogRecords => _logMessages;
    private readonly string _prefix;
    private LogLevel _minLogLevel = LogLevel.Info;
    
    internal delegate void LogAddedEventHandler(LogMessage newLog);
    internal event LogAddedEventHandler? LogAdded = null!;
    
    public string Prefix => _prefix;
    
    public Logger(string prefix)
    {
        _prefix = prefix;
    }
    
    public void SetMinimumLogLevel(LogLevel level)
    {
        _minLogLevel = level;
    }
    
    public void AddLog(string message, LogLevel level = LogLevel.Info)
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

    public void Subscribe(Logger logger)
    {
        // Subscribe the HandleNewLog method to the logger's LogAdded event
        logger.LogAdded += (newLog) => HandleNewLog(newLog, logger.Prefix);

        // The logger.Prefix is accessible because the Logger's prefix field
        // is likely available (or can be accessed via a property in the wrapper).
    }
    public void Subscribe(Logger logger,bool isToPrintToScreen)
    {
        _enableConsoleOutput = isToPrintToScreen;
        // Subscribe the HandleNewLog method to the logger's LogAdded event
        logger.LogAdded += (newLog) => HandleNewLog(newLog, logger.Prefix);

        // The logger.Prefix is accessible because the Logger's prefix field
        // is likely available (or can be accessed via a property in the wrapper).
    }
    private bool _enableConsoleOutput = true;  // Default to true for initialization
    
    private void HandleNewLog(LogMessage newLog, string prefix)
    {
        try
        {
            // Format the message first as it's needed in both console and storage
            var formattedMessage = Logger.FormatLogMessage(newLog, prefix);
            
            // Safe console output that won't throw if ConfigurationManager isn't ready
            if (_enableConsoleOutput)
            {
                Console.WriteLine(formattedMessage);
            }
        }
        catch
        {
            // If anything goes wrong with logging to console, just continue with storage
        }
        
        // Always store the log, even if console output fails
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

    public void UpdatePrintLogs(bool toPrint)
    {
        _enableConsoleOutput = toPrint;
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
