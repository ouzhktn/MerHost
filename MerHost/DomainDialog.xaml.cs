using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace MerHost;

public partial class DomainDialog : Window
{
    public string DomainName { get; private set; } = "";
    public string ProjectFolder { get; private set; } = "";
    public bool IsNodeProject { get; private set; } = false;
    public int NodePort { get; private set; } = 3000;
    private readonly string _wwwPath;

    public DomainDialog(string wwwPath)
    {
        InitializeComponent();
        _wwwPath = wwwPath;
        FolderTextBox.Text = wwwPath;
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Proje Klasörü Seç",
            InitialDirectory = _wwwPath
        };

        if (dialog.ShowDialog() == true)
        {
            FolderTextBox.Text = dialog.FolderName;
        }
    }

    private void NodeJsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        NodePortPanel.Visibility = NodeJsCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        var domain = DomainTextBox.Text.Trim().ToLower();
        
        if (string.IsNullOrEmpty(domain))
        {
            MessageBox.Show("Lütfen domain adı girin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (domain.Contains("."))
        {
            MessageBox.Show("Lütfen sadece domain adını girin (.test eklenmez)!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrEmpty(FolderTextBox.Text) || !Directory.Exists(FolderTextBox.Text))
        {
            MessageBox.Show("Lütfen geçerli bir proje klasörü seçin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        IsNodeProject = NodeJsCheckBox.IsChecked == true;
        
        if (IsNodeProject)
        {
            if (!int.TryParse(NodePortTextBox.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Lütfen geçerli bir port numarası girin (1-65535)!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            NodePort = port;
        }

        DomainName = domain;
        
        var fullPath = FolderTextBox.Text.Trim();
        if (fullPath.StartsWith(_wwwPath, StringComparison.OrdinalIgnoreCase))
        {
            ProjectFolder = fullPath.Substring(_wwwPath.Length).Trim(Path.DirectorySeparatorChar);
        }
        else
        {
            ProjectFolder = Path.GetFileName(fullPath);
        }
        
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
