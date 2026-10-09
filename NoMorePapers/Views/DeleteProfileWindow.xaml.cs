using System.Windows;
using System.Windows.Controls;

namespace NoMorePapers.Views
{
    public partial class DeleteProfileWindow : Window
    {
        public DeleteProfileWindow(string profileName, bool requiresPassword = false)
        {
            InitializeComponent();
            Title = $"Borrar perfil: {profileName}";
            if (requiresPassword)
            {
                ((TextBlock)FindName("PasswordPrompt")).Visibility = Visibility.Visible;
                ((PasswordBox)FindName("ConfirmationPassword")).Visibility = Visibility.Visible;
            }
        }

        public string EnteredPassword => ((PasswordBox)FindName("ConfirmationPassword")).Password;

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void ConfirmClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
