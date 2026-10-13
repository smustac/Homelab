using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Renci.SshNet.Common;
using SshSenderApp.Services;

namespace SshSenderApp.ViewModels;

/// <summary>A file chosen by the user. Opened as a stream so it also works with Android content:// files.</summary>
public sealed record PickedFile(string Name, long Size, Func<Task<Stream>> OpenAsync);

public class MainViewModel : ObservableBase
{
    private readonly SshService _ssh = new();
    private List<PickedFile> _files = new();

    // ---- backing fields ----
    private string _host = "192.168.1.50";
    private string _port = "22";
    private string _username = "";
    private string _password = "";
    private bool _isConnected;
    private bool _isBusy;
    private string _status = "";
    private string _currentDir = "/";
    private string _newFolderName = "";
    private bool _overwrite;
    private double _progress;
    private string _progressText = "";
    private RemoteEntry? _selectedEntry;

    public MainViewModel()
    {
        ConnectCommand = new AsyncCommand(ConnectAsync,
            () => !IsBusy && Host.Trim().Length > 0 && Username.Trim().Length > 0);
        DisconnectCommand = new AsyncCommand(() => { Disconnect(); return Task.CompletedTask; },
            () => IsConnected && !IsBusy);
        UpCommand = new AsyncCommand(() => GoToAsync(SshService.ParentOf(CurrentDir)),
            () => IsConnected && !IsBusy && CurrentDir != "/");
        RefreshCommand = new AsyncCommand(RefreshAsync,
            () => IsConnected && !IsBusy);
        CreateFolderCommand = new AsyncCommand(CreateFolderAsync,
            () => IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(NewFolderName));
        UploadCommand = new AsyncCommand(UploadAsync,
            () => IsConnected && !IsBusy && _files.Count > 0);

        LoadSettings();
    }

    // ---- commands ----
    public AsyncCommand ConnectCommand { get; }
    public AsyncCommand DisconnectCommand { get; }
    public AsyncCommand UpCommand { get; }
    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand CreateFolderCommand { get; }
    public AsyncCommand UploadCommand { get; }

    // ---- bindable state ----
    public string Host
    {
        get => _host;
        set { if (Set(ref _host, value)) UpdateCommands(); }
    }

    public string Port
    {
        get => _port;
        set => Set(ref _port, value);
    }

    public string Username
    {
        get => _username;
        set { if (Set(ref _username, value)) UpdateCommands(); }
    }

    public string Password
    {
        get => _password;
        set => Set(ref _password, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (!Set(ref _isConnected, value)) return;
            Raise(nameof(IsNotConnected));
            Raise(nameof(ConnectionTitle));
            UpdateCommands();
        }
    }

