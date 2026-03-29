using System.IO;

namespace MerHost.Services;

public class AppSettings
{
    private readonly string _settingsPath;
    private string _installPath = "";
    private string _wwwPath = "";

    public string InstallPath => string.IsNullOrEmpty(_installPath) 
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MerHost")
        : _installPath;
    
    public string WwwPath => string.IsNullOrEmpty(_wwwPath)
        ? Path.Combine(InstallPath, "www")
        : _wwwPath;

    public AppSettings()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _settingsPath = Path.Combine(baseDir, "settings.ini");
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var lines = File.ReadAllLines(_settingsPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=');
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        
                        switch (key)
                        {
                            case "InstallPath":
                                if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                                    _installPath = value;
                                break;
                            case "WwwPath":
                                if (!string.IsNullOrEmpty(value) && Directory.Exists(value))
                                    _wwwPath = value;
                                break;
                        }
                    }
                }
            }
        }
        catch { }
    }

    public string GetBinPath() => Path.Combine(InstallPath, "bin");
    public string GetWwwPath() => Path.Combine(InstallPath, "www");
    public string GetDataPath() => Path.Combine(InstallPath, "mysql-data");
    public string GetSslPath() => Path.Combine(InstallPath, "ssl");
    public string GetPhpMyAdminPath() => Path.Combine(InstallPath, "phpmyadmin");
}
