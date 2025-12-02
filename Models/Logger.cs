namespace AnimeBingeDownloader.Models;

public class Logger(string prefix)
{
    private readonly List<LogMessage> _logMessages = [];
    private IEnumerable<LogMessage> LogRecords => _logMessages;
    public void AddLog(string message)
    {
        _logMessages.Add(new LogMessage(message,DateTime.Now));
    }

    private string FormatLogMessage(LogMessage logMessage)
    {
        return $"[{logMessage.Time}] {prefix} {logMessage.Log}";
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
                FormattedLog = logger.FormatLogMessage(log),
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

}

internal record LoggerData(string Prefix, IEnumerable<LogMessage> LogRecords);
internal record LogMessage(string Log,DateTime Time)
{
    public readonly string Log = Log;
    public readonly DateTime Time = Time;
};
