using System.Diagnostics;
using System.IO;

namespace MerHost.Services;

public class ServerManager
{
    private Process? _nginxProcess;
    private Process? _phpProcess;
    private Process? _mysqlProcess;
    private readonly BinaryManager _binaryManager;
    private readonly VirtualHostManager _virtualHostManager;
    private readonly AppSettings _appSettings;
    private int _port = 80;
    private int _mysqlPort = 3306;

    public event Action<string>? OnStatusChanged;
    public event Action<int, string>? OnLog;
    public event Action<string>? OnDownloadProgress;

    public bool IsRunning => _nginxProcess != null && !_nginxProcess.HasExited;
    public bool IsNginxRunning => _nginxProcess != null && !_nginxProcess.HasExited;
    public bool IsPhpRunning => _phpProcess != null && !_phpProcess.HasExited;
    public bool IsMysqlRunning => _mysqlProcess != null && !_mysqlProcess.HasExited;
    public int Port => _port;
    public int MysqlPort => _mysqlPort;
    public string WwwRoot => _binaryManager.WwwPath;
    public string BinPath => _binaryManager.BinPath;
    public List<string> ProjectDomains => _virtualHostManager.GetProjectDomains();
    public VirtualHostManager VirtualHostManager => _virtualHostManager;
    public BinaryManager BinaryManager => _binaryManager;
    public AppSettings AppSettings => _appSettings;

    public bool CreateDomain(string domainName, string folderName)
    {
        return _virtualHostManager.CreateDomain(domainName, folderName);
    }

    public ServerManager()
    {
        _appSettings = new AppSettings();
        _binaryManager = new BinaryManager();
        _binaryManager.OnLog += (level, msg) => OnLog?.Invoke(level, msg);
        _binaryManager.OnDownloadProgress += (msg) => OnDownloadProgress?.Invoke(msg);
        
        _virtualHostManager = new VirtualHostManager(_binaryManager);
        _virtualHostManager.OnLog += (level, msg) => OnLog?.Invoke(level, msg);
    }

    public async Task<bool> StartAsync()
    {
        try
        {
            OnStatusChanged?.Invoke("Dosyalar kontrol ediliyor...");
            
            var binariesReady = await _binaryManager.EnsureBinariesAsync();
            if (!binariesReady)
            {
                OnLog?.Invoke(1, "Dosya kurulumu başarısız!");
                OnStatusChanged?.Invoke("Hata: Dosya kurulumu başarısız");
                return false;
            }

            OnStatusChanged?.Invoke("Veritabanı başlatılıyor...");
            StartMysql();

            OnStatusChanged?.Invoke("PHP-FPM başlatılıyor...");
            StartPhp();

            OnStatusChanged?.Invoke("Nginx başlatılıyor...");
            await Task.Delay(500);
            StartNginx();

            OnStatusChanged?.Invoke($"Çalışıyor - http://localhost:{_port}");
            OnLog?.Invoke(0, $"Web: http://localhost:{_port}");
            OnLog?.Invoke(0, $"phpMyAdmin: http://localhost:{_port}/phpmyadmin");
            OnLog?.Invoke(0, $"MySQL: localhost:{_mysqlPort}");
            return true;
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Hata: {ex.Message}");
            OnStatusChanged?.Invoke($"Hata: {ex.Message}");
            return false;
        }
    }

    private void StartPhp()
    {
        var phpCgiPath = _binaryManager.PhpCgiPath;
        var phpCgiExe = Path.Combine(phpCgiPath, "php-cgi.exe");
        if (!File.Exists(phpCgiExe))
        {
            OnLog?.Invoke(1, "PHP bulunamadı!");
            return;
        }

        var phpIni = Path.Combine(phpCgiPath, "php.ini");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = phpCgiExe,
            Arguments = $"-b 127.0.0.1:9000 -c \"{phpIni}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _binaryManager.WwwPath
        };

