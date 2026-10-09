using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoMorePapers.Services;
using System;
using System.Windows;

namespace NoMorePapers.ViewModels
{
    public partial class ConfigureProfileViewModel : ObservableObject
    {
        private readonly IProfileService _profileService;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private int _slotNumber;
        [ObservableProperty] private string _profileName = string.Empty;
        [ObservableProperty] private DateTime _birthDate = DateTime.Now;
        [ObservableProperty] private string _password = string.Empty;
        private string _confirmPassword = string.Empty;

        public string ConfirmPassword
        {
            get => _confirmPassword;
            set => SetProperty(ref _confirmPassword, value);
        }

        public ConfigureProfileViewModel(IProfileService profileService, MainViewModel mainViewModel)
        {
            _profileService = profileService;
            _mainViewModel = mainViewModel;
        }

        // Este es el método que pedía el error
        public void SetSlot(int slot)
        {
            SlotNumber = slot;
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
            {
                MessageBox.Show("El nombre es obligatorio.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                MessageBox.Show("Introduce la contraseña de acceso que deseas confirmar.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(Password) && string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                MessageBox.Show("Confirma la contraseña.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!string.IsNullOrWhiteSpace(Password) && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
            {
                MessageBox.Show("Las contraseñas no coinciden.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            await _profileService.ConfigureProfileAsync(SlotNumber, ProfileName, BirthDate, Password, null);

            var selectionVM = App.ServiceProvider.GetService(typeof(ProfileSelectionViewModel)) as ProfileSelectionViewModel;
            _mainViewModel.NavigateTo(selectionVM!);
        }

        [RelayCommand]
        private void Cancel()
        {
            var selectionVM = App.ServiceProvider.GetService(typeof(ProfileSelectionViewModel)) as ProfileSelectionViewModel;
            _mainViewModel.NavigateTo(selectionVM!);
        }
    }
}