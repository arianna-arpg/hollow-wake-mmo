namespace HollowWake.Client.UI.Logic;

/// <summary>A token-owned preparation. Superseded jobs cannot change or close a newer cover.</summary>
public sealed class LoadingSession
{
    private long _token;
    public bool Active { get; private set; }
    public bool Failed { get; private set; }
    public string Label { get; private set; } = "";
    public string Detail { get; private set; } = "";
    public double? Fraction { get; private set; }
    public Action? Retry { get; private set; }
    public Action? Cancel { get; private set; }

    public Lease Begin(string label, Action? cancel = null)
    {
        _token++; Active = true; Failed = false; Label = label; Detail = ""; Fraction = null; Retry = null; Cancel = cancel;
        return new(this, _token);
    }
    public void Close() { Active = false; _token++; Retry = Cancel = null; }

    public sealed class Lease(LoadingSession owner, long token)
    {
        public bool Current => owner.Active && owner._token == token;
        public void Update(string label, string detail = "", double? completed = null, double? total = null)
        {
            if (!Current) return;
            owner.Label = label; owner.Detail = detail; owner.Failed = false; owner.Retry = null;
            owner.Fraction = completed is { } n && total is { } d && double.IsFinite(n) && double.IsFinite(d) && d > 0 ? Math.Clamp(n / d, 0, 1) : null;
        }
        public void Fail(string label, string message, Action? retry = null)
        {
            if (!Current) return;
            Update(label, message); owner.Failed = true; owner.Retry = retry;
        }
        public void Finish() { if (Current) owner.Close(); }
    }
}
