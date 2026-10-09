using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using NoMorePapers.Helpers;
using NoMorePapers.Models;
using NoMorePapers.Services;
using NoMorePapers.ViewModels;
using System.Globalization;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace NoMorePapers.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettingsService _settingsService;
        private readonly UserProfile _currentProfile;
        private readonly IProfileService _profileService;
        private readonly MainViewModel _mainViewModel;
        private readonly AppSettings _originalSettings;
        private readonly byte[]? _originalCustomSound;
        private readonly DispatcherTimer _statusTimer;
        private bool _initializing = true;
        private bool _allowClose;
        private bool _changesSaved;
        private bool _settingsRestored;
        private MediaPlayer? _previewPlayer;

        public SettingsWindow(AppSettingsService settingsService, UserProfile currentProfile, IProfileService profileService, MainViewModel mainViewModel)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _currentProfile = currentProfile;
            _profileService = profileService;
            _mainViewModel = mainViewModel;
            _originalSettings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settingsService.Settings))!;
            _originalCustomSound = _originalSettings.CustomSoundPath is { } originalPath && File.Exists(originalPath)
                ? File.ReadAllBytes(originalPath)
                : null;
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statusTimer.Tick += (_, _) => UpdateNotificationStatus();
            LoadSettings();
            _initializing = false;
            UpdateNotificationStatus();
            _statusTimer.Start();
        }

        private void LoadSettings()
        {
            var settings = _settingsService.Settings;
            SelectComboItem(FontSizeCombo, settings.FontSize);
            SelectComboItem(AppearanceCombo, settings.Appearance);
            VolumeSlider.Value = settings.NotificationVolume;
            SoundsEnabledCheck.IsChecked = settings.SoundsEnabled;
            VolumeSlider.IsEnabled = settings.SoundsEnabled;
            NotificationsEnabledCheck.IsChecked = _settingsService.AreNotificationsEnabled;
            SnoozeDurationCombo.SelectedIndex = 0;
            SoundFileName.Text = _settingsService.CustomSoundDisplayName;
            UpdateValues();
        }

        private static void SelectComboItem(ComboBox combo, string value)
        {
            combo.SelectedItem = combo.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSelections();
            _settingsService.Save();
            _changesSaved = true;
            Close();
        }

        private void SaveCurrentSelections()
        {
            if (FontSizeCombo.SelectedItem is ComboBoxItem fontItem)
            {
                _settingsService.Settings.FontSize = fontItem.Content?.ToString() ?? "Mediano";
            }

            if (AppearanceCombo.SelectedItem is ComboBoxItem appearanceItem)
            {
                _settingsService.Settings.Appearance = appearanceItem.Content?.ToString() ?? "Claro";
            }

            _settingsService.Settings.NotificationVolume = (int)VolumeSlider.Value;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("¿Quieres restaurar todas las preferencias a sus valores predeterminados?",
                    "Restaurar configuración", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            _settingsService.Reset();
            _initializing = true;
            LoadSettings();
            _initializing = false;
            UpdateNotificationStatus();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initializing)
            {
                SaveCurrentSelections();
                _settingsService.Save();
            }
        }

        private void AppearanceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initializing)
            {
                SaveCurrentSelections();
                _settingsService.Save();
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateValues();
            if (_previewPlayer != null)
            {
                _previewPlayer.Volume = VolumeSlider.Value / 100d;
            }

            if (!_initializing)
            {
                SaveCurrentSelections();
                _settingsService.Save();
            }
        }

        private void UpdateValues()
        {
            if (VolumeValue != null)
            {
                VolumeValue.Text = $"{(int)VolumeSlider.Value}%";
            }
        }

        private void AccentColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: string color })
            {
                return;
            }

            _settingsService.Settings.AccentColor = color;
            _settingsService.Save();
        }

        private void SoundsEnabledCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing)
            {
                return;
            }

            _settingsService.Settings.SoundsEnabled = SoundsEnabledCheck.IsChecked == true;
            VolumeSlider.IsEnabled = _settingsService.Settings.SoundsEnabled;
            _settingsService.Save();
        }

        private void LoadSound_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar sonido de notificación",
                Filter = "Audio WAV (*.wav)|*.wav",
                CheckFileExists = true,
                Multiselect = false
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                _settingsService.SetCustomSound(dialog.FileName);
                SoundFileName.Text = _settingsService.CustomSoundDisplayName;
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or InvalidOperationException)
            {
                MessageBox.Show(exception.Message, "Archivo no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PreviewSound_Click(object sender, RoutedEventArgs e)
        {
            if (!_settingsService.Settings.SoundsEnabled)
            {
                return;
            }

            var soundPath = _settingsService.Settings.CustomSoundPath;
            if (string.IsNullOrWhiteSpace(soundPath) || !File.Exists(soundPath))
            {
                soundPath = Path.Combine(AppContext.BaseDirectory, "Resources", "reminder-alert.wav");
            }

            if (!File.Exists(soundPath))
            {
                MessageBox.Show("No se encontró el sonido predeterminado.", "Vista previa", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _previewPlayer?.Close();
            _previewPlayer = new MediaPlayer { Volume = VolumeSlider.Value / 100d };
            _previewPlayer.Open(new Uri(soundPath));
            _previewPlayer.Play();
        }

        private void RemoveSound_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.RemoveCustomSound();
            SoundFileName.Text = _settingsService.CustomSoundDisplayName;
        }

        private void NotificationsEnabledCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_initializing)
            {
                return;
            }

            var enabled = NotificationsEnabledCheck.IsChecked == true;
            _settingsService.Settings.NotificationsEnabled = enabled;
            _settingsService.Settings.NotificationsDisabledUntil = enabled
                ? null
                : DateTime.Now.AddMinutes(GetSelectedSnoozeMinutes());
            SnoozeCard.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            _settingsService.Save();
            UpdateNotificationStatus();
        }

        private void SnoozeDurationCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_initializing || NotificationsEnabledCheck.IsChecked == true)
            {
                return;
            }

            _settingsService.Settings.NotificationsDisabledUntil = DateTime.Now.AddMinutes(GetSelectedSnoozeMinutes());
            _settingsService.Save();
            UpdateNotificationStatus();
        }

        private int GetSelectedSnoozeMinutes()
        {
            return SnoozeDurationCombo.SelectedItem is ComboBoxItem { Tag: string minutes } &&
                   int.TryParse(minutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 15;
        }

        private void UpdateNotificationStatus()
        {
            if (NotificationStatus == null)
            {
                return;
            }

            var settings = _settingsService.Settings;
            if (_settingsService.AreNotificationsEnabled)
            {
                NotificationsEnabledCheck.IsChecked = true;
                SnoozeCard.Visibility = Visibility.Collapsed;
                NotificationStatus.Text = "Notificaciones activadas";
                NotificationStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.BellCheckOutline;
                NotificationStatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                return;
            }

            NotificationsEnabledCheck.IsChecked = false;
            SnoozeCard.Visibility = Visibility.Visible;
            var until = settings.NotificationsDisabledUntil!.Value;
            var remaining = until - DateTime.Now;
            var remainingText = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours} h {remaining.Minutes} min"
                : $"{Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes))} min";
            NotificationStatus.Text = $"Notificaciones desactivadas hasta las {until.ToString("t", CultureInfo.GetCultureInfo("es-ES"))} · Quedan {remainingText}.";
            NotificationStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.BellOffOutline;
            NotificationStatusIcon.Foreground = (System.Windows.Media.Brush)FindResource("DangerBrush");
        }

        private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            var requiresPassword = !string.IsNullOrWhiteSpace(_currentProfile.PasswordHash);
            var confirmation = new DeleteProfileWindow(_currentProfile.Name ?? "este perfil", requiresPassword)
            {
                Owner = this
            };

            if (confirmation.ShowDialog() != true)
            {
                return;
            }

            if (requiresPassword && !SecurityHelper.VerifyPassword(confirmation.EnteredPassword, _currentProfile.PasswordHash!))
            {
                MessageBox.Show("La contraseña no es correcta. El perfil no se ha eliminado.", "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                await _profileService.DeleteProfileAsync(_currentProfile);
                _profileService.Logout();
                _mainViewModel.NavigateTo(App.ServiceProvider.GetRequiredService<ProfileSelectionViewModel>());
                _changesSaved = true;
                Close();
            }
            catch (Exception exception)
            {
                MessageBox.Show($"No se pudo completar la eliminación del perfil: {exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(e.Source, SettingsTabs))
            {
                var selected = SettingsTabs.SelectedItem as TabItem;
                if (selected?.Header?.ToString() == "Zona de peligro")
                {
                    return;
                }
            }
        }

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
                _statusTimer.Stop();
                _previewPlayer?.Close();
                return;
            }

            if (!_changesSaved && !_settingsRestored)
            {
                _settingsService.Restore(_originalSettings, _originalCustomSound);
                _settingsRestored = true;
            }

            e.Cancel = true;
            _allowClose = true;
            var animation = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(140));
            animation.Completed += (_, _) => Close();
            BeginAnimation(OpacityProperty, animation);
        }
    }
}