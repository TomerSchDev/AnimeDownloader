using AnimeBingeDownloader.Services;

namespace AnimeBingeDownloader.Models;

public class Logger(string prefix)
{
    private readonly List<LogMessage> _logMessages = [];
    private IEnumerable<LogMessage> LogRecords => _logMessages;
    internal delegate void LogAddedEventHandler(LogMessage newLog);
    
    // The actual event that listeners will subscribe to
    internal event LogAddedEventHandler? LogAdded = null!;
    internal string Prefix => prefix;
    public void AddLog(string message)
    {
        var logMessage = new LogMessage(message, DateTime.Now);
        _logMessages.Add(logMessage);
        LogAdded?.Invoke(logMessage);
    }

    
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

    internal static string FormatLogMessage(LogMessage logMessage,string prefix)
    {
        return $"[{logMessage.Time}] {prefix} {logMessage.Log}";
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

    private void HandleNewLog(LogMessage newLog, string prefix)
    {
        // IMPORTANT: Access to the shared list MUST be synchronized
        // (This assumes the prefix is accessible or stored in the ObservableLogger)
        if (ConfigurationManager.Instance.PrintToScreen)
        {
            var formatedMessage = Logger.FormatLogMessage(newLog, prefix);
            Console.WriteLine(formatedMessage);
        }

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

    public List<string> GetAllSortedMegaLog()
    {
        // 1. Retrieve and format logs with their prefixes
        var allLogs = _megaAllLogMessages.Select(log => new
        {
            FormattedLog = $"[{log.Time}] {_prefixMap[log]} {log.Log}",
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
                FormattedLog = $"[{log.Time}] {_prefixMap[log]} {log.Log}",
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
internal record LogMessage(string Log,DateTime Time)
{
    public readonly string Log = Log;
    public readonly DateTime Time = Time;
};
