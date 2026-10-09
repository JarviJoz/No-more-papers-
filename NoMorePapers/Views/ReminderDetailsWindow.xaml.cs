using NoMorePapers.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NoMorePapers.Views
{
    public partial class ReminderDetailsWindow : Window
    {
        private readonly Reminder _reminder;
        private readonly Func<Task> _deleteAction;

        public ReminderDetailsWindow(Reminder reminder, Func<Task> deleteAction)
        {
            InitializeComponent();
            _reminder = reminder;
            _deleteAction = deleteAction;
            DataContext = reminder;
            DeleteButton.IsEnabled = reminder.ScheduledAt.Date >= DateTime.Today;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "¿Está seguro de que desea eliminar este recordatorio?",
                "Eliminar recordatorio",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            await _deleteAction();
            Close();
        }
    }
}
