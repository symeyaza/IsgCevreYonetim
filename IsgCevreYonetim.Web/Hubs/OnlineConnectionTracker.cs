namespace IsgCevreYonetim.Web.Hubs;

// All tabs in one authentication session share a single presence record.
public sealed class OnlineConnectionTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, HashSet<string>> _sessions = new();

    public void Add(string sessionKey, string connectionId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionKey, out var ids))
                _sessions[sessionKey] = ids = new HashSet<string>();
            ids.Add(connectionId);
        }
    }

    public bool RemoveLast(string sessionKey, string connectionId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionKey, out var ids)) return false;
            ids.Remove(connectionId);
            if (ids.Count != 0) return false;
            _sessions.Remove(sessionKey);
            return true;
        }
    }

    public bool IsActive(string sessionKey)
    {
        lock (_gate) return _sessions.ContainsKey(sessionKey);
    }
}