    public bool IsNotConnected => !IsConnected;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value)) return;
            Raise(nameof(IsNotBusy));
            UpdateCommands();
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string ConnectionTitle => $"{Username}@{Host}";

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string CurrentDir
    {
        get => _currentDir;
        private set
        {
            if (Set(ref _currentDir, value)) UpdateCommands();
        }
    }

    public ObservableCollection<RemoteEntry> Entries { get; } = new();

    /// <summary>Tapping a folder in the list opens it.</summary>
    public RemoteEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (!Set(ref _selectedEntry, value)) return;
            if (value is { IsDirectory: true })
            {
                _ = GoToAsync(SshService.Combine(CurrentDir, value.Name));
                Dispatcher.UIThread.Post(() => SelectedEntry = null);
            }
        }
    }

    public string NewFolderName
    {
        get => _newFolderName;
        set { if (Set(ref _newFolderName, value)) UpdateCommands(); }
    }

    public bool Overwrite
    {
        get => _overwrite;
        set => Set(ref _overwrite, value);
    }

    public double Progress
    {
        get => _progress;
        private set => Set(ref _progress, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => Set(ref _progressText, value);
    }

    public string SelectedFilesText => _files.Count switch
    {
        0 => "No files selected",
        1 => $"{_files[0].Name}  ({Fmt.Size(_files[0].Size)})",
        _ => $"{_files.Count} files selected  ({Fmt.Size(_files.Sum(f => f.Size))})"
    };

    // ---- called from the view ----
    public void SetFiles(IEnumerable<PickedFile> files)
    {
        _files = files.ToList();
        Raise(nameof(SelectedFilesText));
        Progress = 0;
        ProgressText = "";
        UpdateCommands();
    }

    // ---- actions ----
    private async Task ConnectAsync()
    {
        await RunBusy(async () =>
        {
            Status = "Connecting...";
            int port = int.TryParse(Port, out int p) ? p : 22;

            await _ssh.ConnectAsync(Host.Trim(), port, Username.Trim(), Password);
            Password = "";
            SaveSettings();
            IsConnected = true;

            CurrentDir = SshService.Combine(_ssh.HomeDir, "incoming");
            await _ssh.EnsureDirectoryAsync(CurrentDir);
            await LoadEntriesAsync();
            Status = $"Connected to {Host}";
        });
    }

    private void Disconnect()
    {
        _ssh.Disconnect();
        Entries.Clear();
        _files.Clear();
        Raise(nameof(SelectedFilesText));
        Progress = 0;
        ProgressText = "";
        IsConnected = false;
        Status = "Disconnected.";
    }

    private Task RefreshAsync() => RunBusy(LoadEntriesAsync);

    private Task GoToAsync(string path) => RunBusy(async () =>
    {
        if (!await _ssh.DirectoryExistsAsync(path))
        {
            SetError($"Folder not found: {path}");
            return;
        }
        CurrentDir = path;
        await LoadEntriesAsync();
        Status = "";
    });

    private Task CreateFolderAsync() => RunBusy(async () =>
    {
        string path = SshService.Combine(CurrentDir, NewFolderName.Trim().Trim('/'));
        await _ssh.EnsureDirectoryAsync(path);
        NewFolderName = "";
        await LoadEntriesAsync();
        Status = $"Created {path}";
    });

    private async Task LoadEntriesAsync()
    {
        var list = await _ssh.ListAsync(CurrentDir);
        Entries.Clear();
        foreach (var e in list) Entries.Add(e);
    }

    private Task UploadAsync() => RunBusy(async () =>
    {
        var queue = _files.ToList();
        if (queue.Count == 0) return;

        long total = queue.Sum(f => f.Size);
        long doneBefore = 0;
        int sent = 0, skipped = 0;

        Progress = 0;
        ProgressText = "";

        foreach (var file in queue)
        {
            string remotePath = SshService.Combine(CurrentDir, file.Name);

            if (!Overwrite && await _ssh.ExistsAsync(remotePath))
            {
                skipped++;
                doneBefore += file.Size;
                continue;
            }

            Status = $"Uploading {file.Name}...";
            long baseline = doneBefore;
            int lastPct = -1;

            await using var stream = await file.OpenAsync();
            await _ssh.UploadAsync(stream, remotePath, uploaded =>
            {
                long done = baseline + uploaded;
                int pct = total > 0 ? (int)Math.Min(100, 100.0 * done / total) : 0;
                if (pct == lastPct) return; // don't flood the UI thread
                lastPct = pct;
                Dispatcher.UIThread.Post(() =>
                {
                    Progress = pct;
                    ProgressText = $"{pct}%   ({Fmt.Size(done)} / {Fmt.Size(total)})";
                });
            });

            doneBefore += file.Size;
            sent++;
        }

        Progress = 100;
        ProgressText = "";
        Status = $"Done: {sent} uploaded" +
                 (skipped > 0 ? $", {skipped} skipped (already on server)" : "") + ".";

        _files = new List<PickedFile>();
        Raise(nameof(SelectedFilesText));
        await LoadEntriesAsync();
    });

    // ---- helpers ----
    private async Task RunBusy(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (SshAuthenticationException)
        {
            SetError("Wrong username or password.");
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
        }
        finally
        {
            IsBusy = false;
            if (IsConnected && !_ssh.IsConnected)
            {
                IsConnected = false;
                SetError("Connection lost. Please connect again.");
            }
        }
    }

    private void SetError(string message) => Status = "⚠ " + message;

    private void UpdateCommands()
    {
        ConnectCommand?.NotifyCanExecuteChanged();
        DisconnectCommand?.NotifyCanExecuteChanged();
        UpCommand?.NotifyCanExecuteChanged();
        RefreshCommand?.NotifyCanExecuteChanged();
        CreateFolderCommand?.NotifyCanExecuteChanged();
        UploadCommand?.NotifyCanExecuteChanged();
    }

    // ---- remember host / port / username (never the password) ----
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SshSenderApp", "settings.txt");

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var lines = File.ReadAllLines(SettingsPath);
            if (lines.Length > 0) Host = lines[0];
            if (lines.Length > 1) Port = lines[1];
            if (lines.Length > 2) Username = lines[2];
        }
        catch { /* ignore */ }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllLines(SettingsPath, new[] { Host.Trim(), Port.Trim(), Username.Trim() });
        }
        catch { /* ignore */ }
    }
}