        _phpProcess = Process.Start(startInfo);
        OnLog?.Invoke(0, $"PHP-FPM başlatıldı (PID: {_phpProcess?.Id})");
    }

    private void StartNginx()
    {
        var nginxPath = _binaryManager.NginxPath;
        if (!File.Exists(nginxPath))
        {
            OnLog?.Invoke(1, "Nginx bulunamadı!");
            return;
        }

        OnLog?.Invoke(0, "Virtual host yapılandırması güncelleniyor...");
        _virtualHostManager.UpdateVirtualHosts();

        var nginxDir = Path.GetDirectoryName(nginxPath)!;

        var startInfo = new ProcessStartInfo
        {
            FileName = nginxPath,
            Arguments = "-c conf/nginx.conf",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = nginxDir
        };

        _nginxProcess = Process.Start(startInfo);
        OnLog?.Invoke(0, $"Nginx başlatıldı (PID: {_nginxProcess?.Id})");
    }

    private void StartMysql()
    {
        var mysqlPath = _binaryManager.MysqlPath;
        
        if (!File.Exists(mysqlPath))
        {
            OnLog?.Invoke(0, "MySQL bulunamadı, atlanıyor");
            return;
        }

        var mysqlDir = Path.GetDirectoryName(Path.GetDirectoryName(mysqlPath))!;
        
        var startInfo = new ProcessStartInfo
        {
            FileName = mysqlPath,
            Arguments = "--console",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = mysqlDir
        };

        _mysqlProcess = Process.Start(startInfo);
        OnLog?.Invoke(0, $"MySQL başlatıldı (PID: {_mysqlProcess?.Id})");
    }

    public void Stop()
    {
        try
        {
            if (_nginxProcess != null && !_nginxProcess.HasExited)
            {
                _nginxProcess.Kill();
                _nginxProcess.Dispose();
                _nginxProcess = null;
            }

            if (_phpProcess != null && !_phpProcess.HasExited)
            {
                _phpProcess.Kill();
                _phpProcess.Dispose();
                _phpProcess = null;
            }

            if (_mysqlProcess != null && !_mysqlProcess.HasExited)
            {
                _mysqlProcess.Kill();
                _mysqlProcess.Dispose();
                _mysqlProcess = null;
            }

            try
            {
                var nginxKill = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/F /IM nginx.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(nginxKill);
            }
            catch { }

            try
            {
                var phpKill = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/F /IM php-cgi.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(phpKill);
            }
            catch { }

            try
            {
                var mysqlKill = new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = "/F /IM mysqld.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(mysqlKill);
            }
            catch { }

            OnStatusChanged?.Invoke("Durduruldu");
            OnLog?.Invoke(0, "Tüm servisler durduruldu");
        }
        catch (Exception ex)
        {
            OnLog?.Invoke(1, $"Durdurma hatası: {ex.Message}");
        }
    }

    public async Task StartNginxAsync()
    {
        if (_nginxProcess != null && !_nginxProcess.HasExited)
        {
            OnLog?.Invoke(0, "Nginx zaten çalışıyor");
            return;
        }
        
        await _binaryManager.EnsureBinariesAsync();
        StartNginx();
    }

    public void StopNginx()
    {
        if (_nginxProcess != null && !_nginxProcess.HasExited)
        {
            _nginxProcess.Kill();
            _nginxProcess.Dispose();
            _nginxProcess = null;
            OnLog?.Invoke(0, "Nginx durduruldu");
        }
        try
        {
            var nginxKill = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/F /IM nginx.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(nginxKill);
        }
        catch { }
    }

    public void StartPhpInternal()
    {
        if (_phpProcess != null && !_phpProcess.HasExited)
        {
            OnLog?.Invoke(0, "PHP zaten çalışıyor");
            return;
        }
        
        var phpCgiPath = _binaryManager.PhpCgiPath;
        var phpCgiExe = Path.Combine(phpCgiPath, "php-cgi.exe");
        if (!File.Exists(phpCgiExe))
        {
            OnLog?.Invoke(1, "PHP bulunamadı!");
            return;
        }

        var phpIni = Path.Combine(phpCgiPath, "php.ini");
        
        var startInfo = new ProcessStartInfo
        {
            FileName = phpCgiExe,
            Arguments = $"-b 127.0.0.1:9000 -c \"{phpIni}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _binaryManager.WwwPath
        };

        _phpProcess = Process.Start(startInfo);
        OnLog?.Invoke(0, $"PHP-FPM başlatıldı (PID: {_phpProcess?.Id})");
    }

    public void StopPhp()
    {
        if (_phpProcess != null && !_phpProcess.HasExited)
        {
            _phpProcess.Kill();
            _phpProcess.Dispose();
            _phpProcess = null;
            OnLog?.Invoke(0, "PHP durduruldu");
        }
        try
        {
            var phpKill = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/F /IM php-cgi.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(phpKill);
        }
        catch { }
    }

    public void StartMysqlInternal()
    {
        if (_mysqlProcess != null && !_mysqlProcess.HasExited)
        {
            OnLog?.Invoke(0, "MySQL zaten çalışıyor");
            return;
        }
        
        var mysqlPath = _binaryManager.MysqlPath;
        
        if (!File.Exists(mysqlPath))
        {
            OnLog?.Invoke(0, "MySQL bulunamadı, atlanıyor");
            return;
        }

        var mysqlDir = Path.GetDirectoryName(Path.GetDirectoryName(mysqlPath))!;
        
        var startInfo = new ProcessStartInfo
        {
            FileName = mysqlPath,
            Arguments = "--console",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = mysqlDir
        };

        _mysqlProcess = Process.Start(startInfo);
        OnLog?.Invoke(0, $"MySQL başlatıldı (PID: {_mysqlProcess?.Id})");
    }

    public void StopMysql()
    {
        if (_mysqlProcess != null && !_mysqlProcess.HasExited)
        {
            _mysqlProcess.Kill();
            _mysqlProcess.Dispose();
            _mysqlProcess = null;
            OnLog?.Invoke(0, "MySQL durduruldu");
        }
        try
        {
            var mysqlKill = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = "/F /IM mysqld.exe",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            Process.Start(mysqlKill);
        }
        catch { }
    }
}
