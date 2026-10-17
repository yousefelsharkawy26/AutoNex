using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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
            ResultTextBlock.Foreground = Brushes.Red;
            return;
        }

        if (!int.TryParse(ageText, out int age))
        {
            ResultTextBlock.Text = "Age must be a valid number.";
            ResultTextBlock.Foreground = Brushes.Red;
            return;
        }

        string country = (CountryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Unknown";
        bool rememberMe = RememberMeCheckBox.IsChecked == true;
        string role = AdminRoleRadioButton.IsChecked == true ? "Admin" : "User";

        ResultTextBlock.Text = $"Hello, {firstName} {lastName}! You are {age} years old from {country} ({role}).";
        ResultTextBlock.Foreground = Brushes.Green;
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        FirstNameTextBox.Text = string.Empty;
        LastNameTextBox.Text = string.Empty;
        AgeTextBox.Text = string.Empty;
        CountryComboBox.SelectedIndex = 0;
        RememberMeCheckBox.IsChecked = false;
        UserRoleRadioButton.IsChecked = true;
        ResultTextBlock.Text = string.Empty;
    }
}