using System;
using Renci.SshNet;

string host = "192.168.1.8";   
string user = "smustac";

Console.Write("Password: ");
string password = ReadPassword();

var connInfo = new ConnectionInfo(host, 22, user,
    new PasswordAuthenticationMethod(user, password));

try
{
    // --- Send a file via SFTP ---
    using (var sftp = new SftpClient(connInfo))
    {
        sftp.Connect();

        using var fs = File.OpenRead(@"C:\temp\data.csv");
        sftp.UploadFile(fs, "/home/youruser/incoming/data.csv", true);

        // Or send text directly
        sftp.WriteAllText("/home/youruser/incoming/note.txt", "Hello from my PC!");

        sftp.Disconnect();
    }

    // --- Run a command on the server ---
    using (var ssh = new SshClient(connInfo))
    {
        ssh.Connect();
        var cmd = ssh.RunCommand("ls -la ~/incoming");
        Console.WriteLine(cmd.Result);
        ssh.Disconnect();
    }

    Console.WriteLine("Done.");
}
catch (Renci.SshNet.Common.SshAuthenticationException)
{
    Console.WriteLine("Wrong password.");
}
catch (Exception ex)
{
    Console.WriteLine("Error: " + ex.Message);
}

// Reads input without echoing it to the screen
static string ReadPassword()
{
    var sb = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (sb.Length > 0) sb.Length--;
            continue;
        }
        sb.Append(key.KeyChar);
    }
    return sb.ToString();
}