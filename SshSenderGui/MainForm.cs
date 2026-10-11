using Renci.SshNet;
using Renci.SshNet.Common;

namespace SshSenderGui;

public class MainForm : Form
{
    private record RemoteItem(string Name, bool IsDir);

    // ---- Connection controls ----
    private readonly TextBox txtHost = new() { Text = "192.168.1.50", Width = 150 };
    private readonly TextBox txtUser = new() { Width = 110 };
    private readonly TextBox txtPass = new() { Width = 130, UseSystemPasswordChar = true };
    private readonly Button btnConnect = new() { Text = "Connect", Width = 100, Height = 28 };
    private readonly Label lblStatus = new() { AutoSize = true, Margin = new Padding(12, 7, 3, 0) };

    // ---- Remote folder controls ----
    private readonly TextBox txtRemoteDir = new() { Dock = DockStyle.Fill };
    private readonly Button btnUp = new() { Text = "Up", AutoSize = true };
    private readonly Button btnGo = new() { Text = "Go", AutoSize = true };
    private readonly Button btnNewFolder = new() { Text = "New folder", AutoSize = true };
    private readonly ListView lvRemote = new()
    {
        View = View.Details,
        FullRowSelect = true,
        Dock = DockStyle.Fill,
        HideSelection = false
    };

    // ---- Upload controls ----
    private readonly TextBox txtLocal = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Button btnBrowse = new() { Text = "Browse...", AutoSize = true };
    private readonly Button btnUpload = new() { Text = "Upload", AutoSize = true };
    private readonly ProgressBar progress = new() { Dock = DockStyle.Fill, Height = 22, Minimum = 0, Maximum = 100 };
    private readonly Label lblProgress = new() { AutoSize = true, Text = "Drag files onto this window or click Browse." };

