using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoMorePapers.Repositories;
using NoMorePapers.Services;
using System.Linq;
using System.Threading.Tasks;

namespace NoMorePapers.ViewModels
{
    public partial class StatisticsViewModel : ObservableObject
    {
        private readonly ICaseRepository _caseRepository;
        private readonly IProfileService _profileService;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty] private int _totalCases;
        [ObservableProperty] private int _activeCasesCount;
        [ObservableProperty] private int _finishedCasesCount;
        [ObservableProperty] private int _criticalCasesCount;

        // Categorías
        [ObservableProperty] private int _emocionalCount;
        [ObservableProperty] private int _academicoCount;
        [ObservableProperty] private int _familiarCount;
        [ObservableProperty] private int _conductualCount;

        public StatisticsViewModel(ICaseRepository caseRepository, IProfileService profileService, MainViewModel mainViewModel)
        {
            _caseRepository = caseRepository;
            _profileService = profileService;
            _mainViewModel = mainViewModel;

            _ = LoadStatisticsAsync();
        }

        private async Task LoadStatisticsAsync()
        {
            var profileId = _profileService.CurrentProfile!.Id;
            var cases = await _caseRepository.GetActiveCasesByProfileAsync(profileId);

            // Si también queremos contar los archivados como terminados, podríamos sumarlos
            var archivedCases = await _caseRepository.GetArchivedCasesByProfileAsync(profileId);
            var allValidCases = cases.Concat(archivedCases).ToList();

            TotalCases = allValidCases.Count;
            ActiveCasesCount = cases.Count(c => c.Status != "Finalizado" && c.Status != "TERMINADO");
            FinishedCasesCount = allValidCases.Count(c => c.Status == "Finalizado" || c.Status == "TERMINADO");

            // Consideraremos críticos los que tienen más de 30 días sin actualizar
            var thirtyDaysAgo = System.DateTime.Now.AddDays(-30);
            CriticalCasesCount = cases.Count(c => c.Status != "Finalizado" && c.Status != "TERMINADO" && c.UpdatedAt < thirtyDaysAgo);

            // Conteo básico por categorías de texto
            EmocionalCount = allValidCases.Count(c => c.Categories.Contains("Emocional"));
            AcademicoCount = allValidCases.Count(c => c.Categories.Contains("Académico"));
            FamiliarCount = allValidCases.Count(c => c.Categories.Contains("Familiar"));
            ConductualCount = allValidCases.Count(c => c.Categories.Contains("Conductual"));
        }

        [RelayCommand]
        private void GoBack()
        {
            var workspaceVM = App.ServiceProvider.GetService(typeof(WorkspaceViewModel)) as WorkspaceViewModel;
            _mainViewModel.NavigateTo(workspaceVM!);
        }
    }
}