using System.Net;
using SnmpSharpNet;

namespace ETTerms.Scripting.Pdu;

/// <summary>
/// PDU Controller for iPoMan II/III models via SNMP.
/// Ported from MyTeraTerm's PDUControlLib.
/// </summary>
public sealed class PduController : IDisposable
{
    private readonly string _ip;
    private const string Community = "private";
    private const int SnmpPort = 161;
    private const int Timeout = 3000;

    public PduController(string ip) => _ip = ip;

    public bool CheckConnection()
    {
        var name = SnmpGet(".1.3.6.1.4.1.2468.1.4.2.1.1.4");
        return !string.IsNullOrEmpty(name) && name.Contains("PDU");
    }

    public bool SetPortOn(int port) => SnmpSet(PortControlOid(port), new Integer32(3));
    public bool SetPortOff(int port) => SnmpSet(PortControlOid(port), new Integer32(4));

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
            var target = new UdpTarget((IPAddress)new IpAddress(_ip), SnmpPort, Timeout, 1);
            var pdu = new SnmpSharpNet.Pdu(PduType.Set);
            pdu.VbList.Add(new Oid(oid), value);
            var result = (SnmpV1Packet)target.Request(pdu, param);
            return result?.Pdu.ErrorStatus == 0;
        }
        catch { return false; }
    }

    private string? SnmpGet(string oid)
    {
        try
        {
            var param = new AgentParameters(new OctetString(Community)) { Version = SnmpVersion.Ver1 };
            var target = new UdpTarget((IPAddress)new IpAddress(_ip), SnmpPort, Timeout, 1);
            var pdu = new SnmpSharpNet.Pdu(PduType.Get);
            pdu.VbList.Add(new Oid(oid));
            var result = (SnmpV1Packet)target.Request(pdu, param);
            if (result?.Pdu.ErrorStatus == 0)
                foreach (var v in result.Pdu.VbList) return v.Value.ToString();
        }
        catch { }
        return null;
    }

    public void Dispose() { }
}
