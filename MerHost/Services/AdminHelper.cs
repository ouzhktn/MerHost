using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace MerHost.Services;

public static class AdminHelper
{
    public static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool UpdateHostsFileWithElevation(List<string> hostsEntries, Action<string> onLog)
    {
        var hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
        
        if (IsRunningAsAdmin())
        {
            return UpdateHostsFile(hostsEntries, hostsPath, onLog);
        }

        var tempFile = Path.Combine(Path.GetTempPath(), "merhost_hosts_update.bat");
        
        var lines = new List<string>();
        lines.Add("@echo off");
        lines.Add($"type \"{hostsPath}\" > \"{Path.GetTempPath()}\\hosts.backup\"");
        
        foreach (var entry in hostsEntries)
        {
            lines.Add($"echo {entry} >> \"{hostsPath}\"");
        }
        
        lines.Add("exit");
        
        File.WriteAllLines(tempFile, lines);
        
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{tempFile}\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            
            var process = Process.Start(startInfo);
            process?.WaitForExit(10000);
            
            File.Delete(tempFile);
            
            if (process?.ExitCode == 0)
            {
                onLog?.Invoke("Hosts dosyası yönetici olarak güncellendi");
                return true;
            }
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"Hosts güncelleme hatası: {ex.Message}");
        }
        
        return false;
    }

    private static bool UpdateHostsFile(List<string> entries, string hostsPath, Action<string> onLog)
    {
        try
        {
            var lines = new List<string>();
            
            if (File.Exists(hostsPath))
            {
                var existingLines = File.ReadAllLines(hostsPath);
                foreach (var line in existingLines)
                {
                    if (!line.Contains(".test") && !line.Contains("merhost.local") && !line.Contains("merhost"))
                    {
                        lines.Add(line);
                    }
                }
            }
            
            foreach (var entry in entries)
            {
                lines.Add(entry);
            }
            
            File.WriteAllLines(hostsPath, lines);
            onLog?.Invoke("Hosts dosyası güncellendi");
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            onLog?.Invoke("Hosts dosyası için yönetici izni gerekli!");
            return false;
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"Hosts hatası: {ex.Message}");
            return false;
        }
    }

    public static bool InstallCertWithElevation(string certPath, Action<string> onLog)
    {
        if (IsRunningAsAdmin())
        {
            return true;
        }
        
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                return false;
            
            var helperPath = Path.Combine(Path.GetTempPath(), "MerHostCertHelper.exe");
            File.Copy(exePath, helperPath, true);
            
            var startInfo = new ProcessStartInfo
            {
                FileName = helperPath,
                Arguments = $"--install-cert \"{certPath}\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true
            };
            
            var process = Process.Start(startInfo);
            process?.WaitForExit(30000);
            
            try { File.Delete(helperPath); } catch { }
            
            return true;
        }
        catch (Exception ex)
        {
            onLog?.Invoke($"Sertifika yükleme hatası: {ex.Message}");
            return false;
        }
    }
}
