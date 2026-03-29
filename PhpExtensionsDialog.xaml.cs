using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MerHost.Services;

namespace MerHost;

public class PhpExtension : INotifyPropertyChanged
{
    private bool _isEnabled;
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            Status = value ? "Aktif" : "Pasif";
            OnPropertyChanged(nameof(IsEnabled));
            OnPropertyChanged(nameof(Status));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class PhpExtensionsDialog : Window
{
    private readonly ServerManager _serverManager;
    private string _phpIniPath = "";
    private string _phpPath = "";
    private ObservableCollection<PhpExtension> _extensions = new();
    private Dictionary<string, string> _phpSettings = new();

    public PhpExtensionsDialog(ServerManager serverManager)
    {
        InitializeComponent();
        _serverManager = serverManager;
        LoadExtensions();
        LoadQuickSettings();
    }

    private void LoadQuickSettings()
    {
        try
        {
            if (!File.Exists(_phpIniPath))
                return;

            var content = File.ReadAllText(_phpIniPath);
            
            UpdateButtonTag(UploadMaxFilesizeBtn, content, "upload_max_filesize", "upload_max_filesize: 2M");
            UpdateButtonTag(PostMaxSizeBtn, content, "post_max_size", "post_max_size: 8M");
            UpdateButtonTag(MaxExecutionTimeBtn, content, "max_execution_time", "max_execution_time: 30");
            UpdateButtonTag(MemoryLimitBtn, content, "memory_limit", "memory_limit: 128M");
            UpdateButtonTag(MaxInputTimeBtn, content, "max_input_time", "max_input_time: 60");
            UpdateButtonTag(DisplayErrorsBtn, content, "display_errors", "display_errors: Off", "display_errors: On");
            UpdateButtonTag(ErrorReportingBtn, content, "error_reporting", "error_reporting: E_ALL");
            
            var xdebugEnabled = content.Contains("zend_extension=xdebug") || content.Contains("zend_extension=\"xdebug\"");
            XdebugBtn.Content = xdebugEnabled ? "xdebug: Açık" : "xdebug: Kapalı";
            XdebugBtn.Background = xdebugEnabled ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 134, 54)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 38, 45));
        }
        catch { }
    }

    private void UpdateButtonTag(Button btn, string content, string setting, string defaultVal, string? altVal = null)
    {
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(setting, StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith(";"))
            {
                var val = trimmed.Split('=')[1].Trim();
                btn.Content = $"{setting}: {val}";
                
                if (altVal != null && val != "0" && val.ToLower() != "off" && val.ToLower() != "false")
                {
                    btn.Content = altVal;
                    btn.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 134, 54));
                }
                return;
            }
        }
        btn.Content = defaultVal;
    }

    private void LoadExtensions()
    {
        try
        {
            var binaryManager = _serverManager.BinaryManager;
            _phpPath = binaryManager.PhpCgiPath;
            _phpIniPath = Path.Combine(_phpPath, "php.ini");

            if (!File.Exists(_phpIniPath))
            {
                MessageBox.Show($"php.ini dosyası bulunamadı: {_phpIniPath}", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var phpVersion = GetPhpVersion(_phpPath);
            PhpInfoText.Text = $"PHP: {phpVersion}";

            var iniContent = File.ReadAllText(_phpIniPath);
            var enabledExtensions = ParseEnabledExtensions(iniContent);
            var allExtensions = GetAvailableExtensions(_phpPath);

            _extensions.Clear();
            foreach (var ext in allExtensions)
            {
                _extensions.Add(new PhpExtension
                {
                    Name = ext,
                    IsEnabled = enabledExtensions.Contains(ext),
                    Status = enabledExtensions.Contains(ext) ? "Aktif" : "Pasif"
                });
            }

            ExtensionsList.ItemsSource = _extensions;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Eklentiler yüklenirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetPhpVersion(string phpPath)
    {
        try
        {
            var phpExe = Path.Combine(phpPath, "php.exe");
            if (File.Exists(phpExe))
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = phpExe,
                    Arguments = "-v",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(startInfo);
                var output = process?.StandardOutput.ReadToEnd();
                process?.WaitForExit();
                
                if (!string.IsNullOrEmpty(output))
                {
                    var firstLine = output.Split('\n')[0];
                    return firstLine;
                }
            }
        }
        catch { }
        return "PHP";
    }

    private HashSet<string> ParseEnabledExtensions(string iniContent)
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = iniContent.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(";"))
                continue;
                
            if (trimmed.StartsWith("extension=", StringComparison.OrdinalIgnoreCase))
            {
                var ext = trimmed.Substring("extension=".Length).Trim().Trim('"');
                if (ext.EndsWith(".dll"))
                    ext = ext.Substring(0, ext.Length - 4);
                enabled.Add(ext);
            }
            else if (trimmed.StartsWith("zend_extension=", StringComparison.OrdinalIgnoreCase))
            {
                var ext = trimmed.Substring("zend_extension=".Length).Trim().Trim('"');
                if (ext.EndsWith(".dll"))
                    ext = ext.Substring(0, ext.Length - 4);
                if (ext == "xdebug" || ext == "php_xdebug")
                    enabled.Add("xdebug");
            }
        }
        return enabled;
    }

    private List<string> GetAvailableExtensions(string phpPath)
    {
        var extensions = new List<string>();
        var extDir = Path.Combine(phpPath, "ext");
        
        if (Directory.Exists(extDir))
        {
            foreach (var file in Directory.GetFiles(extDir, "*.dll"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                extensions.Add(name);
            }
        }
        
        return extensions.OrderBy(e => e).ToList();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        LoadExtensions();
        LoadQuickSettings();
    }

    private void Setting_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string setting)
            return;

        var dialog = new InputDialog($"{setting} değerini girin:", GetCurrentValue(setting));
        dialog.Owner = this;
        
        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.InputValue))
            return;

        ApplySetting(setting, dialog.InputValue);
        LoadQuickSettings();
        
        MessageBox.Show($"{setting} = {dialog.InputValue} olarak güncellendi. Kaydet butonuna basarak PHP'yi yeniden başlatın.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private string GetCurrentValue(string setting)
    {
        try
        {
            if (!File.Exists(_phpIniPath))
                return "";

            var content = File.ReadAllText(_phpIniPath);
            var lines = content.Split('\n');
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(setting, StringComparison.OrdinalIgnoreCase) && !trimmed.StartsWith(";"))
                {
                    if (trimmed.Contains("="))
                        return trimmed.Split('=')[1].Trim();
                }
            }
        }
        catch { }
        
        return setting switch
        {
            "upload_max_filesize" => "2M",
            "post_max_size" => "8M",
            "max_execution_time" => "30",
            "memory_limit" => "128M",
            "max_input_time" => "60",
            "display_errors" => "Off",
            "error_reporting" => "E_ALL",
            _ => ""
        };
    }

    private void ApplySetting(string setting, string value)
    {
        try
        {
            if (!File.Exists(_phpIniPath))
                return;

            var lines = File.ReadAllLines(_phpIniPath);
            var newLines = new List<string>();
            var found = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (trimmed.StartsWith(setting + "=", StringComparison.OrdinalIgnoreCase))
                {
                    newLines.Add($"{setting} = {value}");
                    found = true;
                }
                else if (trimmed.StartsWith(";" + setting + "=", StringComparison.OrdinalIgnoreCase))
                {
                    newLines.Add($"{setting} = {value}");
                    found = true;
                }
                else
                {
                    newLines.Add(line);
                }
            }

            if (!found)
            {
                newLines.Add($"{setting} = {value}");
            }

            File.WriteAllLines(_phpIniPath, newLines);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ayar kaydedilirken hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAndRestart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_phpIniPath) || !File.Exists(_phpIniPath))
            {
                MessageBox.Show("php.ini bulunamadı!", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var lines = File.ReadAllLines(_phpIniPath);
            var newLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                
                if (trimmed.StartsWith(";extension=", StringComparison.OrdinalIgnoreCase) || 
                    trimmed.StartsWith("; extension=", StringComparison.OrdinalIgnoreCase))
                {
                    var extName = GetExtensionName(trimmed);
                    var isEnabled = _extensions.Any(e => e.Name.Equals(extName, StringComparison.OrdinalIgnoreCase) && e.IsEnabled);
                    
                    if (isEnabled)
                    {
                        newLines.Add(line.TrimStart(';', ' '));
                        continue;
                    }
                }
                else if (trimmed.StartsWith("extension=", StringComparison.OrdinalIgnoreCase) ||
                         trimmed.StartsWith(" extension=", StringComparison.OrdinalIgnoreCase))
                {
                    var extName = GetExtensionName(trimmed);
                    var isEnabled = _extensions.Any(e => e.Name.Equals(extName, StringComparison.OrdinalIgnoreCase) && e.IsEnabled);
                    
                    if (!isEnabled)
                    {
                        newLines.Add("; " + line);
                        continue;
                    }
                }
                
                newLines.Add(line);
            }

            File.WriteAllLines(_phpIniPath, newLines);
            
            _serverManager.StopPhp();
            _serverManager.StartPhpInternal();
            
            MessageBox.Show("Eklentiler kaydedildi ve PHP yeniden başlatıldı!", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string GetExtensionName(string line)
    {
        var idx = line.IndexOf("extension=", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var ext = line.Substring(idx + "extension=".Length).Trim().Trim('"');
            if (ext.EndsWith(".dll"))
                ext = ext.Substring(0, ext.Length - 4);
            return ext;
        }
        return "";
    }
}
