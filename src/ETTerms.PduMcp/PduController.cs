using System.Net;
using SnmpSharpNet;

namespace ETTerms.PduMcp;

/// <summary>
/// PDU controller for iPoMan II/III models via SNMP (slim copy of the GUI's
/// ETTerms.Scripting.Pdu.PduController, ported into the standalone MCP server).
///
/// Differences from the GUI version:
///   • No dependency on ETTerms.Infrastructure.AppLogger — diagnostics go to stderr
///     (stdout is reserved for the MCP JSON-RPC stream).
///   • Adds <see cref="GetModelName"/> so tools can surface the device identity.
///
/// SNMP is connectionless (UDP) and non-exclusive, so this talks to the PDU directly
/// without bridging through the GUI.
/// </summary>
public sealed class PduController : IDisposable
{
    private readonly string _ip;
    private const string Community = "private";
    private const int SnmpPort = 161;
    private const int Timeout = 3000;

    public string Ip => _ip;

    public PduController(string ip) => _ip = ip;

    /// <summary>Reads the PDU model/name OID; non-empty containing "PDU" means reachable.</summary>
    public string? GetModelName() => SnmpGet(".1.3.6.1.4.1.2468.1.4.2.1.1.4");

    public bool CheckConnection()
    {
        var name = GetModelName();
        Log($"CheckConnection {_ip}: name='{name ?? "<null>"}'");
        return !string.IsNullOrEmpty(name) && name.Contains("PDU");
    }

    public bool SetPortOn(int port) => SnmpSet(PortControlOid(port), new Integer32(3));
    public bool SetPortOff(int port) => SnmpSet(PortControlOid(port), new Integer32(4));

    /// <summary>true = on, false = off, null = unknown/unreachable.</summary>
    public bool? GetPortState(int port)
    {
        var r = SnmpGet(PortStateOid(port));
        return r == "3" ? true : (r == "2" || r == "4") ? false : null;
    }

    public int? GetPortCurrent(int port) => int.TryParse(SnmpGet(PortCurrentOid(port)), out int v) ? v : null;
    public double? GetPortPowerWatts(int port) => int.TryParse(SnmpGet(PortPowerOid(port)), out int v) ? v / 10.0 : null;

    private static string PortControlOid(int port) => $".1.3.6.1.4.1.2468.1.4.2.1.3.2.4.1.2.{port}";
    private static string PortStateOid(int port) => $".1.3.6.1.4.1.2468.1.4.2.1.3.2.3.1.2.{port}";
    private static string PortCurrentOid(int port) => $".1.3.6.1.4.1.2468.1.4.2.1.3.2.3.1.3.{port}";
    private static string PortPowerOid(int port) => $".1.3.6.1.4.1.2468.1.4.2.1.3.2.3.1.5.{port}";

    private bool SnmpSet(string oid, AsnType value)
    {
        try
        {
            var param = new AgentParameters(new OctetString(Community)) { Version = SnmpVersion.Ver1 };
            using var target = new UdpTarget((IPAddress)new IpAddress(_ip), SnmpPort, Timeout, 1);
            var pdu = new SnmpSharpNet.Pdu(PduType.Set);
            pdu.VbList.Add(new Oid(oid), value);
            var result = (SnmpV1Packet)target.Request(pdu, param);
            return result?.Pdu.ErrorStatus == 0;
        }
        catch (Exception ex) { Log($"SNMP SET {oid} exception: {ex.Message}"); return false; }
    }

    private string? SnmpGet(string oid)
    {
        try
        {
            var param = new AgentParameters(new OctetString(Community)) { Version = SnmpVersion.Ver1 };
            using var target = new UdpTarget((IPAddress)new IpAddress(_ip), SnmpPort, Timeout, 1);
            var pdu = new SnmpSharpNet.Pdu(PduType.Get);
            pdu.VbList.Add(new Oid(oid));
            var result = (SnmpV1Packet)target.Request(pdu, param);
            if (result == null) { Log($"SNMP GET {oid}: no response (timeout)"); return null; }
            if (result.Pdu.ErrorStatus != 0) { Log($"SNMP GET {oid}: ErrorStatus={result.Pdu.ErrorStatus}"); return null; }
            foreach (var v in result.Pdu.VbList) return v.Value.ToString();
        }
        catch (Exception ex) { Log($"SNMP GET {oid} exception: {ex.Message}"); }
        return null;
    }

    // stdio MCP server：stdout 專供 JSON-RPC，診斷一律走 stderr。
    private static void Log(string msg) => Console.Error.WriteLine($"[PDU] {msg}");

    public void Dispose() { }
}
