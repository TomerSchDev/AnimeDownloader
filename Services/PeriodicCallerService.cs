using AnimeBingeDownloader.Views;

namespace AnimeBingeDownloader.Models;

public class PeriodicCallerService
{
    public static PeriodicCallerService Instance { get; }= new();
    private Dictionary<string,Timer> PeriodicCalls { get; } = new();
    private readonly Logger _logger = new("PeriodicCaller");
    public Logger Logger => _logger;

    private PeriodicCallerService()
    {
        Utils.AppLogger.MegaLogger.Subscribe(_logger);

    }
    public string AddNewCall(TimerCallback callback,object? state,int dueTime,int period)
    {
        var callId = Guid.NewGuid().ToString();
        var timer = new Timer(callback,state,dueTime,period);
        PeriodicCalls[callId] = timer;
        _logger.AddLog($"Added new call {callId}, callback: {callback} with due time {dueTime} and period {period} ");
        return callId;
    }

    public void RemoveCall(string callId)
    {
        if(string.IsNullOrEmpty(callId)) return;
        if (!PeriodicCalls.TryGetValue(callId, out var call)) return;
        _logger.AddLog($"Removed call {callId}");
        PeriodicCalls.Remove(callId);
        call.Dispose();
    }

    public void ClearCalls()
    {
        lock (PeriodicCalls)
        {
            foreach (var callId in PeriodicCalls.Keys)
            {
                PeriodicCalls[callId].Dispose();
            }
            PeriodicCalls.Clear();
        }
       
        _logger.AddLog("Removed all the calls");
    }
        
}