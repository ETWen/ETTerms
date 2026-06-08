using System.Collections.Concurrent;

namespace ETTerms.PduMcp;

/// <summary>
/// Process-wide registry of connected PDUs, keyed by IP. The MCP server is long-lived,
/// so connections established by <c>pdu_connect</c> persist across tool calls.
/// </summary>
public sealed class PduRegistry
{
    public static readonly PduRegistry Instance = new();

    /// <summary>iPoMan II/III outlet count.</summary>
    public const int PortCount = 12;

    private readonly ConcurrentDictionary<string, PduController> _pdus = new(StringComparer.OrdinalIgnoreCase);

    public PduController GetOrAdd(string ip) =>
        _pdus.GetOrAdd(ip.Trim(), static k => new PduController(k));

    public bool TryGet(string ip, out PduController pdu) =>
        _pdus.TryGetValue(ip.Trim(), out pdu!);

    public bool Remove(string ip)
    {
        if (_pdus.TryRemove(ip.Trim(), out var pdu)) { pdu.Dispose(); return true; }
        return false;
    }

    public IReadOnlyCollection<string> ConnectedIps => _pdus.Keys.ToArray();
}
