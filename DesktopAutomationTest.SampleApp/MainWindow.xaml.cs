using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace DesktopAutomationTest.SampleApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        string firstName = FirstNameTextBox.Text;
        string lastName = LastNameTextBox.Text;
        string ageText = AgeTextBox.Text;

        if (string.IsNullOrWhiteSpace(firstName) ||
            string.IsNullOrWhiteSpace(lastName) ||
            string.IsNullOrWhiteSpace(ageText))
        {
            ResultTextBlock.Text = "Please fill in all fields.";
            ResultTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        if (!int.TryParse(ageText, out int age))
        {
            ResultTextBlock.Text = "Age must be a valid number.";
            ResultTextBlock.Foreground = System.Windows.Media.Brushes.Red;
            return;
        }

        ResultTextBlock.Text = $"Hello, {firstName} {lastName}! You are {age} years old.";
        ResultTextBlock.Foreground = System.Windows.Media.Brushes.Green;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        FirstNameTextBox.Text = string.Empty;
        LastNameTextBox.Text = string.Empty;
        AgeTextBox.Text = string.Empty;
        ResultTextBlock.Text = string.Empty;
    }
}