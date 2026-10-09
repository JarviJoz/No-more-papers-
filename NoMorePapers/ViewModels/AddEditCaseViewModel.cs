using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoMorePapers.Models;
using NoMorePapers.Repositories;
using NoMorePapers.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace NoMorePapers.ViewModels
{
    public partial class AddEditCaseViewModel : ObservableObject
    {
        private readonly ICaseRepository _caseRepository;
        private readonly IProfileService _profileService;
        private readonly DateTime _caseOpenedAt;
        private StudentCase? _currentCase;
        private string? _studentId;
        private DateTime? _originalBirthDate;

        public Action? CloseAction { get; set; }
        public event Action<FollowUpViewModel>? FollowUpOpened;

        public IReadOnlyList<FollowUpViewModel> FollowUps { get; } =
            Enumerable.Range(1, 4).Select(number => new FollowUpViewModel(number)).ToList();

        public IReadOnlyList<string> StatusOptions { get; } =
            new[] { "En Proceso", "Pendiente", "Finalizado", "Cancelado" };

        public IReadOnlyList<string> NotificationIntervalOptions { get; } =
            new[] { "Sin notificaciones", "3 días", "7 días", "15 días", "30 días" };

        public IReadOnlyList<string> ShiftOptions { get; } =
            new[] { "Mañana", "Tarde" };

        [ObservableProperty]
        private FollowUpViewModel? _activeFollowUp;

        [ObservableProperty]
        private bool _isFollowUpOpen;

        [ObservableProperty]
        private string _selectedStatus = "En Proceso";

        [ObservableProperty]
        private bool _isNotificationEnabled = true;

        [ObservableProperty]
        private string _selectedNotificationInterval = "15 días";

        partial void OnSelectedNotificationIntervalChanged(string value)
        {
            if (value == "Sin notificaciones")
            {
                IsNotificationEnabled = false;
            }
        }

        // --- Propiedades del Estudiante ---
        [ObservableProperty] private string _studentName = string.Empty;
        [ObservableProperty] private int _age;
        [ObservableProperty] private string _grade = string.Empty;
        [ObservableProperty] private string _group = string.Empty;
        [ObservableProperty] private string _shift = string.Empty;
        [ObservableProperty] private DateTime? _birthDate;

        partial void OnBirthDateChanged(DateTime? value)
        {
            if (value.HasValue)
            {
                Age = Student.CalculateAge(value.Value, DateTime.Today);
            }
        }

        // --- Propiedades del Acudiente ---
        [ObservableProperty] private string _guardianName = string.Empty;
        [ObservableProperty] private string _guardianPhone = string.Empty;

        // RadioButtons para Parentesco
        [ObservableProperty] private bool _isPadre;
        [ObservableProperty] private bool _isMadre;
        [ObservableProperty] private bool _isOtro;
        [ObservableProperty] private string _parentescoOtroText = string.Empty;

        // --- Motivos de Remisión (Checkboxes) ---
        [ObservableProperty] private bool _isEmocional;
        [ObservableProperty] private bool _isFamiliar;
        [ObservableProperty] private bool _isAcademico;
        [ObservableProperty] private bool _isPsicologico;
        [ObservableProperty] private bool _isFisico;
        [ObservableProperty] private bool _isConductual;

        // --- Textos Grandes ---
        [ObservableProperty] private string _reason = string.Empty; // Texto del Seguimiento #1
        [ObservableProperty] private string _observations = string.Empty; // Cuadro izquierdo de observaciones

        // --- Fechas Visuales ---
        // --- Fechas Visuales ---
        [ObservableProperty] private string _createdAtString = string.Empty;
        [ObservableProperty] private string _updatedAtString = string.Empty;
        [ObservableProperty] private Brush _agingBrush = Brushes.LightSkyBlue;
        [ObservableProperty] private string _agingTooltip = "Actualizado recientemente";
        [ObservableProperty] private string _formTitle = "CREAR CASO";
        private string _avatarType = "DefaultMale";
        public string AvatarType
        {
            get => _avatarType;
            set => SetProperty(ref _avatarType, value);
        }

        private byte[]? _avatarImage;
        public byte[]? AvatarImage
        {
            get => _avatarImage;
            set => SetProperty(ref _avatarImage, value);
        }

        partial void OnSelectedStatusChanged(string value) => RefreshAgingIndicator();

        public AddEditCaseViewModel(ICaseRepository caseRepository, IProfileService profileService)
        {
            _caseRepository = caseRepository;
            _profileService = profileService;
            _caseOpenedAt = DateTime.Now;

            // Fijar la fecha actual al momento exacto de abrir la ventana
            CreatedAtString = _caseOpenedAt.ToString("dd/MM/yyyy");
            UpdatedAtString = _caseOpenedAt.ToString("dd/MM/yyyy");
        }

        public async Task LoadCaseAsync(StudentCase studentCase)
        {
            _currentCase = studentCase;
            _studentId = studentCase.StudentId;
            FormTitle = "EDITAR CASO";
            StudentName = studentCase.StudentName;
            Age = studentCase.Age;
            var student = string.IsNullOrWhiteSpace(_studentId)
                ? null
                : await _caseRepository.GetStudentByIdAsync(_studentId);
            BirthDate = student?.BirthDate;
            _originalBirthDate = BirthDate;
            Grade = studentCase.Grade;
            Group = studentCase.Group;
            Shift = studentCase.Shift;
            AvatarType = studentCase.AvatarType;
            AvatarImage = studentCase.AvatarImage;
            GuardianName = studentCase.GuardianName ?? string.Empty;
            GuardianPhone = studentCase.GuardianPhone ?? string.Empty;
            IsPadre = studentCase.GuardianRelation == "Padre";
            IsMadre = studentCase.GuardianRelation == "Madre";
            IsOtro = !IsPadre && !IsMadre && !string.IsNullOrWhiteSpace(studentCase.GuardianRelation);
            ParentescoOtroText = IsOtro ? studentCase.GuardianRelation : string.Empty;
            IsEmocional = studentCase.Categories.Contains("Emocional", StringComparison.OrdinalIgnoreCase);
            IsFamiliar = studentCase.Categories.Contains("Familiar", StringComparison.OrdinalIgnoreCase);
            IsAcademico = studentCase.Categories.Contains("Académico", StringComparison.OrdinalIgnoreCase);
            IsPsicologico = studentCase.Categories.Contains("Psicológico", StringComparison.OrdinalIgnoreCase);
            IsFisico = studentCase.Categories.Contains("Físico", StringComparison.OrdinalIgnoreCase);
            IsConductual = studentCase.Categories.Contains("Conductual", StringComparison.OrdinalIgnoreCase);
            Reason = studentCase.Reason;
            Observations = studentCase.Observations;
            SelectedStatus = studentCase.Status;
            IsNotificationEnabled = studentCase.NotificationsEnabled;
            SelectedNotificationInterval = studentCase.NotificationIntervalDays switch
            {
                3 => "3 días",
                7 => "7 días",
                30 => "30 días",
                _ when !studentCase.NotificationsEnabled => "Sin notificaciones",
                _ => "15 días"
            };
            CreatedAtString = studentCase.CreatedAt.ToString("dd/MM/yyyy");
            UpdatedAtString = studentCase.UpdatedAt.ToString("dd/MM/yyyy");
            RefreshAgingIndicator();
        }

        public void LoadStudent(Student student)
        {
            _studentId = student.Id;
            StudentName = student.Name;
            BirthDate = student.BirthDate;
            _originalBirthDate = BirthDate;
            Age = student.CurrentAge ?? 0;
            Grade = student.Grade;
            Group = student.Group;
            Shift = student.Shift;
            AvatarType = student.AvatarType;
            AvatarImage = student.AvatarImage;
            GuardianName = student.Guardian1Name ?? string.Empty;
            GuardianPhone = student.Guardian1Phone ?? string.Empty;
            IsPadre = student.Guardian1Relation == "Padre";
            IsMadre = student.Guardian1Relation == "Madre";
            IsOtro = !IsPadre && !IsMadre && !string.IsNullOrWhiteSpace(student.Guardian1Relation);
            ParentescoOtroText = IsOtro ? student.Guardian1Relation : string.Empty;
            FormTitle = "CREAR CASO";
        }

        private void RefreshAgingIndicator()
        {
            var preview = new StudentCase
            {
                UpdatedAt = _currentCase?.UpdatedAt ?? _caseOpenedAt,
                Status = SelectedStatus
            };
            var aging = CaseAgingService.Calculate(preview);
            AgingBrush = aging.Brush;
            AgingTooltip = aging.Description;
        }

        [RelayCommand]
        private async Task OpenFollowUpAsync(int number)
        {
            var followUp = FollowUps.Single(item => item.Number == number);
            foreach (var item in FollowUps)
            {
                item.IsOpen = item == followUp;
            }

            ActiveFollowUp = followUp;
            IsFollowUpOpen = true;

            if (_currentCase != null)
            {
                var savedFollowUp = (await _caseRepository.GetFollowUpsAsync(_currentCase.Id))
                    .SingleOrDefault(item => item.FollowUpNumber == number);

                if (savedFollowUp != null)
                {
                    followUp.ContentRtf = savedFollowUp.ContentRtf;
                    followUp.FollowUpDate = savedFollowUp.FollowUpDate;
                    followUp.SignatureImage = savedFollowUp.SignatureImage;
                    followUp.SavedAt = savedFollowUp.SavedAt;
                }
            }

            FollowUpOpened?.Invoke(followUp);
        }

        [RelayCommand]
        private void CloseFollowUp()
        {
            if (ActiveFollowUp != null)
            {
                ActiveFollowUp.IsOpen = false;
            }

            IsFollowUpOpen = false;
            ActiveFollowUp = null;
        }

        public async Task SaveActiveFollowUpAsync(string contentRtf)
        {
            if (ActiveFollowUp == null)
            {
                return;
            }

            await EnsureCaseSavedAsync(updateFields: false);

            ActiveFollowUp.ContentRtf = contentRtf;
            ActiveFollowUp.SavedAt = DateTime.Now;

            await _caseRepository.SaveFollowUpAsync(new CaseFollowUp
            {
                StudentCaseId = _currentCase!.Id,
                FollowUpNumber = ActiveFollowUp.Number,
                ContentRtf = ActiveFollowUp.ContentRtf,
                FollowUpDate = ActiveFollowUp.FollowUpDate,
                SignatureImage = ActiveFollowUp.SignatureImage,
                SavedAt = ActiveFollowUp.SavedAt.Value
            });

            UpdatedAtString = ActiveFollowUp.SavedAt.Value.ToString("dd/MM/yyyy");
            CloseFollowUp();
        }

        [RelayCommand]
        private async Task DeleteActiveFollowUpAsync()
        {
            if (ActiveFollowUp == null || _currentCase == null)
            {
                CloseFollowUp();
                return;
            }

            await _caseRepository.DeleteFollowUpAsync(_currentCase.Id, ActiveFollowUp.Number);
            ActiveFollowUp.ContentRtf = string.Empty;
            ActiveFollowUp.FollowUpDate = null;
            ActiveFollowUp.SignatureImage = null;
            ActiveFollowUp.SignatureFileName = string.Empty;
            ActiveFollowUp.SavedAt = null;
            CloseFollowUp();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(StudentName))
            {
                MessageBox.Show("El nombre del estudiante es obligatorio.", "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 1. Procesar el Parentesco
            string relation = string.Empty;
            if (IsPadre) relation = "Padre";
            else if (IsMadre) relation = "Madre";
            else if (IsOtro) relation = ParentescoOtroText;

            // 2. Procesar las Categorías (Motivos)
            var cats = new List<string>();
            if (IsEmocional) cats.Add("Emocional");
            if (IsFamiliar) cats.Add("Familiar");
            if (IsAcademico) cats.Add("Académico");
            if (IsPsicologico) cats.Add("Psicológico");
            if (IsFisico) cats.Add("Físico");
            if (IsConductual) cats.Add("Conductual");
            string finalCategories = string.Join(", ", cats);
            var hasChanges = _currentCase == null || HasCaseChanges(relation, finalCategories);
            var updatedAt = hasChanges ? DateTime.Now : _currentCase!.UpdatedAt;
            UpdatedAtString = updatedAt.ToString("dd/MM/yyyy");

            if (hasChanges)
            {
                await EnsureCaseSavedAsync(relation, finalCategories, updatedAt);
            }
            MessageBox.Show("Caso guardado exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            CloseAction?.Invoke();
        }

        private async Task EnsureCaseSavedAsync(string relation = "", string categories = "", DateTime? updatedAt = null, bool updateFields = true)
        {
            var timestamp = updatedAt ?? DateTime.Now;

            if (_currentCase == null)
        {
                var student = await ResolveStudentAsync();
                student.Name = StudentName.Trim();
                if (BirthDate.HasValue)
                {
                    student.BirthDate = BirthDate;
                }
                student.Grade = Grade;
                student.Group = Group;
                student.Shift = Shift;
                student.Guardian1Name = GuardianName;
                student.Guardian1Phone = GuardianPhone;
                student.Guardian1Relation = relation;
                student.AvatarType = AvatarType;
                student.AvatarImage = AvatarImage;
                await _caseRepository.UpdateStudentAsync(student);
                Age = student.CurrentAge ?? Age;
                _currentCase = new StudentCase
            {
                    Id = $"CASE-{DateTime.Now:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}"[..30],
                    ProfileId = _profileService.CurrentProfile!.Id,
                    StudentId = student.Id,
                    StudentName = StudentName,
                    Age = Age,
                    Grade = Grade,
                    Group = Group,
                    Shift = Shift,
                    AvatarType = AvatarType,
                    AvatarImage = AvatarImage,
                    GuardianName = GuardianName,
                    GuardianRelation = relation,
                    GuardianPhone = GuardianPhone,
                    Reason = Reason,
                    Categories = categories,
                    Observations = Observations,
                    Status = SelectedStatus,
                    NotificationsEnabled = IsNotificationEnabled,
                    NotificationIntervalDays = GetNotificationIntervalDays(),
                    CreatedAt = _caseOpenedAt,
                    UpdatedAt = timestamp
                };

                await _caseRepository.AddCaseAsync(_currentCase);
            }
            else
            {
                if (updateFields)
                {
                    var student = await ResolveStudentAsync();
                    student.BirthDate = BirthDate;
                    student.Grade = Grade;
                    student.Group = Group;
                    await _caseRepository.UpdateStudentAsync(student);

                    _currentCase.StudentName = StudentName;
                    _currentCase.Age = Age;
                    _currentCase.Grade = Grade;
                    _currentCase.Group = Group;
                    _currentCase.Shift = Shift;
                    _currentCase.AvatarType = AvatarType;
                    _currentCase.AvatarImage = AvatarImage;
                    _currentCase.GuardianName = GuardianName;
                    _currentCase.GuardianRelation = relation;
                    _currentCase.GuardianPhone = GuardianPhone;
                    _currentCase.Reason = Reason;
                    _currentCase.Categories = categories;
                    _currentCase.Observations = Observations;
                    if (SelectedStatus is "Finalizado" or "Cancelado" &&
                        _currentCase.Status is "En Proceso" or "Pendiente")
                    {
                        _currentCase.PreviousStatus = _currentCase.Status;
                    }
                    _currentCase.Status = SelectedStatus;
                    _currentCase.NotificationsEnabled = IsNotificationEnabled;
                    _currentCase.NotificationIntervalDays = GetNotificationIntervalDays();
                }
                _currentCase.UpdatedAt = timestamp;
                await _caseRepository.UpdateCaseAsync(_currentCase);
            }
        }

        private async Task<Student> ResolveStudentAsync()
        {
            if (!string.IsNullOrWhiteSpace(_studentId))
            {
                var existingStudent = await _caseRepository.GetStudentByIdAsync(_studentId);
                if (existingStudent != null)
                {
                    return existingStudent;
                }
            }

            var matches = await _caseRepository.FindStudentsByNameAsync(_profileService.CurrentProfile!.Id, StudentName);
            if (matches.Count > 0)
            {
                var match = matches[0];
                var caseCount = (await _caseRepository.GetCasesByStudentAsync(match.Id)).Count;
                var result = MessageBox.Show(
                    $"💡 Este estudiante ya tiene {caseCount} {(caseCount == 1 ? "caso registrado" : "casos registrados")} anteriormente, se registrará como caso nuevo.\n\n¿Es el mismo estudiante?",
                    "Estudiante existente", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    _studentId = match.Id;
                    return match;
                }
            }

            var newStudent = new Student
            {
                Id = $"STU-{Guid.NewGuid():N}",
                ProfileId = _profileService.CurrentProfile!.Id,
                Name = StudentName.Trim(),
                BirthDate = BirthDate,
                Grade = Grade,
                Group = Group,
                Shift = Shift,
                Guardian1Name = GuardianName,
                Guardian1Phone = GuardianPhone,
                Guardian1Relation = GetGuardianRelation(),
                AvatarType = AvatarType,
                AvatarImage = AvatarImage
            };
            await _caseRepository.AddStudentAsync(newStudent);
            _studentId = newStudent.Id;
            return newStudent;
        }

        private string GetGuardianRelation()
        {
            if (IsPadre) return "Padre";
            if (IsMadre) return "Madre";
            return IsOtro ? ParentescoOtroText : string.Empty;
        }

        private bool HasCaseChanges(string relation, string categories)
        {
            if (_currentCase == null)
            {
                return true;
            }

            return _currentCase.StudentName != StudentName ||
                   _currentCase.Age != Age ||
                   _originalBirthDate?.Date != BirthDate?.Date ||
                   _currentCase.Grade != Grade ||
                   _currentCase.Group != Group ||
                   _currentCase.Shift != Shift ||
                   _currentCase.AvatarType != AvatarType ||
                   !(_currentCase.AvatarImage ?? Array.Empty<byte>()).SequenceEqual(AvatarImage ?? Array.Empty<byte>()) ||
                   (_currentCase.GuardianName ?? string.Empty) != GuardianName ||
                   (_currentCase.GuardianPhone ?? string.Empty) != GuardianPhone ||
                   _currentCase.GuardianRelation != relation ||
                   _currentCase.Reason != Reason ||
                   _currentCase.Categories != categories ||
                   _currentCase.Observations != Observations ||
                   _currentCase.Status != SelectedStatus ||
                   _currentCase.NotificationsEnabled != IsNotificationEnabled ||
                   _currentCase.NotificationIntervalDays != GetNotificationIntervalDays();
        }


        private int GetNotificationIntervalDays()
        {
            return int.TryParse(SelectedNotificationInterval.Split(' ')[0], out var days) ? days : 0;
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseAction?.Invoke();
        }
    }
}