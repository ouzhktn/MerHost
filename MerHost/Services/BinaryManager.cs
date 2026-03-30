using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text.RegularExpressions;

namespace MerHost.Services;

public class BinaryManager
{
    private readonly string _projectPath;
    private readonly string _appDataPath;
    private readonly string _binPath;
    private readonly string _wwwPath;
    private readonly AppSettings _settings;

    public event Action<string>? OnDownloadProgress;
    public event Action<int, string>? OnLog;

    public string BinPath => _binPath;
    public string WwwPath => _wwwPath;
    public string AppDataPath => _appDataPath;
    public string NginxPath => Path.Combine(_binPath, "nginx", "nginx.exe");
    public string PhpPath => Path.Combine(_binPath, "php-cgi.exe");
    public string PhpCgiPath => _binPath;
    public string MysqlPath => Path.Combine(_binPath, "mariadb-11.4.2-winx64", "bin", "mysqld.exe");
    public string PhpMyAdminPath => _settings.GetPhpMyAdminPath();
    public string NodePath => Path.Combine(_binPath, "node", "node.exe");
    public string NodeModulesPath => Path.Combine(_wwwPath, "node_modules");

    public async Task<bool> EnsureNodeAsync()
    {
        if (File.Exists(NodePath))
            return true;

        OnLog?.Invoke(0, "Node.js indiriliyor...");
        
        try
        {
            await DownloadAndExtractAsync("node-v20.11.1-win-x64.zip", _binPath, "node", () => 
            {
                OnLog?.Invoke(0, "Node.js kuruldu");
            });
            return File.Exists(NodePath);
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Node.js indirme hatası: {ex.Message}");
            return false;
        }
    }

    public BinaryManager()
    {
        _settings = new AppSettings();
        
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        _projectPath = baseDir;
        _appDataPath = _settings.InstallPath;
        _binPath = _settings.GetBinPath();
        _wwwPath = _settings.GetWwwPath();

        EnsureDirectories();
    }

    private void EnsureDirectories()
    {
        if (!Directory.Exists(_binPath))
            Directory.CreateDirectory(_binPath);
        
        if (!Directory.Exists(_wwwPath))
            Directory.CreateDirectory(_wwwPath);

        var sslPath = _settings.GetSslPath();
        if (!Directory.Exists(sslPath))
            Directory.CreateDirectory(sslPath);
    }

    private readonly Dictionary<string, string> _downloadUrls = new()
    {
        { "nginx-1.28.2.zip", "https://nginx.org/download/nginx-1.28.2.zip" },
        { "php-8.3.30-Win32-vs16-x64.zip", "https://windows.php.net/downloads/releases/php-8.3.30-Win32-vs16-x64.zip" },
        { "mariadb-11.4.2-winx64.zip", "https://dlm.mariadb.com/3829199/MariaDB/mariadb-11.4.2/winx64-packages/mariadb-11.4.2-winx64.zip" },
        { "phpMyAdmin-5.2.3-all-languages.zip", "https://files.phpmyadmin.net/phpMyAdmin/5.2.3/phpMyAdmin-5.2.3-all-languages.zip" },
        { "node-v20.11.1-win-x64.zip", "https://nodejs.org/dist/v20.11.1/node-v20.11.1-win-x64.zip" }
    };

    public async Task<bool> EnsureBinariesAsync()
    {
        OnLog?.Invoke(0, "Dosyalar kontrol ediliyor...");

        bool success = true;

        try
        {
            await DownloadAndExtractAsync("nginx-1.28.2.zip", _binPath, "nginx", () => OnLog?.Invoke(0, "Nginx kuruldu"));
            await DownloadAndExtractAsync("php-8.3.30-Win32-vs16-x64.zip", _binPath, "php", () => 
            {
                var phpIniPath = Path.Combine(_binPath, "php.ini");
                if (File.Exists(phpIniPath))
                    File.Delete(phpIniPath);
                ConfigurePhp();
                OnLog?.Invoke(0, "PHP kuruldu");
            });
            await DownloadAndExtractAsync("phpMyAdmin-5.2.3-all-languages.zip", _appDataPath, "phpmyadmin", () => 
            {
                var phpmyadminDir = Path.Combine(_appDataPath, "phpmyadmin");
                ConfigurePhpMyAdmin(phpmyadminDir);
                OnLog?.Invoke(0, "phpMyAdmin kuruldu");
            });
            await DownloadAndExtractAsync("mariadb-11.4.2-winx64.zip", _binPath, "mariadb-11.4.2-winx64", () => 
            {
                ConfigureMysql();
                OnLog?.Invoke(0, "MariaDB kuruldu");
            });
            await DownloadAndExtractAsync("node-v20.11.1-win-x64.zip", _binPath, "node", () => 
            {
                OnLog?.Invoke(0, "Node.js kuruldu");
            });

            OnLog?.Invoke(0, "Tüm dosyalar hazır");
            return success;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Hata: {ex.Message}");
            return success;
        }
    }

