using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace ETTerms.PduMcp;

/// <summary>
/// MCP tools exposed to the AI for controlling an SNMP PDU (iPoMan II/III).
///
/// Unlike the serial bridge, the PDU is reached directly over SNMP — the ETTerms GUI
/// does NOT need to be running. Connections are held per-IP and persist for the lifetime
/// of this MCP server. Always <c>pdu_connect</c> an IP first, then control its ports.
/// </summary>
[McpServerToolType]
public static class PduTools
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    [McpServerTool, Description("Connect to a PDU over SNMP by IP and verify it responds. Required before controlling ports. Returns the model name on success.")]
    public static Task<string> pdu_connect(
        [Description("PDU IP address, e.g. 192.168.1.21")] string ip)
    {
        ip = (ip ?? "").Trim();
        if (ip.Length == 0) return Err("ip is required");
        var pdu = PduRegistry.Instance.GetOrAdd(ip);
        var model = pdu.GetModelName();
        bool ok = !string.IsNullOrEmpty(model) && model.Contains("PDU");
        if (!ok)
        {
            PduRegistry.Instance.Remove(ip);
            return Err($"no SNMP response from PDU at {ip} (check IP/network/community)");
        }
        return Ok(new { ip, model });
    }

    [McpServerTool, Description("List PDUs currently connected in this MCP session (by IP).")]
    public static Task<string> pdu_list()
        => Ok(new { connected = PduRegistry.Instance.ConnectedIps });

    [McpServerTool, Description("Turn a PDU outlet on or off. The PDU must be connected first via pdu_connect.")]
    public static Task<string> pdu_set_port(
        [Description("PDU IP address")] string ip,
        [Description("Outlet/port number (1-12)")] int port,
        [Description("true = ON, false = OFF")] bool on)
    {
        if (!TryResolve(ip, port, out var pdu, out var error)) return Err(error);
        bool ok = on ? pdu.SetPortOn(port) : pdu.SetPortOff(port);
        if (!ok) return Err($"SNMP set failed for {ip} port {port}");
        return Ok(new { ip, port, state = on ? "on" : "off" });
    }

    [McpServerTool, Description("Read a single outlet's state, current (mA) and power (W).")]
    public static Task<string> pdu_get_port(
        [Description("PDU IP address")] string ip,
        [Description("Outlet/port number (1-12)")] int port)
    {
        if (!TryResolve(ip, port, out var pdu, out var error)) return Err(error);
        return Ok(PortSnapshot(pdu, port));
    }

    [McpServerTool, Description("Read the state, current (mA) and power (W) of all outlets on the PDU.")]
    public static Task<string> pdu_status(
        [Description("PDU IP address")] string ip)
    {
        if (!PduRegistry.Instance.TryGet(ip, out var pdu))
            return Err($"PDU {ip} not connected. Call pdu_connect first.");
        var ports = new List<object>();
        for (int p = 1; p <= PduRegistry.PortCount; p++)
            ports.Add(PortSnapshot(pdu, p));
        return Ok(new { ip, ports });
    }

    [McpServerTool, Description("Power-cycle an outlet: turn it OFF, wait offSeconds, then turn it ON. Useful for rebooting a DUT.")]
    public static async Task<string> pdu_power_cycle(
        [Description("PDU IP address")] string ip,
        [Description("Outlet/port number (1-12)")] int port,
        [Description("Seconds to stay off before powering back on")] int offSeconds = 5)
    {
        if (!TryResolve(ip, port, out var pdu, out var error)) return await Err(error);
        if (offSeconds < 0) offSeconds = 0;
        if (!pdu.SetPortOff(port)) return await Err($"SNMP off failed for {ip} port {port}");
        await Task.Delay(offSeconds * 1000);
        if (!pdu.SetPortOn(port)) return await Err($"SNMP on failed for {ip} port {port}");
        return await Ok(new { ip, port, action = "power_cycle", offSeconds, state = "on" });
    }

    [McpServerTool, Description("Disconnect a PDU from this MCP session (does not change outlet states).")]
    public static Task<string> pdu_disconnect(
        [Description("PDU IP address")] string ip)
        => Ok(new { ip, removed = PduRegistry.Instance.Remove(ip) });

    // ── helpers ──

    private static bool TryResolve(string ip, int port, out PduController pdu, out string error)
    {
        pdu = null!;
        error = "";
        if (port < 1 || port > PduRegistry.PortCount)
        {
            error = $"port must be 1-{PduRegistry.PortCount}";
            return false;
        }
        if (!PduRegistry.Instance.TryGet(ip, out pdu))
        {
            error = $"PDU {ip} not connected. Call pdu_connect first.";
            return false;
        }
        return true;
    }

    private static object PortSnapshot(PduController pdu, int port)
    {
        var state = pdu.GetPortState(port);
        return new
        {
            port,
            state = state == true ? "on" : state == false ? "off" : "unknown",
            currentMilliAmps = pdu.GetPortCurrent(port),
            powerWatts = pdu.GetPortPowerWatts(port)
        };
    }

    private static Task<string> Ok(object payload)
    {
        var node = new { ok = true, result = payload };
        return Task.FromResult(JsonSerializer.Serialize(node, Json));
    }

    private static Task<string> Err(string message)
        => Task.FromResult(JsonSerializer.Serialize(new { ok = false, error = message }, Json));
}
