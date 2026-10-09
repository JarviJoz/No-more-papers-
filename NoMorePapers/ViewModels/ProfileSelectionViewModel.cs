using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoMorePapers.Helpers;
using NoMorePapers.Models;
using NoMorePapers.Services;
using NoMorePapers.Views;
using System.Collections.ObjectModel;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace NoMorePapers.ViewModels
{
    public partial class ProfileSelectionViewModel : ObservableObject
    {
        private readonly IProfileService _profileService;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty]
        private ObservableCollection<UserProfile> _profiles = new();

        [ObservableProperty]
        private UserProfile? _selectedProfile;

        [ObservableProperty]
        private string _passwordInput = string.Empty;

        [ObservableProperty]
        private bool _isPasswordVisible;

        [ObservableProperty]
        private bool _isRecoveryOpen;

        [ObservableProperty]
        private DateTime? _recoveryBirthDate;

        [ObservableProperty]
        private string _newPassword = string.Empty;

        public ProfileSelectionViewModel(IProfileService profileService, MainViewModel mainViewModel)
        {
            _profileService = profileService;
            _mainViewModel = mainViewModel;
            _ = LoadProfilesAsync();
        }

        private async Task LoadProfilesAsync()
        {
            var profilesList = await _profileService.GetAllProfilesAsync();
            foreach (var profile in profilesList)
            {
                profile.IsMenuOpen = false;
            }

            Profiles = new ObservableCollection<UserProfile>(profilesList);
        }

        [RelayCommand]
        private void SelectProfile(UserProfile profile)
        {
            if (profile == null) return;

            if (!profile.IsConfigured)
            {
                // Ir a configurar perfil
                var configureVM = App.ServiceProvider.GetService(typeof(ConfigureProfileViewModel)) as ConfigureProfileViewModel;
                if (configureVM != null)
                {
                    configureVM.SetSlot(profile.SlotNumber);
                    _mainViewModel.NavigateTo(configureVM);
                }

                return;
            }

            if (SelectedProfile == profile)
            {
                profile.IsMenuOpen = false;
                SelectedProfile = null;
                ClearCredentials();
                return;
            }

            foreach (var item in Profiles)
            {
                item.IsMenuOpen = false;
            }

            profile.IsMenuOpen = true;
            SelectedProfile = profile;
            ClearCredentials();
        }

        [RelayCommand]
        private void TogglePasswordVisibility()
        {
            IsPasswordVisible = !IsPasswordVisible;
        }

        [RelayCommand]
        private void ToggleRecovery()
        {
            IsRecoveryOpen = !IsRecoveryOpen;
            RecoveryBirthDate = null;
            NewPassword = string.Empty;
        }

        [RelayCommand]
        private async Task ResetPasswordAsync()
        {
            if (SelectedProfile == null || RecoveryBirthDate == null)
            {
                MessageBox.Show("Introduce la fecha de nacimiento registrada.", "Recuperar contraseña", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                MessageBox.Show("Introduce una nueva contraseña.", "Recuperar contraseña", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var reset = await _profileService.ResetPasswordAsync(SelectedProfile, RecoveryBirthDate.Value, NewPassword);
            if (!reset)
            {
                MessageBox.Show("La fecha de nacimiento no coincide.", "Recuperar contraseña", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            PasswordInput = NewPassword;
            IsRecoveryOpen = false;
            NewPassword = string.Empty;
            MessageBox.Show("Contraseña actualizada. Ya puedes acceder al perfil.", "Recuperar contraseña", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private async Task AccessProfileAsync(UserProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            SelectedProfile = profile;

            if (!_profileService.Login(profile, PasswordInput))
            {
                MessageBox.Show("La contraseña no es correcta.", "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var workspaceVM = App.ServiceProvider.GetService(typeof(WorkspaceViewModel)) as WorkspaceViewModel;
            if (workspaceVM != null)
            {
                await _mainViewModel.CompletePendingToastActivationAsync(workspaceVM);
            }
        }

        [RelayCommand]
        private async Task DeleteProfileAsync(UserProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            SelectedProfile = profile;

            var requiresPassword = !string.IsNullOrWhiteSpace(profile.PasswordHash);
            var confirmation = new DeleteProfileWindow(profile.Name ?? "este perfil", requiresPassword)
            {
                Owner = Application.Current.MainWindow
            };

            if (confirmation.ShowDialog() != true)
            {
                return;
            }

            if (requiresPassword && !SecurityHelper.VerifyPassword(confirmation.EnteredPassword, profile.PasswordHash!))
            {
                MessageBox.Show("La contraseña no es correcta. El perfil no se ha eliminado.", "Acceso denegado", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await _profileService.DeleteProfileAsync(profile);
            await LoadProfilesAsync();
            SelectedProfile = null;
            ClearCredentials();
        }

        private void ClearCredentials()
        {
            PasswordInput = string.Empty;
            IsPasswordVisible = false;
            IsRecoveryOpen = false;
            RecoveryBirthDate = null;
            NewPassword = string.Empty;
        }
    }
}