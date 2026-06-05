using System.Drawing;
using System.Windows.Forms;
using ETTerms.App.Dialogs;
using ETTerms.Connections;

namespace ETTerms.App;

/// <summary>
/// Saved Connections 側欄（仿 KKTerm）：可自建資料夾、把連線分類進去。
/// 功能：搜尋、Quick Connect、新增資料夾 / 連線、改名、刪除、拖曳分類、展開 / 收合。
/// Phase 2：連線資料綁 <see cref="ConnectionStore"/>（SQLite）持久化，密碼走 <see cref="CredentialVault"/>。
/// 資料夾以連線的 GroupName（'/'-join 路徑）持久化；空資料夾為 session-only。
/// </summary>
public sealed class ConnectionSidebar : UserControl
{
    /// <summary>使用者啟用（雙擊 / Open / Quick Connect）一條連線時觸發。</summary>
    public event EventHandler<Connection>? ConnectionActivated;

    // ── in-memory tree（連線節點掛 Connection；資料夾節點只用 Name）──
    private enum Kind { Folder, Connection }

    private sealed class Node
    {
        public Kind Kind;
        public string Name = "";          // 資料夾名稱
        public Connection? Conn;          // 連線資料（Kind == Connection）
        public bool Expanded = true;
        public Node? Parent;
        public readonly List<Node> Children = new();

        public bool IsFolder => Kind == Kind.Folder;
        public string DisplayName => IsFolder ? Name : (Conn?.Name ?? "");
        public string Detail => Conn?.Detail ?? "";
        public bool IsSsh => Conn?.IsSsh ?? false;
    }

    private readonly Node _root = new() { Kind = Kind.Folder, Name = "root" };
    private readonly ConnectionStore _store = new();
    private int _sortSeq;

    private readonly TreeView _tree;
    private readonly TextBox _search;
    private TreeNode? _dragNode;

    public ConnectionSidebar()
    {
        Width = 250;
        Dock = DockStyle.Left;
        BackColor = Theme.SidebarBack;

        // ── Tab bar (Sessions / SFTP) ──
        // 高度 40 與右側 Workspace 工具列對齊，讓「CONNECTIONS / 連線清單」與右側分頁列同一條基準線
        var tabBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 40, BackColor = Theme.RailBack,
            Padding = new Padding(4, 7, 4, 0), WrapContents = false
        };

