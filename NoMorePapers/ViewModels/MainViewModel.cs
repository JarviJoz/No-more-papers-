using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using NoMorePapers.Services;
using System.Threading.Tasks;

namespace NoMorePapers.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private ToastNotificationActivation? _pendingToastActivation;

        [ObservableProperty]
        private ObservableObject? _currentViewModel;

        public MainViewModel()
        {
        }

        public void NavigateTo(ObservableObject viewModel)
        {
            CurrentViewModel = viewModel;
        }

        public async Task HandleToastActivationAsync(ToastNotificationActivation activation)
        {
            var profileService = App.ServiceProvider.GetRequiredService<IProfileService>();
            if (profileService.CurrentProfile?.Id != activation.ProfileId)
            {
                _pendingToastActivation = activation;
                NavigateTo(App.ServiceProvider.GetRequiredService<ProfileSelectionViewModel>());
                return;
            }

            await OpenToastSourceAsync(activation);
        }

        public async Task CompletePendingToastActivationAsync(WorkspaceViewModel workspace)
        {
            if (_pendingToastActivation is { } activation && activation.ProfileId == workspace.CurrentProfile.Id)
            {
                _pendingToastActivation = null;
                NavigateTo(workspace);
                await workspace.OpenToastSourceAsync(activation.SourceType, activation.SourceId);
                return;
            }

            NavigateTo(workspace);
        }

        private async Task OpenToastSourceAsync(ToastNotificationActivation activation)
        {
            var workspace = CurrentViewModel as WorkspaceViewModel;
            if (workspace?.CurrentProfile.Id != activation.ProfileId)
            {
                workspace = App.ServiceProvider.GetRequiredService<WorkspaceViewModel>();
            }

            NavigateTo(workspace);
            await workspace.OpenToastSourceAsync(activation.SourceType, activation.SourceId);
        }
    }
}