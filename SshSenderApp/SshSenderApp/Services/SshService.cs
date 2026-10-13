using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Renci.SshNet;

namespace SshSenderApp.Services;

public static class Fmt
{
    public static string Size(long bytes)
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
}

public sealed record RemoteEntry(string Name, bool IsDirectory, long Size, DateTime Modified)
{
    public string Icon => IsDirectory ? "📁" : "📄";

    public string Details => IsDirectory
        ? "Folder"
        : $"{Fmt.Size(Size)}  ·  {Modified:yyyy-MM-dd HH:mm}";
}

/// <summary>Thin async wrapper around SSH.NET's SftpClient. No UI code in here.</summary>
public sealed class SshService : IDisposable
{
    private SftpClient? _client;

    public bool IsConnected => _client?.IsConnected == true;
    public string HomeDir { get; private set; } = "/";

    private SftpClient Client =>
        _client ?? throw new InvalidOperationException("Not connected.");

    public async Task ConnectAsync(string host, int port, string user, string password)
    {
        Disconnect();

        var client = new SftpClient(host, port, user, password)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        };
        client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            await Task.Run(() => client.Connect());
        }
        catch
        {
            client.Dispose();
            throw;
        }

        _client = client;
        string wd = client.WorkingDirectory.TrimEnd('/');
        HomeDir = wd.Length == 0 ? "/" : wd;
    }

    public void Disconnect()
    {
        if (_client is null) return;
        try
        {
            if (_client.IsConnected) _client.Disconnect();
        }
        catch { /* ignore */ }
        _client.Dispose();
        _client = null;
    }

    public Task<bool> ExistsAsync(string path) =>
        Task.Run(() => Client.Exists(path));

    public Task<bool> DirectoryExistsAsync(string path) =>
        Task.Run(() => Client.Exists(path) && Client.GetAttributes(path).IsDirectory);

    /// <summary>Creates the folder and any missing parents (like mkdir -p).</summary>
    public Task EnsureDirectoryAsync(string path) => Task.Run(() =>
    {
        string current = "";
        foreach (string part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current += "/" + part;
            if (!Client.Exists(current))
                Client.CreateDirectory(current);
        }
    });

    public Task<List<RemoteEntry>> ListAsync(string path) => Task.Run(() =>
        Client.ListDirectory(path)
            .Where(f => f.Name != "." && f.Name != "..")
            .OrderByDescending(f => f.IsDirectory)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new RemoteEntry(f.Name, f.IsDirectory, f.Length, f.LastWriteTime))
            .ToList());

    /// <summary>Uploads from any readable stream. progress receives total bytes sent so far (on a background thread).</summary>
    public Task UploadAsync(Stream source, string remotePath, Action<long> progress) => Task.Run(() =>
        Client.UploadFile(source, remotePath, true, done => progress((long)done)));

    /// <summary>Downloads into any writable stream. progress receives total bytes received so far (on a background thread).</summary>
    public Task DownloadAsync(string remotePath, Stream destination, Action<long> progress) => Task.Run(() =>
        Client.DownloadFile(remotePath, destination, done => progress((long)done)));

    public void Dispose() => Disconnect();

    // ---- remote path helpers ----

    public static string Combine(string dir, string name) =>
        dir == "/" ? "/" + name : dir.TrimEnd('/') + "/" + name;

    public static string ParentOf(string dir)
    {
        string d = dir.TrimEnd('/');
        int i = d.LastIndexOf('/');
        return i <= 0 ? "/" : d[..i];
    }
}
