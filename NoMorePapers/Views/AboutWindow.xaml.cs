using NoMorePapers.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NoMorePapers.Views
{
    public partial class AboutWindow : Window
    {
        private readonly ContactSubmissionService _contactSubmissionService;
        private bool _allowClose;

        public AboutWindow(ContactSubmissionService contactSubmissionService)
        {
            InitializeComponent();
            _contactSubmissionService = contactSubmissionService;
        }

        private void Contact_Click(object sender, RoutedEventArgs e)
        {
            var contactWindow = new ContactWindow(_contactSubmissionService) { Owner = this };
            contactWindow.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            e.Cancel = true;
            _allowClose = true;
            var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(140));
            animation.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, animation);
        }
    }
}