        var sessionsPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.SidebarBack };
        var sftpPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.SidebarBack, Visible = false };

        Button? activeTab = null;
        Button MakeTabBtn(string text, Panel panel)
        {
            var b = new Button
            {
                Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(70, 24), Padding = new Padding(8, 2, 8, 2),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont,
                Margin = new Padding(0, 0, 4, 0), Cursor = Cursors.Hand
            };
            b.FlatAppearance.BorderColor = Theme.Border;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.Click += (_, _) =>
            {
                sessionsPanel.Visible = panel == sessionsPanel;
                sftpPanel.Visible = panel == sftpPanel;
                if (activeTab != null) activeTab.BackColor = Theme.TabBack;
                b.BackColor = Theme.TabActiveBack;
                activeTab = b;
            };
            return b;
        }
        var sessBtn = MakeTabBtn("📡 Sessions", sessionsPanel);
        var sftpBtn = MakeTabBtn("📁 SFTP", sftpPanel);
        sessBtn.BackColor = Theme.TabActiveBack;
        activeTab = sessBtn;
        tabBar.Controls.Add(sessBtn);
        tabBar.Controls.Add(sftpBtn);

        // ── Sessions panel content ──
        var header = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.SidebarBack };
        var title = new Label
        {
            Text = "CONNECTIONS",
            Dock = DockStyle.Fill,
            ForeColor = Theme.Text,
            Font = Theme.UiFontBold,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        var btnNewFolder = IconButton("🗀", "New Folder", (_, _) => NewFolder(SelectedFolder()));
        var btnNewConn = IconButton("＋", "New Connection", (_, _) => NewConnection(SelectedFolder()));
        btnNewConn.Dock = DockStyle.Right;
        btnNewFolder.Dock = DockStyle.Right;
        header.Controls.Add(title);
        header.Controls.Add(btnNewFolder);
        header.Controls.Add(btnNewConn);

        var searchHost = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Theme.SidebarBack, Padding = new Padding(8, 2, 8, 4) };
        _search = new TextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.WorkspaceBack,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Theme.UiFont,
            PlaceholderText = "Search hosts, folders"
        };
        _search.TextChanged += (_, _) => Rebuild();
        searchHost.Controls.Add(_search);

        var quick = new Button
        {
            Text = "▷  Quick Connect",
            Dock = DockStyle.Top,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Theme.Accent,
            Font = Theme.UiFontBold,
            Cursor = Cursors.Hand,
            Margin = new Padding(8)
        };
        quick.FlatAppearance.BorderSize = 0;
        quick.FlatAppearance.MouseOverBackColor = Theme.AccentDim;
        quick.Click += (_, _) => QuickConnect();
        var quickHost = new Panel { Dock = DockStyle.Top, Height = 42, BackColor = Theme.SidebarBack, Padding = new Padding(8, 4, 8, 4) };
        quickHost.Controls.Add(quick);

        var shellBtn = new Button
        {
            Text = "🖥  Local Shell", Dock = DockStyle.Top, Height = 30,
            FlatStyle = FlatStyle.Flat, ForeColor = Theme.Text, BackColor = Theme.TabBack,
            Font = Theme.UiFont, Cursor = Cursors.Hand
        };
        shellBtn.FlatAppearance.BorderColor = Theme.Border;
        shellBtn.FlatAppearance.MouseOverBackColor = Theme.Hover;
        shellBtn.Click += (_, _) => OpenLocalShell();
        var shellHost = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Theme.SidebarBack, Padding = new Padding(8, 2, 8, 4) };
        shellHost.Controls.Add(shellBtn);

        var toolbar = new Panel { Dock = DockStyle.Top, Height = 28, BackColor = Theme.SidebarBack };
        var btnExpand = IconButton("⊞", "Expand All", (_, _) => SetAllExpanded(true));
        var btnCollapse = IconButton("⊟", "Collapse All", (_, _) => SetAllExpanded(false));
        btnExpand.Dock = DockStyle.Right;
        btnCollapse.Dock = DockStyle.Right;
        toolbar.Controls.Add(btnExpand);
        toolbar.Controls.Add(btnCollapse);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.SidebarBack,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            Font = Theme.UiFont,
            HideSelection = false,
            ShowLines = false,
            ShowRootLines = true,
            ShowPlusMinus = true,
            FullRowSelect = true,
            ItemHeight = 26,
            Indent = 18,
            AllowDrop = true
        };
        _tree.NodeMouseDoubleClick += OnNodeDoubleClick;
        _tree.AfterExpand += (_, e) => { if (e.Node?.Tag is Node n) n.Expanded = true; };
        _tree.AfterCollapse += (_, e) => { if (e.Node?.Tag is Node n) n.Expanded = false; };
        _tree.MouseDown += OnTreeMouseDown;
        _tree.ItemDrag += OnItemDrag;
        _tree.DragEnter += (_, e) => e.Effect = DragDropEffects.Move;
        _tree.DragOver += OnDragOver;
        _tree.DragDrop += OnDragDrop;

        sessionsPanel.Controls.Add(_tree);
        sessionsPanel.Controls.Add(toolbar);
        sessionsPanel.Controls.Add(shellHost);
        sessionsPanel.Controls.Add(quickHost);
        sessionsPanel.Controls.Add(searchHost);
        sessionsPanel.Controls.Add(header);

        // ── SFTP panel content ──
        BuildSftpPanel(sftpPanel);

        // ── Main layout ──
        Controls.Add(sessionsPanel);
        Controls.Add(sftpPanel);
        Controls.Add(tabBar);

        LoadFromStore();
        Rebuild();
    }

    // ── SFTP panel builder ──────────────────────────────────────
    private Renci.SshNet.SftpClient? _sftp;
    private ListView? _sftpList;
    private TextBox? _sftpPath;

    private void BuildSftpPanel(Panel panel)
    {
        // Connection row
        var connRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, Height = 30, BackColor = Theme.SidebarBack,
            Padding = new Padding(4, 4, 4, 0), WrapContents = false
        };
        var sshLabel = new Label { Text = "SSH:", AutoSize = true, ForeColor = Theme.TextDim, Font = Theme.UiFont, Margin = new Padding(0, 4, 4, 0) };
        var sshCombo = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Theme.TabBack, ForeColor = Theme.Text, Font = Theme.UiFont };
        var connectBtn = new Button
        {
            Text = "Connect", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(64, 24), Padding = new Padding(8, 2, 8, 2),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.Text, BackColor = Theme.TabBack, Font = Theme.UiFont, Cursor = Cursors.Hand, Margin = new Padding(4, 0, 0, 0)
        };
        connectBtn.FlatAppearance.BorderColor = Theme.SerialColor;
        connRow.Controls.Add(sshLabel);
        connRow.Controls.Add(sshCombo);
        connRow.Controls.Add(connectBtn);

        // Path bar
        _sftpPath = new TextBox
        {
            Dock = DockStyle.Top, Height = 24, Text = "/",
            BackColor = Theme.TabBack, ForeColor = Theme.Accent, Font = Theme.UiFont, BorderStyle = BorderStyle.FixedSingle
        };
        _sftpPath.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter && _sftp?.IsConnected == true) SftpNavigate(_sftpPath.Text); };

        // File list
        _sftpList = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
            BackColor = Theme.SidebarBack, ForeColor = Theme.Text, Font = Theme.UiFont,
            BorderStyle = BorderStyle.None, HeaderStyle = ColumnHeaderStyle.Nonclickable
        };
        _sftpList.Columns.Add("Name", 150);
        _sftpList.Columns.Add("Size", 60, HorizontalAlignment.Right);
        _sftpList.DoubleClick += (_, _) =>
        {
            if (_sftpList.SelectedItems.Count == 0 || _sftp?.IsConnected != true) return;
            var item = _sftpList.SelectedItems[0];
            if (item.Tag is string dir) SftpNavigate(dir);
        };

        panel.Controls.Add(_sftpList);
        panel.Controls.Add(_sftpPath);
        panel.Controls.Add(connRow);

        // Populate SSH connections on tab show
        panel.VisibleChanged += (_, _) =>
        {
            if (!panel.Visible) return;
            sshCombo.Items.Clear();
            foreach (var conn in _store.GetAll().Where(c => c.IsSsh))
                sshCombo.Items.Add(conn);
            sshCombo.DisplayMember = "Name";
        };

        connectBtn.Click += (_, _) =>
        {
            if (_sftp?.IsConnected == true) { _sftp.Disconnect(); _sftp.Dispose(); _sftp = null; connectBtn.Text = "Connect"; _sftpList!.Items.Clear(); return; }
            if (sshCombo.SelectedItem is not Connections.Connection conn || conn.Ssh == null) return;
            try
            {
                var secret = Connections.CredentialVault.Get(conn.CredentialKey);
                var methods = new List<Renci.SshNet.AuthenticationMethod>();
                if (!string.IsNullOrEmpty(secret))
                    methods.Add(new Renci.SshNet.PasswordAuthenticationMethod(conn.Ssh.Username, secret));
                var ci = new Renci.SshNet.ConnectionInfo(conn.Ssh.Host, conn.Ssh.Port, conn.Ssh.Username, methods.ToArray());
                _sftp = new Renci.SshNet.SftpClient(ci);
                _sftp.Connect();
                connectBtn.Text = "Disconnect";
                SftpNavigate(_sftp.WorkingDirectory ?? "/");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"SFTP connect failed:\n{ex.Message}", "SFTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
    }

    private void SftpNavigate(string path)
    {
        if (_sftp == null || _sftpList == null || _sftpPath == null) return;
        try
        {
            var items = _sftp.ListDirectory(path).OrderByDescending(f => f.IsDirectory).ThenBy(f => f.Name).ToList();
            _sftpPath.Text = path;
            _sftpList.Items.Clear();
            if (path != "/")
            {
                var parent = path.TrimEnd('/');
                int lastSlash = parent.LastIndexOf('/');
                string parentPath = lastSlash <= 0 ? "/" : parent[..lastSlash];
                var up = new ListViewItem(new[] { "..", "" }) { Tag = parentPath, ForeColor = Theme.Accent };
                _sftpList.Items.Add(up);
            }
            foreach (var f in items)
            {
                if (f.Name == "." || f.Name == "..") continue;
                string size = f.IsDirectory ? "" : FormatSize(f.Length);
                var lvi = new ListViewItem(new[] { (f.IsDirectory ? "📁 " : "📄 ") + f.Name, size });
                lvi.ForeColor = f.IsDirectory ? Theme.Accent : Theme.Text;
                if (f.IsDirectory) lvi.Tag = f.FullName;
                _sftpList.Items.Add(lvi);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "SFTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes}",
        < 1024 * 1024 => $"{bytes / 1024}K",
        _ => $"{bytes / (1024 * 1024)}M"
    };

    // ── 從 SQLite 載入並重建資料夾樹 ─────────────────────────
    private void LoadFromStore()
    {
        foreach (var conn in _store.GetAll())
        {
            var folder = EnsureFolderPath(conn.GroupName);
            AddNode(folder, new Node { Kind = Kind.Connection, Conn = conn });
            _sortSeq = Math.Max(_sortSeq, conn.SortOrder);
        }
    }

    private Node EnsureFolderPath(string? group)
    {
        var cur = _root;
        if (string.IsNullOrEmpty(group)) return cur;
        foreach (var part in group.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = cur.Children.FirstOrDefault(c => c.IsFolder && c.Name == part)
                       ?? AddNode(cur, new Node { Kind = Kind.Folder, Name = part });
            cur = next;
        }
        return cur;
    }

    private string? PathOf(Node folder)
    {
        var parts = new List<string>();
        for (var n = folder; n != null && n != _root; n = n.Parent) parts.Insert(0, n.Name);
        return parts.Count == 0 ? null : string.Join("/", parts);
    }

    private static Node AddNode(Node parent, Node child)
    {
        child.Parent = parent;
        parent.Children.Add(child);
        return child;
    }

    // ── 重建 TreeView（套用搜尋過濾、保留展開狀態）────────────
    private void Rebuild()
    {
        string q = _search.Text.Trim().ToLowerInvariant();
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var child in _root.Children)
        {
            var tn = BuildTreeNode(child, q);
            if (tn != null) _tree.Nodes.Add(tn);
        }
        _tree.EndUpdate();
    }

    private TreeNode? BuildTreeNode(Node node, string filter)
    {
        bool selfMatch = filter.Length == 0
            || node.DisplayName.ToLowerInvariant().Contains(filter)
            || node.Detail.ToLowerInvariant().Contains(filter);

        if (node.IsFolder)
        {
            var childNodes = new List<TreeNode>();
            foreach (var c in node.Children)
            {
                var ctn = BuildTreeNode(c, filter);
                if (ctn != null) childNodes.Add(ctn);
            }
            if (!selfMatch && childNodes.Count == 0) return null;

            var tn = new TreeNode($"📁  {node.Name}   ({CountConnections(node)})")
            {
                Tag = node,
                ForeColor = Theme.TextDim
            };
            tn.Nodes.AddRange(childNodes.ToArray());
            if (filter.Length > 0 || node.Expanded) tn.Expand();
            return tn;
        }

        if (!selfMatch) return null;
        string detail = node.Conn != null ? $" ({(node.IsSsh ? node.Conn.Ssh?.Host : node.Conn.Serial?.PortName)})" : "";
        return new TreeNode($"{(node.IsSsh ? "🖧" : "🔌")}  {node.DisplayName}{detail}")
        {
            Tag = node,
            ToolTipText = node.Detail,
            ForeColor = node.IsSsh ? Theme.SshColor : Theme.SerialColor
        };
    }

    private static int CountConnections(Node folder)
    {
        int n = 0;
        foreach (var c in folder.Children)
            n += c.IsFolder ? CountConnections(c) : 1;
        return n;
    }

    // ── 操作：新增 / 改名 / 刪除 ──────────────────────────────
    private Node SelectedFolder()
    {
        if (_tree.SelectedNode?.Tag is Node n)
            return n.IsFolder ? n : (n.Parent ?? _root);
        return _root;
    }

    private void NewFolder(Node parent)
    {
        string? name = TextPromptDialog.Ask(this, "New Folder", "Folder name", "New Folder");
        if (name == null) return;
        var node = AddNode(parent, new Node { Kind = Kind.Folder, Name = name });
        parent.Expanded = true;
        Rebuild();
        SelectModelNode(node);
    }

    private void NewConnection(Node parent)
    {
        using var d = new ConnectionEditDialog("New Connection");
        if (d.ShowDialog(this) != DialogResult.OK) return;
        var conn = d.Result;
        conn.GroupName = PathOf(parent);
        conn.SortOrder = ++_sortSeq;
        _store.Upsert(conn);
        if (d.Password.Length > 0) CredentialVault.Set(conn.CredentialKey, d.Password);
        var node = AddNode(parent, new Node { Kind = Kind.Connection, Conn = conn });
        parent.Expanded = true;
        Rebuild();
        SelectModelNode(node);
    }

    private void RenameNode(Node node)
    {
        if (node.IsFolder)
        {
            string? name = TextPromptDialog.Ask(this, "Rename Folder", "Folder name", node.Name);
            if (name == null) return;
            node.Name = name;
            PersistSubtreeGroups(node);     // 子連線的 GroupName 路徑跟著變
        }
        else
        {
            using var d = new ConnectionEditDialog("Edit Connection", node.Conn);
            if (d.ShowDialog(this) != DialogResult.OK) return;
            node.Conn = d.Result;           // 保留 Id / GroupName / SortOrder
            _store.Upsert(node.Conn);
            if (d.Password.Length > 0) CredentialVault.Set(node.Conn.CredentialKey, d.Password);
        }
        Rebuild();
        SelectModelNode(node);
    }

    private void DeleteNode(Node node)
    {
        string what = node.IsFolder ? $"folder \"{node.Name}\" and its contents" : $"connection \"{node.DisplayName}\"";
        if (MessageBox.Show($"Delete {what}?", "Delete", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning)
            != DialogResult.OK) return;
        DeleteFromStore(node);
        node.Parent?.Children.Remove(node);
        Rebuild();
    }

    private void DeleteFromStore(Node node)
    {
        if (node.IsFolder)
        {
            foreach (var c in node.Children) DeleteFromStore(c);
        }
        else if (node.Conn is { } c)
        {
            _store.Delete(c.Id);
            CredentialVault.Delete(c.CredentialKey);
        }
    }

    /// <summary>把資料夾子樹下所有連線的 GroupName 重設為現在的路徑並存檔。</summary>
    private void PersistSubtreeGroups(Node folder)
    {
        foreach (var c in folder.Children)
        {
            if (c.IsFolder) PersistSubtreeGroups(c);
            else if (c.Conn is { } conn) { conn.GroupName = PathOf(folder); _store.Upsert(conn); }
        }
    }

    private void QuickConnect()
    {
        using var d = new ConnectionEditDialog("Quick Connect");
        if (d.ShowDialog(this) != DialogResult.OK) return;
        ConnectionActivated?.Invoke(this, d.Result);   // ad-hoc：不存入樹 / SQLite
    }

    private void OpenLocalShell()
    {
        var s = Infrastructure.AppSettings.Instance;
        var conn = new Connection
        {
            Name = s.ShellType,
            Type = ConnectionType.Shell,
            Shell = new ShellSettings { ShellType = s.ShellType, StartupDirectory = s.ShellStartupDir }
        };
        ConnectionActivated?.Invoke(this, conn);
    }

    private void SetAllExpanded(bool expanded)
    {
        void Walk(Node n) { if (n.IsFolder) { n.Expanded = expanded; foreach (var c in n.Children) Walk(c); } }
        foreach (var c in _root.Children) Walk(c);
        Rebuild();
    }

    // ── 事件 ─────────────────────────────────────────────────
    private void Activate(Node n)
    {
        if (n.Conn is { } c) ConnectionActivated?.Invoke(this, c);
    }

    private void OnNodeDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node?.Tag is Node { IsFolder: false } n) Activate(n);
    }

    private void OnTreeMouseDown(object? sender, MouseEventArgs e)
    {
        var node = _tree.GetNodeAt(e.Location);
        if (node != null) _tree.SelectedNode = node;
        if (e.Button == MouseButtons.Right)
            ShowContextMenu(node?.Tag as Node, e.Location);
    }

    private void ShowContextMenu(Node? node, Point at)
    {
        var menu = new ContextMenuStrip { BackColor = Theme.SidebarBack, ForeColor = Theme.Text };
        if (node is { IsFolder: false } conn)
        {
            menu.Items.Add("Open", null, (_, _) => Activate(conn));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Rename / Edit", null, (_, _) => RenameNode(conn));
            menu.Items.Add("Delete", null, (_, _) => DeleteNode(conn));
        }
        else
        {
            var target = node ?? _root;
            menu.Items.Add("New Folder", null, (_, _) => NewFolder(target));
            menu.Items.Add("New Connection", null, (_, _) => NewConnection(target));
            if (node != null)
            {
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("Rename", null, (_, _) => RenameNode(node));
                menu.Items.Add("Delete", null, (_, _) => DeleteNode(node));
            }
        }
        menu.Show(_tree, at);
    }

    // ── 拖曳分類（更新 GroupName 並持久化）────────────────────
    private void OnItemDrag(object? sender, ItemDragEventArgs e)
    {
        _dragNode = e.Item as TreeNode;
        if (_dragNode != null) _tree.DoDragDrop(_dragNode, DragDropEffects.Move);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var pt = _tree.PointToClient(new Point(e.X, e.Y));
        _tree.SelectedNode = _tree.GetNodeAt(pt);
        e.Effect = DragDropEffects.Move;
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (_dragNode?.Tag is not Node src) return;
        var pt = _tree.PointToClient(new Point(e.X, e.Y));
        var targetNode = _tree.GetNodeAt(pt)?.Tag as Node;

        Node dest = targetNode == null ? _root
                  : targetNode.IsFolder ? targetNode
                  : (targetNode.Parent ?? _root);

        if (src == dest || IsDescendant(src, dest)) return;

        src.Parent?.Children.Remove(src);
        src.Parent = dest;
        dest.Children.Add(src);
        dest.Expanded = true;

        if (src.IsFolder) PersistSubtreeGroups(src);
        else if (src.Conn is { } c) { c.GroupName = PathOf(dest); _store.Upsert(c); }

        Rebuild();
        SelectModelNode(src);
        _dragNode = null;
    }

    private static bool IsDescendant(Node ancestor, Node maybe)
    {
        for (var p = maybe.Parent; p != null; p = p.Parent)
            if (p == ancestor) return true;
        return false;
    }

    // ── 小工具 ───────────────────────────────────────────────
    private void SelectModelNode(Node target)
    {
        TreeNode? Find(TreeNodeCollection nodes)
        {
            foreach (TreeNode tn in nodes)
            {
                if (ReferenceEquals(tn.Tag, target)) return tn;
                var r = Find(tn.Nodes);
                if (r != null) return r;
            }
            return null;
        }
        var found = Find(_tree.Nodes);
        if (found != null) _tree.SelectedNode = found;
    }

    private Button IconButton(string glyph, string tip, EventHandler onClick)
    {
        var b = new Button
        {
            Text = glyph,
            Width = 34,
            Height = 34,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Theme.TextDim,
            BackColor = Theme.SidebarBack,
            Font = new Font("Segoe UI Symbol", 11f),
            Cursor = Cursors.Hand
        };
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = Theme.Hover;
        b.Click += onClick;
        new ToolTip().SetToolTip(b, tip);
        return b;
    }
}
