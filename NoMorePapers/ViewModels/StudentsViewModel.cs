using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using NoMorePapers.Models;
using NoMorePapers.Repositories;
using NoMorePapers.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NoMorePapers.ViewModels
{
    public partial class StudentsViewModel : ObservableObject
    {
        private readonly ICaseRepository _caseRepository;
        private readonly IProfileService _profileService;
        private readonly MainViewModel _mainViewModel;
        private readonly StudentDocumentStorageService _documentStorage = new();
        private List<Student> _allStudents = new();
        private string _searchText = string.Empty;

        public UserProfile CurrentProfile { get; }

        [ObservableProperty]
        private ObservableCollection<Student> _students = new();

        [ObservableProperty]
        private Student? _selectedStudent;

        public bool HasSelectedStudent => SelectedStudent != null;
        public bool HasSelectedStudents => _allStudents.Any(student => student.IsSelected);

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterStudents();
                }
            }
        }

        public StudentsViewModel(
            ICaseRepository caseRepository,
            IProfileService profileService,
            MainViewModel mainViewModel)
        {
            _caseRepository = caseRepository;
            _profileService = profileService;
            _mainViewModel = mainViewModel;
            CurrentProfile = _profileService.CurrentProfile!;
            _ = LoadStudentsAsync();
        }

        partial void OnSelectedStudentChanged(Student? value)
        {
            OnPropertyChanged(nameof(HasSelectedStudent));
        }

        [RelayCommand]
        private void BackToWorkspace()
        {
            _mainViewModel.NavigateTo(App.ServiceProvider.GetRequiredService<WorkspaceViewModel>());
        }

        [RelayCommand]
        private async Task ReloadStudentsAsync()
        {
            await LoadStudentsAsync();
        }

        [RelayCommand]
        private async Task DeleteSelectedStudentsAsync()
        {
            var selectedStudents = _allStudents.Where(student => student.IsSelected).ToList();
            if (selectedStudents.Count == 0)
            {
                return;
            }

            var selectedCount = selectedStudents.Count;
            var studentLabel = selectedCount == 1 ? "estudiante" : "estudiantes";
            var confirmation = MessageBox.Show(
                $"Al eliminar, los datos de {selectedCount} {studentLabel} se eliminarán de manera permanente, incluidos sus casos, seguimientos y documentos. ¿Deseas continuar?",
                "Eliminar estudiante",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var selectedIds = selectedStudents.Select(student => student.Id).ToList();
                var documents = await _caseRepository.DeleteStudentsPermanentlyAsync(selectedIds);
                foreach (var student in selectedStudents)
                {
                    student.PropertyChanged -= OnStudentPropertyChanged;
                    _allStudents.Remove(student);
                }

                if (SelectedStudent != null && selectedIds.Contains(SelectedStudent.Id))
                {
                    SelectedStudent = null;
                }
                FilterStudents();

                var filesCouldNotBeDeleted = false;
                foreach (var document in documents)
                {
                    try
                    {
                        await _documentStorage.DeleteStoredCopyAsync(document.StudentId, document.FilePath);
                    }
                    catch (Exception)
                    {
                        filesCouldNotBeDeleted = true;
                    }
                }

                if (filesCouldNotBeDeleted)
                {
                    MessageBox.Show(
                        "Los datos del estudiante se eliminaron, pero no se pudieron borrar algunas copias de documentos almacenadas en el equipo.",
                        "Aviso",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception exception)
            {
                MessageBox.Show(
                    $"No se pudo eliminar el estudiante: {exception.Message}",
                    "Error al eliminar",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void OpenStudentProfile()
        {
            if (SelectedStudent == null)
            {
                return;
            }

            _mainViewModel.NavigateTo(new StudentProfileViewModel(
                SelectedStudent,
                _caseRepository,
                _profileService,
                _mainViewModel));
        }

        private async Task LoadStudentsAsync()
        {
            foreach (var student in _allStudents)
            {
                student.PropertyChanged -= OnStudentPropertyChanged;
            }

            _allStudents = await _caseRepository.GetStudentsByProfileAsync(CurrentProfile.Id);
            foreach (var student in _allStudents)
            {
                student.PropertyChanged += OnStudentPropertyChanged;
            }

            FilterStudents();
        }

        private void OnStudentPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Student.IsSelected))
            {
                OnPropertyChanged(nameof(HasSelectedStudents));
            }
        }

        private void FilterStudents()
        {
            var query = SearchText.Trim();
            var filteredStudents = string.IsNullOrWhiteSpace(query)
                ? _allStudents
                : _allStudents.Where(student =>
                    student.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                    (student.CurrentAge?.ToString() ?? string.Empty).Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                    student.Grade.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                    student.Group.Contains(query, System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

            Students = new ObservableCollection<Student>(filteredStudents);
            OnPropertyChanged(nameof(HasSelectedStudents));
        }
    }
}
