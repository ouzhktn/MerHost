using System.Windows;

namespace MerHost;

public partial class InputDialog : Window
{
    public string InputValue { get; private set; } = "";

    public InputDialog(string prompt, string defaultValue = "")
    {
        InitializeComponent();
        PromptText.Text = prompt;
        InputTextBox.Text = defaultValue;
        InputTextBox.SelectAll();
        InputTextBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        InputValue = InputTextBox.Text;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
