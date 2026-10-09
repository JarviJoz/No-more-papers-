using NoMorePapers.Models;
using NoMorePapers.Services;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace NoMorePapers.Views
{
    public partial class ContactWindow : Window
    {
        private readonly ContactSubmissionService _submissionService;
        private CancellationTokenSource? _submissionCancellation;
        private bool _isSubmitting;
        private bool _allowClose;
        private bool _isClosing;

        public ContactWindow(ContactSubmissionService submissionService)
        {
            InitializeComponent();
            _submissionService = submissionService;
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            ClearValidation();
            if (!ValidateForm())
            {
                return;
            }

            SetSubmitting(true);
            _submissionCancellation = new CancellationTokenSource();
            try
            {
                var contactMessage = new ContactMessage(
                    NameInput.Text.Trim(),
                    EmailInput.Text.Trim(),
                    SubjectInput.Text.Trim(),
                    MessageInput.Text.Trim());
                await _submissionService.SubmitAsync(contactMessage, _submissionCancellation.Token);
                if (_isClosing)
                {
                    return;
                }

                FormPanel.Visibility = Visibility.Collapsed;
                SuccessPanel.Visibility = Visibility.Visible;
                SendButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                DoneButton.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) when (_isClosing)
            {
            }
            catch (Exception exception)
            {
                FormStatus.Text = exception.Message;
                FormStatus.Visibility = Visibility.Visible;
            }
            finally
            {
                _submissionCancellation.Dispose();
                _submissionCancellation = null;
                SetSubmitting(false);
            }
        }

        private bool ValidateForm()
        {
            var valid = true;
            if (string.IsNullOrWhiteSpace(NameInput.Text))
            {
                ShowFieldError(NameError, "El nombre es obligatorio.");
                valid = false;
            }
            else if (NameInput.Text.Trim().Length > 100)
            {
                ShowFieldError(NameError, "El nombre no puede superar 100 caracteres.");
                valid = false;
            }

            var email = EmailInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                ShowFieldError(EmailError, "El correo electrónico es obligatorio.");
                valid = false;
            }
            else if (!MailAddress.TryCreate(email, out var address) || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                ShowFieldError(EmailError, "Introduce un correo electrónico válido.");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(SubjectInput.Text))
            {
                ShowFieldError(SubjectError, "El asunto es obligatorio.");
                valid = false;
            }
            else if (SubjectInput.Text.Trim().Length > 200)
            {
                ShowFieldError(SubjectError, "El asunto no puede superar 200 caracteres.");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(MessageInput.Text))
            {
                ShowFieldError(MessageError, "El mensaje es obligatorio.");
                valid = false;
            }
            else if (MessageInput.Text.Trim().Length > 5000)
            {
                ShowFieldError(MessageError, "El mensaje no puede superar 5000 caracteres.");
                valid = false;
            }

            return valid;
        }

        private static void ShowFieldError(TextBlock field, string message)
        {
            field.Text = message;
            field.Visibility = Visibility.Visible;
        }

        private void ClearValidation()
        {
            foreach (var error in new[] { NameError, EmailError, SubjectError, MessageError })
            {
                error.Text = string.Empty;
                error.Visibility = Visibility.Collapsed;
            }

            FormStatus.Text = string.Empty;
            FormStatus.Visibility = Visibility.Collapsed;
        }

        private void Input_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSubmitting)
            {
                return;
            }

            if (ReferenceEquals(sender, NameInput)) NameError.Visibility = Visibility.Collapsed;
            if (ReferenceEquals(sender, EmailInput)) EmailError.Visibility = Visibility.Collapsed;
            if (ReferenceEquals(sender, SubjectInput)) SubjectError.Visibility = Visibility.Collapsed;
            if (ReferenceEquals(sender, MessageInput)) MessageError.Visibility = Visibility.Collapsed;
            FormStatus.Visibility = Visibility.Collapsed;
        }

        private void SetSubmitting(bool submitting)
        {
            _isSubmitting = submitting;
            FormPanel.IsEnabled = !submitting;
            SendButton.IsEnabled = !submitting;
            SendingIndicator.Visibility = submitting ? Visibility.Visible : Visibility.Collapsed;
            SendButtonText.Text = submitting ? "Enviando..." : "Enviar mensaje";
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
            _isClosing = true;
            _submissionCancellation?.Cancel();
            _allowClose = true;
            var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(140));
            animation.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, animation);
        }
    }
}