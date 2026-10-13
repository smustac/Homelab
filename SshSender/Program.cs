using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;

// ====== EDIT THESE ======
string host = " ? ";   // your laptop's IP
string user = " ? ";       // your username on the laptop
// ========================

Console.Write($"Password for {user}@{host}: ");
string password = ReadPassword();

using var sftp = new SftpClient(host, 22, user, password);

try
{
    Console.WriteLine("Connecting...");
    sftp.Connect();
}
catch (SshAuthenticationException)
{
    Console.WriteLine("Wrong password.");
    return;
}
catch (Exception ex)
{
    Console.WriteLine("Could not connect: " + ex.Message);
    return;
}

// Default destination: ~/incoming (WorkingDirectory is your home folder on the server)
string home = sftp.WorkingDirectory.TrimEnd('/');
string remoteDir = home + "/incoming";
EnsureRemoteDir(sftp, remoteDir);
Console.WriteLine("Connected.");

while (true)
{
    Console.WriteLine();
    Console.WriteLine($"=== SSH Sender ({user}@{host}) ===");
    Console.WriteLine($"Destination: {remoteDir}");
    Console.WriteLine("1) Upload a file");
    Console.WriteLine("2) Send typed text as a file");
    Console.WriteLine("3) Change destination folder");
    Console.WriteLine("4) List destination folder");
    Console.WriteLine("0) Quit");
    Console.Write("> ");

    string? choice = Console.ReadLine()?.Trim();

    try
    {
        switch (choice)
        {
            case "1":
                UploadFile(sftp, remoteDir);
                break;
            case "2":
                SendText(sftp, remoteDir);
                break;
            case "3":
                remoteDir = ChangeDirectory(sftp, home, remoteDir);
                break;
            case "4":
                ListDirectory(sftp, remoteDir);
                break;
            case "0":
                sftp.Disconnect();
                Console.WriteLine("Bye.");
                return;
            default:
                Console.WriteLine("Pick a number from the menu.");
                break;
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine("Error: " + ex.Message);
    }
}

// ---------------------------------------------------------------------------

static void UploadFile(SftpClient sftp, string remoteDir)
{
    Console.Write("Local file path (tip: drag the file into this window): ");
    string path = (Console.ReadLine() ?? "").Trim().Trim('"');

    if (!File.Exists(path))
    {
        Console.WriteLine("File not found.");
        return;
    }

    string remotePath = remoteDir.TrimEnd('/') + "/" + Path.GetFileName(path);

    if (sftp.Exists(remotePath))
    {
        Console.Write($"{Path.GetFileName(path)} already exists on the server. Overwrite? (y/n): ");
        if (Console.ReadLine()?.Trim().ToLower() != "y")
        {
            Console.WriteLine("Cancelled.");
            return;
        }
    }

    using var fs = File.OpenRead(path);
    long total = fs.Length;

    Console.WriteLine($"Uploading to {remotePath}");
    sftp.UploadFile(fs, remotePath, true, uploaded => DrawProgress((long)uploaded, total));
    DrawProgress(total, total);
    Console.WriteLine();
    Console.WriteLine("Upload complete.");
}

static void SendText(SftpClient sftp, string remoteDir)
{
    Console.Write("File name on the server (e.g. note.txt): ");
    string name = (Console.ReadLine() ?? "").Trim();
    if (name.Length == 0 || name.Contains('/') || name.Contains('\\'))
    {
        Console.WriteLine("Invalid file name.");
        return;
    }

    Console.Write("Text: ");
    string text = Console.ReadLine() ?? "";

    string remotePath = remoteDir.TrimEnd('/') + "/" + name;
    sftp.WriteAllText(remotePath, text + "\n");
    Console.WriteLine($"Saved {remotePath}");
}

static string ChangeDirectory(SftpClient sftp, string home, string current)
{
    Console.Write("New destination folder (absolute, or relative to your home folder): ");
    string input = (Console.ReadLine() ?? "").Trim();
    if (input.Length == 0) return current;

    string dir = input.StartsWith('/') ? input : home + "/" + input;
    dir = dir.TrimEnd('/');
    if (dir.Length == 0) dir = "/";

    if (!sftp.Exists(dir))
    {
        Console.Write("That folder doesn't exist. Create it? (y/n): ");
        if (Console.ReadLine()?.Trim().ToLower() != "y") return current;
        EnsureRemoteDir(sftp, dir);
    }

    return dir;
}

static void ListDirectory(SftpClient sftp, string remoteDir)
{
    Console.WriteLine($"Contents of {remoteDir}:");
    var entries = sftp.ListDirectory(remoteDir)
        .Where(f => f.Name != "." && f.Name != "..")
        .OrderByDescending(f => f.IsDirectory)
        .ThenBy(f => f.Name)
        .ToList();

    if (entries.Count == 0)
    {
        Console.WriteLine("  (empty)");
        return;
    }

    foreach (var f in entries)
    {
        string size = f.IsDirectory ? "<DIR>" : FormatSize(f.Length);
        Console.WriteLine($"  {f.Name,-40} {size,10}   {f.LastWriteTime:yyyy-MM-dd HH:mm}");
    }
}

static void EnsureRemoteDir(SftpClient sftp, string dir)
{
    string current = "";
    foreach (var part in dir.Split('/', StringSplitOptions.RemoveEmptyEntries))
    {
        current += "/" + part;
        if (!sftp.Exists(current))
            sftp.CreateDirectory(current);
    }
}

static void DrawProgress(long done, long total)
{
    const int width = 30;
    double fraction = total > 0 ? (double)done / total : 1.0;
    if (fraction > 1) fraction = 1;

    int filled = (int)(fraction * width);
    string bar = new string('#', filled) + new string('-', width - filled);
    Console.Write($"\r[{bar}] {fraction * 100,3:0}%  {FormatSize(done)} / {FormatSize(total)}   ");
}

static string FormatSize(long bytes)
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

// Reads input without echoing it to the screen
static string ReadPassword()
{
    var sb = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            break;
        }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0) sb.Length--;
            continue;
        }
        sb.Append(key.KeyChar);
    }
    return sb.ToString();
}