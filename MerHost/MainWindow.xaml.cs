using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MerHost.Services;

namespace MerHost;

public partial class MainWindow : Window
{
    private readonly ServerManager _serverManager;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();
        
        try
        {
            var exePath = AppDomain.CurrentDomain.BaseDirectory + "MerHost.exe";
            if (System.IO.File.Exists(exePath))
            {
                TrayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            }
        }
        catch { }

        _serverManager = new ServerManager();
        _serverManager.OnStatusChanged += OnStatusChanged;
        _serverManager.OnLog += OnLog;
        _serverManager.OnDownloadProgress += OnDownloadProgress;
        _serverManager.OnNodeStarted += () =>
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "http://localhost:3000",
                        UseShellExecute = true
                    });
                }
                catch { }
            });
        };

        WwwPathText.Text = $"www: {_serverManager.WwwRoot}";
        
        RefreshDomainList();
        OnLog(0, "MerHost hazır. Sunucuyu başlatmak için tıklayın.");
    }

    private void OnStatusChanged(string status)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            
            if (status.Contains("Çalışıyor"))
            {
                StatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = true;
                UrlText.Text = $"http://localhost:{_serverManager.Port}";
            }
            else if (status.Contains("Hata") || status.Contains("bulunamadı"))
            {
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                UrlText.Text = "";
            }
            else
            {
                StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                UrlText.Text = "";
            }
        });
    }

    private void OnLog(int level, string message)
    {
        Dispatcher.Invoke(() =>
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogList.Items.Insert(0, $"[{timestamp}] {message}");
            
            if (LogList.Items.Count > 100)
                LogList.Items.RemoveAt(LogList.Items.Count - 1);
        });
    }

    private void OnDownloadProgress(string message)
    {
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = message;
        });
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        await _serverManager.StartAsync();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.Stop();
    }

    private async void StartNginx_Click(object sender, RoutedEventArgs e)
    {
        await _serverManager.StartNginxAsync();
        UpdateServiceStatus();
        RefreshDomainList();
    }

    private void StopNginx_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StopNginx();
        UpdateServiceStatus();
    }

    private void StartPhp_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StartPhpInternal();
        UpdateServiceStatus();
    }

    private void StopPhp_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StopPhp();
        UpdateServiceStatus();
    }

    private void StartMysql_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StartMysqlInternal();
        UpdateServiceStatus();
    }

    private void StopMysql_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StopMysql();
        UpdateServiceStatus();
    }

    private async void StartNode_Click(object sender, RoutedEventArgs e)
    {
        await _serverManager.StartNodeAsync();
        UpdateServiceStatus();
    }

    private void StopNode_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StopNode();
        UpdateServiceStatus();
    }

    private void SelectNodeProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Node.js Projesi Seç"
        };

        if (dialog.ShowDialog() == true)
        {
            _serverManager.SetNodeProjectPath(dialog.FolderName);
            NodeProjectPath.Text = dialog.FolderName;
            OnLog(0, $"Proje seçildi: {dialog.FolderName}");
        }
    }

    private async void NpmInstall_Click(object sender, RoutedEventArgs e)
    {
        await _serverManager.NpmInstallAsync();
    }

    private async void NpmStart_Click(object sender, RoutedEventArgs e)
    {
        await _serverManager.StartNpmRunAsync();
    }

    private void NpmStop_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.StopNode();
        OnLog(0, "Node.js projesi durduruldu");
    }

    private void OpenNodeInBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = "http://localhost:3000";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            OnLog(1, "Tarayıcı açma hatası: " + ex.Message);
        }
    }

    private void OpenWwwFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", _serverManager.WwwRoot);
        }
        catch (Exception ex)
        {
            OnLog(1, $"Klasör açma hatası: {ex.Message}");
        }
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_serverManager);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void PhpExtensions_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PhpExtensionsDialog(_serverManager);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void OpenLocalhost_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("http://localhost");
        }
        catch { }
    }

    private void OpenLocalhostSSL_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("https://localhost");
        }
        catch { }
    }

    private void OpenPhpMyAdmin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start("http://localhost/phpmyadmin");
        }
        catch { }
    }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var logText = string.Join(Environment.NewLine, LogList.Items.Cast<object>());
            Clipboard.SetText(logText);
            OnLog(0, "Log panosuna kopyalandı");
        }
        catch { }
    }

    private void RefreshDomains_Click(object sender, RoutedEventArgs e)
    {
        RefreshDomainList();
    }

    private void CreateDomain_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new DomainDialog(_serverManager.WwwRoot);
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true)
        {
            bool success;
            if (dialog.IsNodeProject)
            {
                success = _serverManager.CreateNodeDomain(dialog.DomainName, dialog.ProjectFolder, dialog.NodePort);
                if (success)
                {
                    RefreshDomainList();
                    OnLog(0, $"Node.js Domain oluşturuldu: https://{dialog.DomainName}.test -> localhost:{dialog.NodePort}");
                }
            }
            else
            {
                success = _serverManager.CreateDomain(dialog.DomainName, dialog.ProjectFolder);
                if (success)
                {
                    RefreshDomainList();
                    OnLog(0, $"Domain oluşturuldu: https://{dialog.DomainName}.test");
                }
            }
        }
    }

    private void RefreshDomainList()
    {
        Dispatcher.Invoke(() =>
        {
            DomainList.Items.Clear();
            
            DomainList.Items.Add("http://localhost");
            DomainList.Items.Add("https://localhost");
            DomainList.Items.Add("http://localhost/phpmyadmin");
            DomainList.Items.Add("https://localhost/phpmyadmin");
            
            foreach (var domain in _serverManager.ProjectDomains)
            {
                DomainList.Items.Add($"https://{domain}");
            }
        });
    }

    private void DomainList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DomainList.SelectedItem != null)
        {
            var url = DomainList.SelectedItem.ToString();
            if (!string.IsNullOrEmpty(url))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }
    }

    private void UpdateServiceStatus()
    {
        Dispatcher.Invoke(() =>
        {
            NginxStatus.Text = _serverManager.IsNginxRunning ? "●" : "○";
            NginxStatus.Foreground = _serverManager.IsNginxRunning ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Gray;
            
            PhpStatus.Text = _serverManager.IsPhpRunning ? "●" : "○";
            PhpStatus.Foreground = _serverManager.IsPhpRunning ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Gray;
            
            MysqlStatus.Text = _serverManager.IsMysqlRunning ? "●" : "○";
            MysqlStatus.Foreground = _serverManager.IsMysqlRunning ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Gray;
            
            NodeStatus.Text = _serverManager.IsNodeRunning ? "●" : "○";
            NodeStatus.Foreground = _serverManager.IsNodeRunning ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Gray;
        });
    }

    private void StartMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = _serverManager.StartAsync();
    }

    private void StopMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _serverManager.Stop();
    }

    private void ShowMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_isClosing)
        {
            e.Cancel = true;
            Hide();
            TrayIcon.ShowBalloonTip("MerHost", "Uygulama sistem tepsisinde çalışıyor", Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        }
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    public void ForceClose()
    {
        _isClosing = true;
        _serverManager.Stop();
        TrayIcon.Dispose();
        Close();
    }

    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ForceClose();
    }

    private void LogList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (LogList.SelectedItem != null)
        {
            Clipboard.SetText(LogList.SelectedItem.ToString());
            StatusText.Text = "Log kopyalandı!";
        }
    }

    private void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "merhost-log.txt");
        var logs = new List<string>();
        foreach (var item in LogList.Items)
        {
            logs.Add(item.ToString());
        }
        File.WriteAllLines(logPath, logs);
        StatusText.Text = $"Log kaydedildi: {logPath}";
    }
}
