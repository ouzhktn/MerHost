using System.IO;
using System.Windows;
using MerHost.Services;
using Microsoft.Win32;

namespace MerHost;

public partial class SettingsDialog : Window
{
    private readonly ServerManager _serverManager;
    private readonly string _settingsPath;

    public SettingsDialog(ServerManager serverManager)
    {
        InitializeComponent();
        
        _serverManager = serverManager;
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MerHost", "settings.ini");
        
        LoadSettings();
        LoadVersionInfo();
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
                            case "HttpPort":
                                HttpPortTextBox.Text = value;
                                break;
                            case "HttpsPort":
                                HttpsPortTextBox.Text = value;
                                break;
                            case "AutoStart":
                                AutoStartCheckBox.IsChecked = value == "1";
                                break;
                            case "StartMinimized":
                                StartMinimizedCheckBox.IsChecked = value == "1";
                                break;
                            case "AutoStartServers":
                                AutoStartServersCheckBox.IsChecked = value == "1";
                                break;
                        }
                    }
                }
            }
            
            if (string.IsNullOrEmpty(HttpPortTextBox.Text))
                HttpPortTextBox.Text = "80";
            if (string.IsNullOrEmpty(HttpsPortTextBox.Text))
                HttpsPortTextBox.Text = "443";
        }
        catch
        {
        }
    }

    private void LoadVersionInfo()
    {
        var binPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MerHost", "bin");
        
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MerHost");
        
        var nginxPath = Path.Combine(binPath, "nginx");
        if (File.Exists(Path.Combine(nginxPath, "nginx.exe")))
        {
            NginxVersionText.Text = "1.28.2";
        }
        else
        {
            NginxVersionText.Text = "Yüklü değil";
        }
        
        var phpPath = Path.Combine(binPath, "php");
        if (File.Exists(Path.Combine(phpPath, "php-cgi.exe")))
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Path.Combine(phpPath, "php.exe"),
                    Arguments = "-v",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    var output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    var firstLine = output.Split('\n')[0];
                    PhpVersionText.Text = firstLine.Replace("PHP ", "").Split(' ')[0];
                }
            }
            catch
            {
                PhpVersionText.Text = "8.x";
            }
        }
        else
        {
            PhpVersionText.Text = "Yüklü değil";
        }
        
        var mysqlPath = Path.Combine(binPath, "mariadb");
        if (File.Exists(Path.Combine(mysqlPath, "bin", "mariadb.exe")) || Directory.Exists(Path.Combine(binPath, "mariadb-11.4.2-winx64")))
        {
            MysqlVersionText.Text = "11.4.2";
        }
        else
        {
            MysqlVersionText.Text = "Yüklü değil";
        }

        var phpmyadminPath = Path.Combine(appDataPath, "phpmyadmin");
        if (Directory.Exists(phpmyadminPath))
        {
            PhpMyAdminVersionText.Text = "5.2.3";
        }
        else
        {
            PhpMyAdminVersionText.Text = "Yüklü değil";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.GetDirectoryName(_settingsPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);
            
            var lines = new List<string>
            {
                $"HttpPort={HttpPortTextBox.Text}",
                $"HttpsPort={HttpsPortTextBox.Text}",
                $"AutoStart={(AutoStartCheckBox.IsChecked == true ? "1" : "0")}",
                $"StartMinimized={(StartMinimizedCheckBox.IsChecked == true ? "1" : "0")}",
                $"AutoStartServers={(AutoStartServersCheckBox.IsChecked == true ? "1" : "0")}"
            };
            
            File.WriteAllLines(_settingsPath, lines);
            
            SetAutoStart(AutoStartCheckBox.IsChecked == true);
            
            MessageBox.Show("Ayarlar kaydedildi!", "MerHost", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                if (enable)
                {
                    var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (exePath.EndsWith(".dll"))
                    {
                        exePath = exePath.Replace(".dll", ".exe");
                    }
                    key.SetValue("MerHost", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("MerHost", false);
                }
                key.Close();
            }
        }
        catch
        {
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
