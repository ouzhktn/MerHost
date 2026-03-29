using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace MerHost;

public partial class DomainDialog : Window
{
    public string DomainName { get; private set; } = "";
    public string ProjectFolder { get; private set; } = "";
    private readonly string _wwwPath;

    public DomainDialog(string wwwPath)
    {
        InitializeComponent();
        _wwwPath = wwwPath;
        LoadFolders();
    }

    private void LoadFolders()
    {
        FolderComboBox.Items.Clear();
        
        if (Directory.Exists(_wwwPath))
        {
            var dirs = Directory.GetDirectories(_wwwPath);
            foreach (var dir in dirs)
            {
                var dirName = Path.GetFileName(dir);
                if (!dirName.StartsWith(".") && dirName != "phpmyadmin")
                {
                    FolderComboBox.Items.Add(dirName);
                }
            }
        }

        if (FolderComboBox.Items.Count > 0)
        {
            FolderComboBox.SelectedIndex = 0;
        }
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

        if (FolderComboBox.SelectedItem == null)
        {
            MessageBox.Show("Lütfen bir proje klasörü seçin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DomainName = domain;
        ProjectFolder = FolderComboBox.SelectedItem.ToString() ?? "";
        
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
