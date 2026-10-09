using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NoMorePapers.Models;
using NoMorePapers.Repositories;
using NoMorePapers.Services;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace NoMorePapers.ViewModels
{
    public partial class StudentProfileViewModel : ObservableObject
    {
        private readonly ICaseRepository _caseRepository;
        private readonly IProfileService _profileService;
        private readonly MainViewModel _mainViewModel;
        private readonly StudentDocumentStorageService _documentStorage = new();
        private static readonly HashSet<string> SupportedDocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".docx", ".xlsx", ".pptx"
        };

        public Student Student { get; }
        public ObservableCollection<StudentCase> Cases { get; } = new();
        public ObservableCollection<Student> Siblings { get; } = new();
        public ObservableCollection<SiblingSearchEntry> SiblingSearches { get; } = new();
        public ObservableCollection<StudentDocument> StudentDocuments { get; } = new();
        public IReadOnlyList<string> StatusOptions { get; } = new[] { "Activo", "Retirado", "Egresado", "Trasladado", "Otro" };

        [ObservableProperty] private bool _isEditing;
        [ObservableProperty] private StudentCase? _selectedCase;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _grade = string.Empty;
        [ObservableProperty] private string _group = string.Empty;
        [ObservableProperty] private string _guardian1Name = string.Empty;
        [ObservableProperty] private string _guardian1Phone = string.Empty;
        [ObservableProperty] private string _guardian1Relation = string.Empty;
        [ObservableProperty] private string _guardian2Name = string.Empty;
        [ObservableProperty] private string _guardian2Phone = string.Empty;
        [ObservableProperty] private string _guardian2Relation = string.Empty;
        [ObservableProperty] private string _status = "Activo";
        [ObservableProperty] private System.DateTime? _birthDate;

        public int? CurrentAge => BirthDate.HasValue ? Student.CalculateAge(BirthDate.Value, System.DateTime.Today) : null;

        public StudentProfileViewModel(Student student, ICaseRepository caseRepository, IProfileService profileService, MainViewModel mainViewModel)
        {
            Student = student;
            _caseRepository = caseRepository;
            _profileService = profileService;
            _mainViewModel = mainViewModel;
            LoadStudentFields();
            _ = LoadHistoryAsync();
            _ = LoadSiblingsAsync();
            _ = RefreshStudentDocumentsAsync();
        }

        private async Task RefreshStudentDocumentsAsync()
        {
            StudentDocuments.Clear();
            foreach (var document in await _caseRepository.GetStudentDocumentsAsync(Student.Id))
            {
                StudentDocuments.Add(document);
            }
        }

        private async Task LoadSiblingsAsync()
        {
            Siblings.Clear();
            foreach (var sibling in await _caseRepository.GetSiblingsAsync(Student.Id))
            {
                Siblings.Add(sibling);
            }
        }

        private void LoadStudentFields()
        {
            Name = Student.Name;
            BirthDate = Student.BirthDate;
            Grade = Student.Grade;
            Group = Student.Group;
            Guardian1Name = Student.Guardian1Name ?? string.Empty;
            Guardian1Phone = Student.Guardian1Phone ?? string.Empty;
            Guardian1Relation = Student.Guardian1Relation;
            Guardian2Name = Student.Guardian2Name ?? string.Empty;
            Guardian2Phone = Student.Guardian2Phone ?? string.Empty;
            Guardian2Relation = Student.Guardian2Relation;
            Status = Student.Status;
        }

        private async Task LoadHistoryAsync()
        {
            Cases.Clear();
            foreach (var studentCase in await _caseRepository.GetCasesByStudentAsync(Student.Id))
            {
                Cases.Add(studentCase);
            }
        }

        partial void OnBirthDateChanged(System.DateTime? value) => OnPropertyChanged(nameof(CurrentAge));

        [RelayCommand]
        private void BackToStudents()
        {
            _mainViewModel.NavigateTo(App.ServiceProvider.GetRequiredService<StudentsViewModel>());
        }

        [RelayCommand]
        private void EditProfile() => IsEditing = true;

        [RelayCommand]
        private async Task SaveProfileAsync()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                MessageBox.Show("El nombre del estudiante es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Student.Name = Name.Trim();
            Student.BirthDate = BirthDate;
            Student.Grade = Grade;
            Student.Group = Group;
            Student.Guardian1Name = Guardian1Name;
            Student.Guardian1Phone = Guardian1Phone;
            Student.Guardian1Relation = Guardian1Relation;
            Student.Guardian2Name = Guardian2Name;
            Student.Guardian2Phone = Guardian2Phone;
            Student.Guardian2Relation = Guardian2Relation;
            Student.Status = Status;
            await _caseRepository.UpdateStudentAsync(Student);
            IsEditing = false;
            OnPropertyChanged(nameof(CurrentAge));
            OnPropertyChanged(nameof(Student));
            MessageBox.Show("Perfil guardado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void AddSiblingSearch()
        {
            var entry = new SiblingSearchEntry();
            entry.SearchTextChanged += OnSiblingSearchTextChanged;
            SiblingSearches.Add(entry);
        }

        private async void OnSiblingSearchTextChanged(object? sender, EventArgs e)
        {
            if (sender is not SiblingSearchEntry entry)
            {
                return;
            }

            var query = entry.SearchText;
            entry.SetSuggestions(Array.Empty<Student>());
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
            {
                return;
            }

            await Task.Delay(250);
            if (query != entry.SearchText)
            {
                return;
            }

            var matches = await _caseRepository.FindStudentsByNameAsync(Student.ProfileId, query, Student.Id);
            if (query == entry.SearchText)
            {
                entry.SetSuggestions(matches);
            }
        }

        [RelayCommand]
        private async Task AddSiblingAsync(SiblingSearchEntry? entry)
        {
            var sibling = entry?.SelectedStudent;
            if (entry == null || sibling == null)
            {
                return;
            }

            if (sibling.Id == Student.Id)
            {
                MessageBox.Show("No es posible registrar al estudiante actual como su propio hermano.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Siblings.Any(existing => existing.Id == sibling.Id))
            {
                MessageBox.Show("Este estudiante ya está registrado como hermano.", "Relación existente", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            await _caseRepository.AddSiblingRelationAsync(Student.Id, sibling.Id);
            SiblingSearches.Remove(entry);
            await LoadSiblingsAsync();
        }

        [RelayCommand]
        private async Task LoadStudentDocumentsAsync()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Cargar documentos del estudiante",
                Filter = "Documentos compatibles|*.pdf;*.docx;*.xlsx;*.pptx|Todos los archivos|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            foreach (var sourcePath in dialog.FileNames)
            {
                await ImportStudentDocumentAsync(sourcePath);
            }
        }

        private async Task ImportStudentDocumentAsync(string sourcePath)
        {
            var extension = Path.GetExtension(sourcePath);
            if (!SupportedDocumentExtensions.Contains(extension))
            {
                MessageBox.Show("Este tipo de archivo no es compatible.", "Formato no permitido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var sourceInfo = new FileInfo(sourcePath);
                if (sourceInfo.Length > StudentDocumentStorageService.LargeFileWarningThresholdBytes)
                {
                    var continueLoading = MessageBox.Show(
                        $"El archivo seleccionado es demasiado grande ({sourceInfo.Length / 1024d / 1024d:N1} MB). ¿Desea continuar?",
                        "Archivo grande",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (continueLoading != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                var contentHash = await _documentStorage.CalculateContentHashAsync(sourcePath);
                if (StudentDocuments.Any(document =>
                        !string.IsNullOrWhiteSpace(document.ContentHash) &&
                        string.Equals(document.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase)))
                {
                    var loadAgain = MessageBox.Show(
                        "Este documento ya está asociado a este estudiante. ¿Desea cargarlo de todos modos?",
                        "Documento duplicado",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (loadAgain != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                var storedDocument = await _documentStorage.StoreCopyAsync(Student.Id, sourcePath, contentHash);
                try
                {
                    await _caseRepository.AddStudentDocumentAsync(storedDocument);
                }
                catch
                {
                    await _documentStorage.DeleteStoredCopyAsync(Student.Id, storedDocument.FilePath);
                    throw;
                }

                StudentDocuments.Insert(0, storedDocument);
            }
            catch (Exception)
            {
                MessageBox.Show("No se pudo cargar el documento. Inténtelo nuevamente.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task RemoveStudentDocumentAsync(StudentDocument? document)
        {
            if (document == null)
            {
                return;
            }

            var confirmation = MessageBox.Show(
                $"¿Está seguro de que desea eliminar este documento?\n\n{document.FileName}\n\nEsta acción eliminará el documento de los documentos asociados al estudiante.",
                "Eliminar documento",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            var deletedDocument = await _caseRepository.DeleteStudentDocumentAsync(Student.Id, document.Id);
            if (deletedDocument == null)
            {
                return;
            }

            StudentDocuments.Remove(document);
            try
            {
                await _documentStorage.DeleteStoredCopyAsync(Student.Id, deletedDocument.FilePath);
            }
            catch (Exception)
            {
                MessageBox.Show("El documento se quitó del perfil, pero no se pudo eliminar su copia administrada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        [RelayCommand]
        private void PreviewStudentDocument(StudentDocument? document)
        {
            if (document == null)
            {
                return;
            }

            if (!File.Exists(document.FilePath))
            {
                MessageBox.Show("No se puede previsualizar este documento porque el archivo almacenado no está disponible.", "Documento no disponible", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var previewWindow = new Views.StudentDocumentPreviewWindow(document)
            {
                Owner = Application.Current.MainWindow
            };
            previewWindow.ShowDialog();
        }

        [RelayCommand]
        private async Task RemoveSiblingAsync(Student? sibling)
        {
            if (sibling == null)
            {
                return;
            }

            var result = MessageBox.Show(
                $"¿Desea eliminar la relación de hermano con {sibling.Name}?",
                "Eliminar relación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            await _caseRepository.RemoveSiblingRelationAsync(Student.Id, sibling.Id);
            await LoadSiblingsAsync();
        }

        [RelayCommand]
        private async Task CreateCaseAsync()
        {
            var editor = App.ServiceProvider.GetRequiredService<AddEditCaseViewModel>();
            editor.LoadStudent(Student);
            var window = new Views.AddEditCaseWindow { DataContext = editor };
            editor.CloseAction = () => window.Close();
            window.ShowDialog();
            await LoadHistoryAsync();
            await RefreshStudentGradeAsync();
        }

        [RelayCommand]
        private async Task OpenSelectedCaseAsync()
        {
            if (SelectedCase == null) return;
            var editor = App.ServiceProvider.GetRequiredService<AddEditCaseViewModel>();
            await editor.LoadCaseAsync(SelectedCase);
            var window = new Views.AddEditCaseWindow { DataContext = editor };
            editor.CloseAction = () => window.Close();
            window.ShowDialog();
            await LoadHistoryAsync();
            await RefreshStudentGradeAsync();
        }

        private async Task RefreshStudentGradeAsync()
        {
            var updatedStudent = await _caseRepository.GetStudentByIdAsync(Student.Id);
            if (updatedStudent == null)
            {
                return;
            }

            Student.Grade = updatedStudent.Grade;
            Student.Group = updatedStudent.Group;
            Grade = updatedStudent.Grade;
            Group = updatedStudent.Group;
            OnPropertyChanged(nameof(Student));
        }
    }
}