    private async Task DownloadAndExtractAsync(string zipName, string destPath, string folderName, Action onComplete, bool isFlatExtract = false)
    {
        var localZip = Path.Combine(_projectPath, zipName);
        
        if (!File.Exists(localZip))
        {
            if (_downloadUrls.TryGetValue(zipName, out var url))
            {
                OnLog?.Invoke(0, $"{zipName} indiriliyor...");
                OnDownloadProgress?.Invoke($"{zipName} indiriliyor...");
                
                try
                {
                    using var client = new WebClient();
                    await client.DownloadFileTaskAsync(url, localZip);
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke(1, $"{zipName} indirilemedi: {ex.Message}");
                    return;
                }
            }
            else
            {
                OnLog?.Invoke(0, $"{zipName} bulunamadı, atlanıyor");
                return;
            }
        }

        await ExtractZipAsync(localZip, destPath, folderName, true, isFlatExtract);
        onComplete?.Invoke();
    }

    private async Task ExtractZipAsync(string zipPath, string destPath, string folderName, bool force = false, bool isFlatExtract = false)
    {
        var destFolder = Path.Combine(destPath, folderName);
        var alreadyExtracted = false;
        
        try
        {
            var requiredExe = folderName == "nginx" ? "nginx.exe" : 
                              folderName == "php" ? "php-cgi.exe" : "mysqld.exe";
            
            if (File.Exists(Path.Combine(destFolder, folderName == "mysql" ? "bin" : "", requiredExe)))
            {
                OnLog?.Invoke(0, $"{folderName} zaten mevcut");
                return;
            }
        }
        catch { }

        OnDownloadProgress?.Invoke($"{folderName} açılıyor...");
        
        await Task.Run(() =>
        {
            try
            {
                if (Directory.Exists(destFolder))
                {
                    ForceDeleteDirectory(destFolder);
                }
                ZipFile.ExtractToDirectory(zipPath, destPath);
            }
            catch (IOException)
            {
                OnLog?.Invoke(0, $"{folderName} dosyaları kilitli, yeniden deneniyor...");
                Thread.Sleep(1000);
                try
                {
                    if (Directory.Exists(destFolder))
                    {
                        ForceDeleteDirectory(destFolder);
                    }
                    ZipFile.ExtractToDirectory(zipPath, destPath);
                }
                catch (IOException ex2)
                {
                    OnLog?.Invoke(1, $"{folderName} çıkarılamadı: {ex2.Message}");
                    throw;
                }
            }
        });

        var extractedDirs = Directory.GetDirectories(destPath);
        if (!isFlatExtract)
        {
            foreach (var dir in extractedDirs)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName.StartsWith(folderName, StringComparison.OrdinalIgnoreCase) && !dirName.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(destFolder))
                        ForceDeleteDirectory(destFolder);
                    Directory.Move(dir, destFolder);
                    break;
                }
            }
        }
    }

    private void ConfigurePhp()
    {
        var phpDir = _binPath;
        var extDir = Path.Combine(phpDir, "ext").Replace("\\", "/");
        var phpIni = Path.Combine(phpDir, "php.ini-production");
        var phpIniDest = Path.Combine(phpDir, "php.ini");

        var content = "";
        if (File.Exists(phpIni))
        {
            content = File.ReadAllText(phpIni);
        }
        else if (File.Exists(phpIniDest))
        {
            content = File.ReadAllText(phpIniDest);
        }
        
        if (!string.IsNullOrEmpty(content))
        {
            content = Regex.Replace(content, @";?\s*extension_dir\s*=\s*""?.*""?", $"extension_dir = \"{extDir}\"", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=gd", "extension=gd", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=mbstring", "extension=mbstring", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=pdo_mysql", "extension=pdo_mysql", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=mysqli", "extension=mysqli", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=curl", "extension=curl", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=openssl", "extension=openssl", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=pdo_sqlite", "extension=pdo_sqlite", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*extension=sqlite3", "extension=sqlite3", RegexOptions.IgnoreCase);
            
            content = Regex.Replace(content, @";\s*fastcgi\.impersonate\s*=\s*1", "fastcgi.impersonate = 1", RegexOptions.IgnoreCase);
            content = Regex.Replace(content, @";\s*fastcgi\.logging\s*=\s*0", "fastcgi.logging = 0", RegexOptions.IgnoreCase);
            
            File.WriteAllText(phpIniDest, content);
        }
    }

    private void ConfigureMysql()
    {
        var mysqlDir = Path.Combine(_binPath, "mariadb-11.4.2-winx64");
        var dataDir = Path.Combine(_appDataPath, "mysql-data");
        
        if (!Directory.Exists(dataDir))
            Directory.CreateDirectory(dataDir);

        var myIni = Path.Combine(mysqlDir, "my.ini");
        var myIniContent = $@"
[mysqld]
port=3306
datadir={dataDir.Replace("\\", "/")}
basedir={mysqlDir.Replace("\\", "/")}
skip-grant-tables

[client]
user=root
password=root
";
        File.WriteAllText(myIni, myIniContent);
    }

    private void ConfigurePhpMyAdmin(string phpmyadminDir)
    {
        var configFile = Path.Combine(phpmyadminDir, "config.inc.php");
        if (File.Exists(configFile))
        {
            var config = File.ReadAllText(configFile);
            config = config.Replace("'auth_type' => 'cookie'", "'auth_type' => 'config'");
            config = config.Replace("'user' => ''", "'user' => 'root'");
            config = config.Replace("'password' => ''", "'password' => 'root'");
            if (!config.Contains("AllowNoPassword"))
            {
                config = config.Insert(config.LastIndexOf(");", StringComparison.Ordinal) - 1, "\n    'AllowNoPassword' => true,");
            }
            File.WriteAllText(configFile, config);
        }
    }

    public void CreateNginxConfig(int port, int mysqlPort)
    {
        var confDir = Path.Combine(_binPath, "nginx", "conf");
        var nginxConf = Path.Combine(confDir, "nginx.conf");

        var config = $@"
worker_processes 1;
error_log logs/error.log;
pid logs/nginx.pid;

events {{
    worker_connections 1024;
}}

http {{
    include mime.types;
    default_type application/octet-stream;
    sendfile on;
    keepalive_timeout 65;

    server {{
        listen {port};
        server_name localhost;
        root ""{_wwwPath.Replace("\\", "/")}"";
        index index.php index.html index.htm;

        location / {{
            try_files $uri $uri/ /index.php?$query_string;
        }}

        location /phpmyadmin {{
            alias ""{_appDataPath.Replace("\\", "/")}/phpmyadmin/"";
            index index.php;
        }}

        location ~ ^/phpmyadmin/(.+\.php)$ {{
            alias ""{_appDataPath.Replace("\\", "/")}/phpmyadmin/$1"";
            fastcgi_pass 127.0.0.1:9000;
            fastcgi_index index.php;
            fastcgi_param SCRIPT_FILENAME $request_filename;
            include fastcgi_params;
        }}

        location ~ \.php$ {{
            fastcgi_pass 127.0.0.1:9000;
            fastcgi_index index.php;
            fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;
            include fastcgi_params;
        }}
    }}
}}
";
        File.WriteAllText(nginxConf, config);
    }

    private void ForceDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
            return;
            
        for (int i = 0; i < 10; i++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (IOException)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(500);
            }
        }
    }
}