    private readonly TextBox txtLog = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill
    };

    // ---- State ----
    private SftpClient? sftp;
    private string connectedHost = "";
    private string homeDir = "";
    private string remoteDir = "";
    private bool busy;
    private readonly List<string> files = new();

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SshSenderGui", "settings.txt");

    public MainForm()
    {
        Text = "SSH Sender";
        ClientSize = new Size(840, 660);
        MinimumSize = new Size(720, 540);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);

        BuildLayout();
        WireEvents();
        LoadSettings();
        UpdateUi();
    }

    // =====================================================================
    // Layout
    // =====================================================================

    private static Label Lbl(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(8, 7, 3, 0) };

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(10)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // server
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // remote folder
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // upload
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));      // progress
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // log
        Controls.Add(root);

        // --- Server group ---
        var flow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
        flow.Controls.AddRange(new Control[]
        {
            Lbl("Host"), txtHost, Lbl("User"), txtUser, Lbl("Password"), txtPass, btnConnect, lblStatus
        });
        var gbServer = new GroupBox { Text = "Server", Dock = DockStyle.Fill, AutoSize = true };
        gbServer.Controls.Add(flow);
        root.Controls.Add(gbServer, 0, 0);

        // --- Remote folder group ---
        var remoteLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        remoteLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        remoteLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var pathRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1
        };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(txtRemoteDir, 0, 0);
        pathRow.Controls.Add(btnUp, 1, 0);
        pathRow.Controls.Add(btnGo, 2, 0);
        pathRow.Controls.Add(btnNewFolder, 3, 0);

        lvRemote.Columns.Add("Name", 380);
        lvRemote.Columns.Add("Size", 100, HorizontalAlignment.Right);
        lvRemote.Columns.Add("Modified", 160);

        remoteLayout.Controls.Add(pathRow, 0, 0);
        remoteLayout.Controls.Add(lvRemote, 0, 1);

        var gbRemote = new GroupBox
        {
            Text = "Destination folder on server (double-click a folder to open it)",
            Dock = DockStyle.Fill,
            Padding = new Padding(8)
        };
        gbRemote.Controls.Add(remoteLayout);
        root.Controls.Add(gbRemote, 0, 1);

        // --- Upload group ---
        var uploadRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 1
        };
        uploadRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        uploadRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        uploadRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        uploadRow.Controls.Add(txtLocal, 0, 0);
        uploadRow.Controls.Add(btnBrowse, 1, 0);
        uploadRow.Controls.Add(btnUpload, 2, 0);

        var gbUpload = new GroupBox
        {
            Text = "File(s) to send",
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8)
        };
        gbUpload.Controls.Add(uploadRow);
        root.Controls.Add(gbUpload, 0, 2);

        // --- Progress ---
        var progLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 6, 0, 6)
        };
        progLayout.Controls.Add(progress, 0, 0);
        progLayout.Controls.Add(lblProgress, 0, 1);
        root.Controls.Add(progLayout, 0, 3);

        // --- Log ---
        root.Controls.Add(txtLog, 0, 4);
    }

    private void WireEvents()
    {
        btnConnect.Click += async (_, _) => await RunBusyAsync(async () =>
        {
            if (sftp?.IsConnected == true) Disconnect();
            else await ConnectAsync();
        });

        txtPass.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnConnect.PerformClick();
            }
        };

        btnGo.Click += async (_, _) => await RunBusyAsync(() => GoToAsync(txtRemoteDir.Text));
        txtRemoteDir.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                btnGo.PerformClick();
            }
        };

        btnUp.Click += async (_, _) => await RunBusyAsync(() => GoToAsync(ParentOf(remoteDir)));

        btnNewFolder.Click += async (_, _) =>
        {
            string? name = Prompt("New folder", "Folder name:");
            if (string.IsNullOrWhiteSpace(name)) return;
            await RunBusyAsync(() => CreateFolderAsync(name));
        };

        lvRemote.DoubleClick += async (_, _) =>
        {
            if (lvRemote.SelectedItems.Count == 0) return;
            if (lvRemote.SelectedItems[0].Tag is RemoteItem { IsDir: true } item)
                await RunBusyAsync(() => GoToAsync(Combine(remoteDir, item.Name)));
        };

        btnBrowse.Click += (_, _) =>
        {
            using var dlg = new OpenFileDialog { Multiselect = true, Title = "Choose file(s) to send" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            files.Clear();
            files.AddRange(dlg.FileNames);
            UpdateFileBox();
            UpdateUi();
        };

        btnUpload.Click += async (_, _) => await RunBusyAsync(UploadAsync);

        // Drag & drop files onto the window
        foreach (Control c in new Control[] { this, lvRemote, txtLocal })
        {
            c.AllowDrop = true;
            c.DragEnter += (_, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                    e.Effect = DragDropEffects.Copy;
            };
            c.DragDrop += (_, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is not string[] dropped) return;
                files.Clear();
                files.AddRange(dropped.Where(File.Exists));
                UpdateFileBox();
                UpdateUi();
            };
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        sftp?.Dispose();
        base.OnFormClosing(e);
    }

    // =====================================================================
    // Actions
    // =====================================================================

    private async Task ConnectAsync()
    {
        string host = txtHost.Text.Trim();
        string user = txtUser.Text.Trim();
        if (host.Length == 0 || user.Length == 0)
        {
            Fail("Enter a host and a username.");
            return;
        }

        lblStatus.Text = "Connecting...";
        lblStatus.ForeColor = SystemColors.GrayText;

        var client = new SftpClient(host, 22, user, txtPass.Text)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        };

        try
        {
            await Task.Run(() => client.Connect());
        }
        catch
        {
            client.Dispose();
            throw;
        }

        sftp = client;
        connectedHost = host;
        homeDir = client.WorkingDirectory.TrimEnd('/');
        remoteDir = homeDir + "/incoming";
        await Task.Run(() => EnsureRemoteDir(client, remoteDir));

        txtRemoteDir.Text = remoteDir;
        txtPass.Clear();
        SaveSettings(host, user);
        Log($"Connected to {user}@{host}");

        await LoadListAsync();
    }

    private void Disconnect()
    {
        sftp?.Dispose();
        sftp = null;
        lvRemote.Items.Clear();
        Log("Disconnected.");
    }

    private async Task GoToAsync(string path)
    {
        var client = sftp;
        if (client is null) return;

        string target = NormalizeRemote(path);
        bool ok = await Task.Run(() => client.Exists(target) && client.GetAttributes(target).IsDirectory);
        if (!ok)
        {
            txtRemoteDir.Text = remoteDir;
            MessageBox.Show(this, $"Folder not found:\n{target}", "SSH Sender",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        remoteDir = target;
        txtRemoteDir.Text = target;
        await LoadListAsync();
    }

    private async Task CreateFolderAsync(string name)
    {
        var client = sftp;
        if (client is null) return;

        string path = Combine(remoteDir, name.Trim('/'));
        await Task.Run(() => EnsureRemoteDir(client, path));
        Log($"Created folder {path}");
        await LoadListAsync();
    }

    private async Task LoadListAsync()
    {
        var client = sftp;
        if (client is null) return;

        string dir = remoteDir;
        var entries = await Task.Run(() => client.ListDirectory(dir)
            .Where(f => f.Name != "." && f.Name != "..")
            .OrderByDescending(f => f.IsDirectory)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => (f.Name, f.IsDirectory, f.Length, f.LastWriteTime))
            .ToList());

        lvRemote.BeginUpdate();
        lvRemote.Items.Clear();
        foreach (var (name, isDir, length, modified) in entries)
        {
            var item = new ListViewItem(name) { Tag = new RemoteItem(name, isDir) };
            item.SubItems.Add(isDir ? "<DIR>" : FormatSize(length));
            item.SubItems.Add(modified.ToString("yyyy-MM-dd HH:mm"));
            if (isDir) item.Font = new Font(lvRemote.Font, FontStyle.Bold);
            lvRemote.Items.Add(item);
        }
        lvRemote.EndUpdate();
    }

    private async Task UploadAsync()
    {
        var client = sftp;
        if (client is null || files.Count == 0) return;

        var queue = files.ToList();
        long totalBytes = queue.Sum(f => new FileInfo(f).Length);
        long doneBefore = 0;
        int sent = 0;

        progress.Value = 0;

        foreach (string path in queue)
        {
            string name = Path.GetFileName(path);
            string remotePath = Combine(remoteDir, name);
            long fileLen = new FileInfo(path).Length;

            if (await Task.Run(() => client.Exists(remotePath)))
            {
                var answer = MessageBox.Show(this,
                    $"\"{name}\" already exists in this folder.\n\nOverwrite it?",
                    "File exists", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) break;
                if (answer == DialogResult.No)
                {
                    doneBefore += fileLen;
                    Log($"Skipped {name}");
                    continue;
                }
            }

            long baseline = doneBefore;
            int lastPct = -1;

            await Task.Run(() =>
            {
                using var fs = File.OpenRead(path);
                client.UploadFile(fs, remotePath, true, uploaded =>
                {
                    long done = baseline + (long)uploaded;
                    int pct = totalBytes > 0 ? (int)(100.0 * done / totalBytes) : 100;
                    if (pct == lastPct) return; // don't flood the UI thread
                    lastPct = pct;
                    BeginInvoke(() => ReportProgress(done, totalBytes, name));
                });
            });

            doneBefore += fileLen;
            sent++;
            Log($"Uploaded {name} ({FormatSize(fileLen)})");
        }

        ReportProgress(totalBytes, totalBytes, "Finished");
        lblProgress.Text = $"Done. {sent} of {queue.Count} file(s) uploaded.";

        files.Clear();
        UpdateFileBox();
        await LoadListAsync();
    }

    // =====================================================================
    // UI helpers
    // =====================================================================

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (busy) return;
        busy = true;
        UpdateUi();
        try
        {
            await action();
        }
        catch (SshAuthenticationException)
        {
            Fail("Wrong username or password.");
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
        finally
        {
            busy = false;
            UpdateUi();
        }
    }

    private void UpdateUi()
    {
        bool connected = sftp?.IsConnected == true;

        txtHost.Enabled = txtUser.Enabled = txtPass.Enabled = !connected && !busy;
        btnConnect.Enabled = !busy;
        btnConnect.Text = connected ? "Disconnect" : "Connect";

        txtRemoteDir.Enabled = btnGo.Enabled = btnUp.Enabled =
            btnNewFolder.Enabled = lvRemote.Enabled = connected && !busy;

        btnBrowse.Enabled = !busy;
        btnUpload.Enabled = connected && !busy && files.Count > 0;

        if (connected)
        {
            lblStatus.Text = $"Connected to {connectedHost}";
            lblStatus.ForeColor = Color.SeaGreen;
        }
        else if (!busy)
        {
            lblStatus.Text = "Not connected";
            lblStatus.ForeColor = SystemColors.GrayText;
        }
    }

    private void UpdateFileBox()
    {
        txtLocal.Text = files.Count switch
        {
            0 => "",
            1 => files[0],
            _ => $"{files.Count} files selected"
        };
        lblProgress.Text = files.Count == 0
            ? "Drag files onto this window or click Browse."
            : $"{files.Count} file(s) ready, {FormatSize(files.Sum(f => new FileInfo(f).Length))} total.";
        progress.Value = 0;
    }

    private void ReportProgress(long done, long total, string name)
    {
        int pct = total > 0 ? (int)Math.Min(100, 100.0 * done / total) : 100;
        progress.Value = pct;
        lblProgress.Text = $"{name}: {FormatSize(done)} / {FormatSize(total)}  ({pct}%)";
    }

    private void Log(string message) =>
        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");

    private void Fail(string message)
    {
        Log("Error: " + message);
        MessageBox.Show(this, message, "SSH Sender", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private string? Prompt(string title, string label)
    {
        using var f = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(360, 110),
            MaximizeBox = false,
            MinimizeBox = false
        };
        var lbl = new Label { Text = label, Left = 12, Top = 12, AutoSize = true };
        var tb = new TextBox { Left = 12, Top = 36, Width = 336 };
        var ok = new Button { Text = "OK", Left = 192, Top = 70, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 273, Top = 70, Width = 75, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
        f.AcceptButton = ok;
        f.CancelButton = cancel;
        return f.ShowDialog(this) == DialogResult.OK ? tb.Text.Trim() : null;
    }

    // =====================================================================
    // Remote path helpers
    // =====================================================================

    private static string Combine(string dir, string name) =>
        dir == "/" ? "/" + name : dir.TrimEnd('/') + "/" + name;

    private static string ParentOf(string dir)
    {
        string d = dir.TrimEnd('/');
        int i = d.LastIndexOf('/');
        return i <= 0 ? "/" : d[..i];
    }

    private string NormalizeRemote(string input)
    {
        string p = input.Trim();
        if (p.Length == 0) return remoteDir;
        if (p == "~") p = homeDir;
        else if (p.StartsWith("~/")) p = homeDir + p[1..];
        else if (!p.StartsWith('/')) p = Combine(remoteDir, p);

        p = p.TrimEnd('/');
        return p.Length == 0 ? "/" : p;
    }

    private static void EnsureRemoteDir(SftpClient client, string dir)
    {
        string current = "";
        foreach (string part in dir.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current += "/" + part;
            if (!client.Exists(current))
                client.CreateDirectory(current);
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = bytes;
        int i = 0;
        while (size >= 1024 && i < units.Length - 1)
        {
            size /= 1024;
            i++;
        }
        return i == 0 ? $"{bytes} B" : $"{size:0.#} {units[i]}";
    }

    // =====================================================================
    // Remember host + username (never the password)
    // =====================================================================

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var lines = File.ReadAllLines(SettingsPath);
            if (lines.Length > 0) txtHost.Text = lines[0];
            if (lines.Length > 1) txtUser.Text = lines[1];
        }
        catch { /* ignore */ }
    }

    private static void SaveSettings(string host, string user)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllLines(SettingsPath, new[] { host, user });
        }
        catch { /* ignore */ }
    }
}