using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ETTerms.Infrastructure;

/// <summary>支援一鍵設定 MCP 的 AI CLI 目標。</summary>
public enum McpTarget { Claude, Kiro }

/// <summary>
/// 把 ETTerms 的 MCP servers（<c>ETTerms.SerialMcp</c> 與 <c>ETTerms.PduMcp</c>）一鍵註冊 /
/// 移除到各 AI CLI 的「使用者層級」MCP 設定檔。採 read-modify-write，保留檔內其他既有伺服器。
///
/// - Claude Code：<c>~/.claude.json</c> 頂層 <c>mcpServers</c>，entry 需 <c>type:"stdio"</c>。
/// - Kiro：<c>%USERPROFILE%\.kiro\settings\mcp.json</c> 頂層 <c>mcpServers</c>。
///
/// 兩個 server 一起註冊 / 移除（一鍵同時設定 serial 與 pdu）。
/// </summary>
public static class McpRegistrar
{
    /// <summary>一個可被註冊的 MCP server 描述：CLI 內名稱 + 發佈子資料夾 + 執行檔名。</summary>
    public sealed record McpServer(string Name, string PublishFolder, string ExeName);

    /// <summary>ETTerms 提供的所有 MCP servers。</summary>
    public static readonly IReadOnlyList<McpServer> Servers = new[]
    {
        new McpServer("etterms-serial", "ETTerms.SerialMcp", "ETTerms.SerialMcp.exe"),
        new McpServer("etterms-pdu",    "ETTerms.PduMcp",    "ETTerms.PduMcp.exe"),
    };

    public static string DisplayName(McpTarget t) => t switch
    {
        McpTarget.Claude => "Claude Code",
        McpTarget.Kiro => "Kiro",
        _ => t.ToString()
    };

    /// <summary>該 AI CLI 的使用者層級 MCP 設定檔路徑。</summary>
    public static string ConfigPath(McpTarget t)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return t switch
        {
            McpTarget.Claude => Path.Combine(home, ".claude.json"),
            McpTarget.Kiro => Path.Combine(home, ".kiro", "settings", "mcp.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(t))
        };
    }

    /// <summary>給使用者在 CLI 確認是否設定成功的指令（多行）。</summary>
    public static string VerifyHint(McpTarget t) => t switch
    {
        McpTarget.Claude =>
            "claude mcp list\r\n" +
            "#  應看到：etterms-serial / etterms-pdu  ✓ Connected\r\n" +
            "#  細節：  claude mcp get etterms-pdu",
        McpTarget.Kiro =>
            "kiro-cli mcp list\r\n" +
            "kiro-cli mcp status --name etterms-pdu\r\n" +
            "#  或在 Kiro IDE：點 ghost 圖示開 MCP Servers 面板查看狀態",
        _ => ""
    };

    /// <summary>找出某個 MCP server 執行檔路徑（找不到回傳最可能的位置作為註冊值）。</summary>
    public static string ResolveServerExe(McpServer server)
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new List<string>
        {
            Path.Combine(baseDir, server.PublishFolder, server.ExeName), // 發佈版（子資料夾）
            Path.Combine(baseDir, server.ExeName),                       // 同層
        };

        // 開發版 fallback：src\ETTerms\bin\<cfg>\net8.0-windows → src\<folder>\bin\<cfg>\net8.0
        try
        {
            var binCfg = new DirectoryInfo(baseDir);          // ...\net8.0-windows
            var config = binCfg.Parent?.Name ?? "Debug";       // Debug / Release
            var srcDir = binCfg.Parent?.Parent?.Parent?.Parent; // ...\src
            if (srcDir != null)
                candidates.Add(Path.Combine(srcDir.FullName, server.PublishFolder, "bin", config, "net8.0", server.ExeName));
        }
        catch { /* 路徑推導失敗就略過開發版 fallback */ }

        foreach (var c in candidates)
            if (File.Exists(c)) return c;
        return candidates[0]; // 都找不到 → 回發佈版預期位置
    }

    /// <summary>所有 server 執行檔是否都存在。</summary>
    public static bool ServerExeExists() => Servers.All(s => File.Exists(ResolveServerExe(s)));

    /// <summary>列出每個 server 的解析路徑與是否存在（給 UI 顯示）。</summary>
    public static IEnumerable<(string Name, string Exe, bool Exists)> ServerInfos()
    {
        foreach (var s in Servers)
        {
            var exe = ResolveServerExe(s);
            yield return (s.Name, exe, File.Exists(exe));
        }
    }

    /// <summary>該目標是否已註冊「全部」ETTerms MCP servers。</summary>
    public static bool IsRegistered(McpTarget t)
    {
        try
        {
            var path = ConfigPath(t);
            if (!File.Exists(path)) return false;
            var servers = (JsonNode.Parse(File.ReadAllText(path)) as JsonObject)?["mcpServers"] as JsonObject;
            if (servers == null) return false;
            return Servers.All(s => servers[s.Name] != null);
        }
        catch { return false; }
    }

    /// <summary>註冊（或更新）所有 ETTerms MCP servers 到該目標設定檔。</summary>
    public static void Register(McpTarget t)
    {
        var path = ConfigPath(t);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var root = LoadRoot(path);
        if (root["mcpServers"] is not JsonObject servers)
        {
            servers = new JsonObject();
            root["mcpServers"] = servers;
        }
        foreach (var s in Servers)
            servers[s.Name] = BuildEntry(t, s);
        WriteRoot(path, root);
        AppLogger.Info($"MCP registered to {DisplayName(t)} at {path}");
    }

    /// <summary>從該目標設定檔移除所有 ETTerms MCP servers。</summary>
    public static void Unregister(McpTarget t)
    {
        var path = ConfigPath(t);
        if (!File.Exists(path)) return;
        var root = LoadRoot(path);
        if (root["mcpServers"] is JsonObject servers)
        {
            bool changed = false;
            foreach (var s in Servers)
                changed |= servers.Remove(s.Name);
            if (changed)
            {
                WriteRoot(path, root);
                AppLogger.Info($"MCP unregistered from {DisplayName(t)} at {path}");
            }
        }
    }

    private static JsonObject BuildEntry(McpTarget t, McpServer server)
    {
        var exe = ResolveServerExe(server);
        return t switch
        {
            // Claude Code：stdio server 需 type 欄位
            McpTarget.Claude => new JsonObject
            {
                ["type"] = "stdio",
                ["command"] = exe,
                ["args"] = new JsonArray()
            },
            // Kiro：local server，附 env / disabled / autoApprove 預設
            McpTarget.Kiro => new JsonObject
            {
                ["command"] = exe,
                ["args"] = new JsonArray(),
                ["env"] = new JsonObject(),
                ["disabled"] = false,
                ["autoApprove"] = new JsonArray()
            },
            _ => throw new ArgumentOutOfRangeException(nameof(t))
        };
    }

    /// <summary>讀入設定檔為可變 JSON 物件；不存在回空物件。檔案存在但格式錯誤則丟例外（不覆蓋使用者資料）。</summary>
    private static JsonObject LoadRoot(string path)
    {
        if (!File.Exists(path)) return new JsonObject();
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        if (JsonNode.Parse(text) is JsonObject obj) return obj;
        throw new InvalidDataException($"{path} 不是有效的 JSON 物件，為避免覆蓋資料已中止。請手動檢查該檔。");
    }

    /// <summary>原子寫回（先寫 .tmp 再 replace），避免半寫壞檔。</summary>
    private static void WriteRoot(string path, JsonObject root)
    {
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(path)) File.Replace(tmp, path, null);
        else File.Move(tmp, path);
    }
}
