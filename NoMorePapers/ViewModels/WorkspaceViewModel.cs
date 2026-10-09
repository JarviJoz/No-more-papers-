using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NoMorePapers.Models;
using NoMorePapers.Repositories;
using NoMorePapers.Services;
using System.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace NoMorePapers.ViewModels
{
    public enum WorkspaceMode { Active, Archived, Trash }
    public enum CaseSortOrder { MostRecent, StudentNameAscending, StudentNameDescending }

    public partial class WorkspaceViewModel : ObservableObject
    {
        private readonly ICaseRepository _caseRepository;
        private readonly IReminderRepository _reminderRepository;
        private readonly IProfileService _profileService;
        private readonly ToastNotificationService _toastNotificationService;
        private readonly AppSettingsService _appSettingsService;
        private readonly MainViewModel _mainViewModel;
        private readonly DispatcherTimer _agingTimer;
        private readonly Dictionary<string, (DateTime UpdatedAt, string Level)> _generatedAlerts = new();
        private readonly Dictionary<int, DateTime> _generatedReminderAlerts = new();
        private static MediaPlayer? _reminderSoundPlayer;
        private List<StudentCase> _allLoadedCases = new();
        private string _searchText = string.Empty;
        private bool _isCreatingReminder;
        private const string AllStatusesOption = "Todos los estados";
        private const string AllCategoriesOption = "Todas las categorías";
        private const string AllGradesOption = "Todos los grados";

        public UserProfile CurrentProfile { get; }

        [ObservableProperty] private WorkspaceMode _currentMode = WorkspaceMode.Active;
        [ObservableProperty] private string _modeTitle = "CASOS ACTIVOS";
        [ObservableProperty] private Brush _modeHeaderBrush = Brushes.Transparent;
        [ObservableProperty] private Brush _modeTitleBrush = new SolidColorBrush(Color.FromRgb(31, 31, 31));
        [ObservableProperty] private ObservableCollection<StudentCase> _cases = new();
        [ObservableProperty] private ObservableCollection<CaseNotification> _notifications = new();
        [ObservableProperty] private StudentCase? _selectedCase;
        [ObservableProperty] private bool _isNotificationPanelOpen;
        [ObservableProperty] private bool _hasNewNotifications;
        [ObservableProperty] private bool _isCalendarMode;
        [ObservableProperty] private bool _isSidebarExpanded = LoadSidebarExpandedPreference();
        [ObservableProperty] private string _selectedStatusFilter = AllStatusesOption;
        [ObservableProperty] private string _selectedCategoryFilter = AllCategoriesOption;
        [ObservableProperty] private string _selectedGradeFilter = AllGradesOption;
        [ObservableProperty] private DateTime? _selectedUpdatedDate;
        private CaseSortOrder _caseSortOrder = CaseSortOrder.MostRecent;
        [ObservableProperty] private DateTime _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        [ObservableProperty] private DateTime _selectedDate = DateTime.Today;
        [ObservableProperty] private ObservableCollection<CalendarDayViewModel> _calendarDays = new();
        [ObservableProperty] private ObservableCollection<Reminder> _selectedDateReminders = new();
        [ObservableProperty] private Reminder? _selectedReminder;
        [ObservableProperty] private string _reminderTitle = string.Empty;
        [ObservableProperty] private DateTime? _reminderSelectedTime = DateTime.Now;
        [ObservableProperty] private string _reminderTime = DateTime.Now.ToString("HH:mm");
        [ObservableProperty] private bool _reminderNotificationsEnabled = true;
        [ObservableProperty] private string _reminderDescription = string.Empty;
        [ObservableProperty] private string _reminderRecurrence = "No repetir";
        [ObservableProperty] private DateTime? _reminderRecurrenceEndDate;
        [ObservableProperty] private int _reminderNotificationAdvanceMinutes;
        [ObservableProperty] private bool _reminderSoundEnabled = true;

        public IReadOnlyList<string> RecurrenceOptions { get; } = new[]
        {
            "No repetir", "Cada día", "Cada semana", "Cada mes", "Personalizado"
        };
        public IReadOnlyList<int> NotificationAdvanceOptions { get; } = new[] { 0, 5, 10, 15, 30, 60 };
        public IReadOnlyList<string> StatusFilterOptions => new[] { AllStatusesOption }
            .Concat(_allLoadedCases.Select(studentCase => studentCase.Status)
                .Where(status => !string.IsNullOrWhiteSpace(status))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(status => status, StringComparer.CurrentCultureIgnoreCase))
            .ToArray();
        public IReadOnlyList<string> CategoryFilterOptions => new[] { AllCategoriesOption }
            .Concat(_allLoadedCases.SelectMany(studentCase => studentCase.Categories.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(category => category, StringComparer.CurrentCultureIgnoreCase))
            .ToArray();
        public IReadOnlyList<string> GradeFilterOptions => new[] { AllGradesOption }
            .Concat(_allLoadedCases.Select(studentCase => studentCase.Grade)
                .Where(grade => !string.IsNullOrWhiteSpace(grade))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(grade => grade, StringComparer.CurrentCultureIgnoreCase))
            .ToArray();
        public bool IsSelectedDateEditable => SelectedDate.Date >= DateTime.Today;
        public bool IsHistoricalDate => !IsSelectedDateEditable;
        public bool IsCasesMode => !IsCalendarMode;
        public string SidebarToggleToolTip => IsSidebarExpanded ? "Contraer menú" : "Expandir menú";
        public string DisplayModeTitle => IsCalendarMode ? "PROGRAMADOR" : ModeTitle;
        public int UnreadNotificationCount => Notifications.Count(notification => !notification.IsRead);
        public bool HasReminderForSelectedDate => SelectedDateReminders.Count > 0;
        public bool CanCreateReminder => IsSelectedDateEditable;
        public string SelectedDateStatusMessage => HasReminderForSelectedDate
            ? $"{SelectedDateReminders.Count} caso(s) programado(s) para este día."
            : IsHistoricalDate ? "Las fechas pasadas son solo de consulta." : "Día disponible para programación.";

        partial void OnSelectedCaseChanged(StudentCase? value)
        {
            if (value != null && !value.IsSelected)
            {
                value.IsSelected = true;
                NotifySelectionCommands();
            }

            EditCaseCommand.NotifyCanExecuteChanged();
            UnarchiveSelectedCommand.NotifyCanExecuteChanged();
        }

        public bool IsTrashMode => CurrentMode == WorkspaceMode.Trash;
        public bool IsArchivedMode => CurrentMode == WorkspaceMode.Archived;
        public bool IsActiveMode => CurrentMode == WorkspaceMode.Active;
        public bool IsActiveCasesMode => IsActiveMode && !IsCalendarMode;
        public bool ShowBackToActive => !IsActiveMode;
        public bool IsNotTrashMode => !IsTrashMode;
        public bool ShowReopenAction => IsActiveMode && HasSelectedCases && _allLoadedCases.Where(c => c.IsSelected).All(IsClosedCase);
        public bool ShowStatusActions => IsActiveMode && !ShowReopenAction && IsCasesMode;
        public bool CanChangeStatus => IsActiveMode && HasSelectedCases &&
            _allLoadedCases.Where(c => c.IsSelected).All(c => IsTrackedStatus(c.Status));
        public bool ShowArchivedAction => IsArchivedMode;
        public int SelectedCasesCount => _allLoadedCases.Count(studentCase => studentCase.IsSelected);
        public string SelectedCasesText => SelectedCasesCount == 1
            ? "1 caso seleccionado"
            : $"{SelectedCasesCount} casos seleccionados";

        partial void OnSelectedStatusFilterChanged(string value) => FilterCases();
        partial void OnSelectedCategoryFilterChanged(string value) => FilterCases();
        partial void OnSelectedGradeFilterChanged(string value) => FilterCases();
        partial void OnSelectedUpdatedDateChanged(DateTime? value) => FilterCases();

        partial void OnIsSidebarExpandedChanged(bool value)
        {
            OnPropertyChanged(nameof(SidebarToggleToolTip));
            SaveSidebarExpandedPreference(value);
        }

        private static bool LoadSidebarExpandedPreference()
        {
            try
            {
                var preferencePath = GetSidebarPreferencePath();
                return !File.Exists(preferencePath) || !bool.TryParse(File.ReadAllText(preferencePath), out var isExpanded) || isExpanded;
            }
            catch
            {
                return true;
            }
        }

        private static void SaveSidebarExpandedPreference(bool isExpanded)
        {
            try
            {
                var preferencePath = GetSidebarPreferencePath();
                Directory.CreateDirectory(Path.GetDirectoryName(preferencePath)!);
                File.WriteAllText(preferencePath, isExpanded.ToString());
            }
            catch
            {
            }
        }

        private static string GetSidebarPreferencePath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NoMorePapers",
            "sidebar-expanded.txt");

        [RelayCommand]
        private void SortByMostRecent() => SetCaseSortOrder(CaseSortOrder.MostRecent);

        [RelayCommand]
        private void SortByStudentNameAscending() => SetCaseSortOrder(CaseSortOrder.StudentNameAscending);

        [RelayCommand]
        private void SortByStudentNameDescending() => SetCaseSortOrder(CaseSortOrder.StudentNameDescending);

        private void SetCaseSortOrder(CaseSortOrder sortOrder)
        {
            _caseSortOrder = sortOrder;
            FilterCases();
        }

        partial void OnCurrentModeChanged(WorkspaceMode value)
        {
            OnPropertyChanged(nameof(IsTrashMode));
            OnPropertyChanged(nameof(IsArchivedMode));
            OnPropertyChanged(nameof(IsActiveMode));
            OnPropertyChanged(nameof(IsActiveCasesMode));
            OnPropertyChanged(nameof(ShowBackToActive));
            OnPropertyChanged(nameof(IsNotTrashMode));
            ModeHeaderBrush = value == WorkspaceMode.Trash
                ? new SolidColorBrush(Color.FromRgb(255, 235, 238))
                : value == WorkspaceMode.Archived
                    ? new SolidColorBrush(Color.FromRgb(13, 71, 161))
                    : Brushes.Transparent;
            ModeTitleBrush = value == WorkspaceMode.Trash
                ? new SolidColorBrush(Color.FromRgb(198, 40, 40))
                : value == WorkspaceMode.Archived
                    ? Brushes.White
                : new SolidColorBrush(Color.FromRgb(31, 31, 31));
            OnPropertyChanged(nameof(ShowReopenAction));
            OnPropertyChanged(nameof(ShowStatusActions));
            OnPropertyChanged(nameof(CanChangeStatus));
            OnPropertyChanged(nameof(ShowArchivedAction));
            MarkFinalizedCommand.NotifyCanExecuteChanged();
            MarkCancelledCommand.NotifyCanExecuteChanged();
            ReopenSelectedCommand.NotifyCanExecuteChanged();
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                FilterCases();
            }
        }

        public WorkspaceViewModel(ICaseRepository caseRepository, IReminderRepository reminderRepository, IProfileService profileService, ToastNotificationService toastNotificationService, AppSettingsService appSettingsService, MainViewModel mainViewModel)
        {
            _caseRepository = caseRepository;
            _reminderRepository = reminderRepository;
            _profileService = profileService;
            _toastNotificationService = toastNotificationService;
            _appSettingsService = appSettingsService;
            _mainViewModel = mainViewModel;
            CurrentProfile = _profileService.CurrentProfile!;
            _agingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _agingTimer.Tick += (_, _) => ReviewAging();
            _agingTimer.Start();
            RefreshCalendar();
            _ = LoadDataAsync();
            _ = LoadRemindersAsync();
        }

        partial void OnIsCalendarModeChanged(bool value)
        {
            OnPropertyChanged(nameof(IsCasesMode));
            OnPropertyChanged(nameof(IsActiveCasesMode));
            OnPropertyChanged(nameof(ShowStatusActions));
            OnPropertyChanged(nameof(DisplayModeTitle));
            if (value)
            {
                _ = LoadRemindersAsync();
            }
        }

        partial void OnDisplayedMonthChanged(DateTime value) => RefreshCalendar();

        partial void OnSelectedDateChanged(DateTime value)
        {
            OnPropertyChanged(nameof(IsSelectedDateEditable));
            OnPropertyChanged(nameof(IsHistoricalDate));
            RefreshCalendar();
            RefreshSelectedDateReminders();
            LoadSelectedReminderEditor();
        }

        partial void OnSelectedReminderChanged(Reminder? value)
        {
            if (value == null)
            {
                return;
            }

            _isCreatingReminder = false;
            ReminderSelectedTime = value.ScheduledAt;
            ReminderTime = value.ScheduledAt.ToString("HH:mm");
            ReminderTitle = value.Title;
            ReminderNotificationsEnabled = value.NotificationsEnabled;
            ReminderDescription = value.Description;
            ReminderRecurrence = value.Recurrence;
            ReminderRecurrenceEndDate = value.RecurrenceEndDate;
            ReminderNotificationAdvanceMinutes = value.NotificationAdvanceMinutes;
            ReminderSoundEnabled = value.SoundEnabled;
        }

        private List<Reminder> _allReminders = new();

        private async Task LoadRemindersAsync()
        {
            _allReminders = await _reminderRepository.GetByProfileAsync(CurrentProfile.Id);
            RefreshCalendar();
            RefreshSelectedDateReminders();
            LoadSelectedReminderEditor();
        }

        private void RefreshCalendar()
        {
            var firstDay = DisplayedMonth;
            var offset = (int)firstDay.DayOfWeek;
            CalendarDays = new ObservableCollection<CalendarDayViewModel>(
                Enumerable.Range(0, 42).Select(index =>
                    new CalendarDayViewModel(firstDay.AddDays(index - offset), firstDay, _allReminders, SelectedDate)));
        }

        private void RefreshSelectedDateReminders()
        {
            SelectedDateReminders = new ObservableCollection<Reminder>(
                _allReminders.Where(reminder => reminder.ScheduledAt.Date == SelectedDate.Date));
            OnPropertyChanged(nameof(HasReminderForSelectedDate));
            OnPropertyChanged(nameof(CanCreateReminder));
            OnPropertyChanged(nameof(SelectedDateStatusMessage));
        }

        private void ClearReminderEditor()
        {
            SelectedReminder = null;
            ReminderTitle = string.Empty;
            var now = DateTime.Now;
            ReminderSelectedTime = new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, now.Hour, now.Minute, 0);
            ReminderTime = DateTime.Now.ToString("HH:mm");
            ReminderNotificationsEnabled = true;
            ReminderDescription = string.Empty;
            ReminderRecurrence = RecurrenceOptions[0];
            ReminderRecurrenceEndDate = null;
            ReminderNotificationAdvanceMinutes = 0;
            ReminderSoundEnabled = true;
        }

        private void LoadSelectedReminderEditor()
        {
            var reminder = SelectedDateReminders.FirstOrDefault();
            if (reminder == null)
            {
                ClearReminderEditor();
                return;
            }

            SelectedReminder = reminder;
        }

        [RelayCommand]
        private void ToggleCalendar() => IsCalendarMode = !IsCalendarMode;

        [RelayCommand]
        private void PreviousMonth()
        {
            DisplayedMonth = DisplayedMonth.AddMonths(-1);
        }

        [RelayCommand]
        private void NextMonth()
        {
            DisplayedMonth = DisplayedMonth.AddMonths(1);
        }

        [RelayCommand]
        private async Task SelectCalendarDateAsync(CalendarDayViewModel? day)
        {
            if (day == null) return;
            SelectedDate = day.Date;
            await Task.CompletedTask;
        }

        [RelayCommand]
        private void NewReminder()
        {
            if (CanCreateReminder)
            {
                _isCreatingReminder = true;
                ClearReminderEditor();
            }
        }

        [RelayCommand]
        private async Task SaveReminderAsync()
        {
            if (!IsSelectedDateEditable) return;
            var time = ReminderSelectedTime?.TimeOfDay ?? TimeSpan.Zero;
            var reminderToEdit = _isCreatingReminder ? null : SelectedReminder;

            var scheduledAt = SelectedDate.Date.Add(time);
            if (scheduledAt.Date < DateTime.Today) return;

            if (reminderToEdit == null)
            {
                var reminder = new Reminder
                {
                    ProfileId = CurrentProfile.Id,
                    Title = string.IsNullOrWhiteSpace(ReminderTitle) ? "Caso programado" : ReminderTitle.Trim(),
                    ScheduledAt = scheduledAt,
                    NotificationsEnabled = ReminderNotificationsEnabled,
                    Description = ReminderDescription.Trim(),
                    Recurrence = ReminderRecurrence,
                    RecurrenceEndDate = ReminderRecurrenceEndDate,
                    NotificationAdvanceMinutes = ReminderNotificationAdvanceMinutes,
                    SoundEnabled = ReminderSoundEnabled
                };
                try
                {
                    await _reminderRepository.AddAsync(reminder);
                }
                catch (InvalidOperationException exception)
                {
                    MessageBox.Show(exception.Message, "Programador", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }
            else
            {
                if (reminderToEdit.ScheduledAt.Date < DateTime.Today) return;
                reminderToEdit.Title = string.IsNullOrWhiteSpace(ReminderTitle) ? "Caso programado" : ReminderTitle.Trim();
                reminderToEdit.ScheduledAt = scheduledAt;
                reminderToEdit.NotificationsEnabled = ReminderNotificationsEnabled;
                reminderToEdit.Description = ReminderDescription.Trim();
                reminderToEdit.Recurrence = ReminderRecurrence;
                reminderToEdit.RecurrenceEndDate = ReminderRecurrenceEndDate;
                reminderToEdit.NotificationAdvanceMinutes = ReminderNotificationAdvanceMinutes;
                reminderToEdit.SoundEnabled = ReminderSoundEnabled;
                try
                {
                    await _reminderRepository.UpdateAsync(reminderToEdit);
                }
                catch (InvalidOperationException exception)
                {
                    MessageBox.Show(exception.Message, "Programador", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            await LoadRemindersAsync();
            _isCreatingReminder = false;
            ClearReminderEditor();
        }

        public async Task DeleteReminderFromDetailsAsync(Reminder reminder)
        {
            if (reminder.ScheduledAt.Date < DateTime.Today) return;
            await _reminderRepository.DeleteAsync(reminder);
            await LoadRemindersAsync();
            ClearReminderEditor();
        }

        [RelayCommand]
        private async Task DeleteReminderAsync(Reminder? reminder)
        {
            if (reminder == null || reminder.ScheduledAt.Date < DateTime.Today) return;
            var result = MessageBox.Show(
                "¿Está seguro de que desea eliminar este recordatorio?",
                "Eliminar recordatorio",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            await _reminderRepository.DeleteAsync(reminder);
            await LoadRemindersAsync();
            ClearReminderEditor();
        }

        public async Task LoadDataAsync(bool preserveSelection = false)
        {
            var selectedCaseIds = preserveSelection
                ? _allLoadedCases.Where(c => c.IsSelected).Select(c => c.Id).ToHashSet()
                : new HashSet<string>();

            foreach (var item in _allLoadedCases)
            {
                item.PropertyChanged -= StudentCasePropertyChanged;
            }

            if (CurrentMode == WorkspaceMode.Active)
            {
                _allLoadedCases = await _caseRepository.GetActiveCasesByProfileAsync(CurrentProfile.Id);
                ModeTitle = "CASOS ACTIVOS";
                ReviewAging();
            }
            else if (CurrentMode == WorkspaceMode.Archived)
            {
                _allLoadedCases = await _caseRepository.GetArchivedCasesByProfileAsync(CurrentProfile.Id);
                ModeTitle = "CASOS ARCHIVADOS";
            }
            else
            {
                _allLoadedCases = await _caseRepository.GetDeletedCasesByProfileAsync(CurrentProfile.Id);
                ModeTitle = "PAPELERA DE RECICLAJE";
            }

            foreach (var item in _allLoadedCases)
            {
                item.PropertyChanged += StudentCasePropertyChanged;
                item.IsSelected = selectedCaseIds.Contains(item.Id);
            }

            OnPropertyChanged(nameof(StatusFilterOptions));
            OnPropertyChanged(nameof(CategoryFilterOptions));
            OnPropertyChanged(nameof(GradeFilterOptions));
            FilterCases();
            NotifySelectionCommands();
        }

        private void FilterCases()
        {
            IEnumerable<StudentCase> filtered = _allLoadedCases;
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(studentCase =>
                    studentCase.StudentName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    studentCase.Grade.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    studentCase.Categories.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedStatusFilter != AllStatusesOption)
            {
                filtered = filtered.Where(studentCase => string.Equals(studentCase.Status, SelectedStatusFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedCategoryFilter != AllCategoriesOption)
            {
                filtered = filtered.Where(studentCase => studentCase.Categories.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Contains(SelectedCategoryFilter, StringComparer.OrdinalIgnoreCase));
            }

            if (SelectedGradeFilter != AllGradesOption)
            {
                filtered = filtered.Where(studentCase => string.Equals(studentCase.Grade, SelectedGradeFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedUpdatedDate is DateTime updatedDate)
            {
                filtered = filtered.Where(studentCase => studentCase.UpdatedAt.Date == updatedDate.Date);
            }

            filtered = _caseSortOrder switch
            {
                CaseSortOrder.StudentNameAscending => filtered.OrderBy(studentCase => studentCase.StudentName, StringComparer.CurrentCultureIgnoreCase),
                CaseSortOrder.StudentNameDescending => filtered.OrderByDescending(studentCase => studentCase.StudentName, StringComparer.CurrentCultureIgnoreCase),
                _ => filtered.OrderByDescending(studentCase => studentCase.UpdatedAt)
            };

            Cases = new ObservableCollection<StudentCase>(filtered);
            foreach (var item in Cases)
            {
                UpdateAgingPresentation(item);
            }

            OnPropertyChanged(nameof(AreAllVisibleCasesSelected));
        }

        private void ReviewAging()
        {
            ReviewReminders();

            var notificationsEnabled = _appSettingsService.AreNotificationsEnabled;

            if (CurrentMode != WorkspaceMode.Active)
            {
                return;
            }

            var newNotification = false;
            foreach (var studentCase in _allLoadedCases)
            {
                var aging = CaseAgingService.Calculate(studentCase);
                UpdateAgingPresentation(studentCase);

                if (!CaseAgingService.IsTracked(studentCase) || aging.Level is not ("Naranja" or "Rojo"))
                {
                    _generatedAlerts.Remove(studentCase.Id);
                    continue;
                }

                if (!notificationsEnabled)
                {
                    _generatedAlerts.Remove(studentCase.Id);
                    continue;
                }

                if (!_generatedAlerts.TryGetValue(studentCase.Id, out var previous) ||
                    previous.UpdatedAt != studentCase.UpdatedAt || previous.Level != aging.Level)
                {
                    _generatedAlerts[studentCase.Id] = (studentCase.UpdatedAt, aging.Level);
                    var notification = CreateNotification(studentCase, aging);
                    ShowNotification(notification, new ToastNotificationRequest(
                        $"case-aging:{CurrentProfile.Id}:{studentCase.Id}:{studentCase.UpdatedAt.Ticks}:{aging.Level}",
                        notification.Title,
                        $"Estudiante: {studentCase.StudentName} · Caso {studentCase.Id} · {aging.Days} días sin cambios.",
                        "case",
                        studentCase.Id,
                        CurrentProfile.Id));
                    newNotification = true;
                }
            }

            FilterCases();
            if (newNotification)
            {
                UpdateNotificationBadge();
                PlayReminderAlert();
            }
        }

        private void ReviewReminders()
        {
            var now = DateTime.Now;
            var newNotification = false;
            var playSound = false;

            foreach (var reminder in _allReminders)
            {
                var alertAt = reminder.ScheduledAt.AddMinutes(-reminder.NotificationAdvanceMinutes);
                if (!reminder.NotificationsEnabled || alertAt > now || !_appSettingsService.AreNotificationsEnabled)
                {
                    _generatedReminderAlerts.Remove(reminder.Id);
                    continue;
                }

                if (_generatedReminderAlerts.TryGetValue(reminder.Id, out var alertedAt) &&
                    alertedAt == reminder.ScheduledAt)
                {
                    continue;
                }

                _generatedReminderAlerts[reminder.Id] = reminder.ScheduledAt;
                var notification = CreateReminderNotification(reminder);
                var reminderMessage = string.IsNullOrWhiteSpace(reminder.Description)
                    ? $"Programado para {reminder.ScheduledAt:dd/MM/yyyy HH:mm}."
                    : $"{reminder.Description} · {reminder.ScheduledAt:dd/MM/yyyy HH:mm}.";
                ShowNotification(notification, new ToastNotificationRequest(
                    $"reminder:{CurrentProfile.Id}:{reminder.Id}:{reminder.ScheduledAt.Ticks}",
                    notification.Title,
                    reminderMessage,
                    "reminder",
                    reminder.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    CurrentProfile.Id));
                newNotification = true;
                playSound |= reminder.SoundEnabled;
            }

            if (newNotification)
            {
                UpdateNotificationBadge();
                if (playSound && _appSettingsService.Settings.SoundsEnabled) PlayReminderAlert();
            }
        }

        private void ShowNotification(CaseNotification notification, ToastNotificationRequest toast)
        {
            Notifications.Insert(0, notification);
            _toastNotificationService.ShowOnce(toast);
        }

        private void UpdateNotificationBadge()
        {
            OnPropertyChanged(nameof(UnreadNotificationCount));
            HasNewNotifications = UnreadNotificationCount > 0;
        }

        private void PlayReminderAlert()
        {
            if (!_appSettingsService.Settings.SoundsEnabled)
            {
                return;
            }

            var soundPath = _appSettingsService.Settings.CustomSoundPath;
            if (string.IsNullOrWhiteSpace(soundPath) || !File.Exists(soundPath))
            {
                soundPath = Path.Combine(AppContext.BaseDirectory, "Resources", "reminder-alert.wav");
            }

            try
            {
                if (File.Exists(soundPath))
                {
                    _reminderSoundPlayer?.Close();
                    _reminderSoundPlayer = new MediaPlayer { Volume = _appSettingsService.Settings.NotificationVolume / 100d };
                    _reminderSoundPlayer.Open(new Uri(soundPath));
                    _reminderSoundPlayer.Play();
                    return;
                }
            }
            catch (Exception)
            {
            }

            SystemSounds.Exclamation.Play();
        }

        private static void UpdateAgingPresentation(StudentCase studentCase)
        {
            var aging = CaseAgingService.Calculate(studentCase);
            studentCase.AgingBrush = aging.Brush;
            studentCase.AgingTooltip = aging.Description;
        }

        private static CaseNotification CreateNotification(StudentCase studentCase, CaseAgingInfo aging) => new()
        {
            Title = aging.Level == "Rojo" ? "Caso con seguimiento atrasado" : "Caso requiere atención",
            StudentName = studentCase.StudentName,
            CaseId = studentCase.Id,
            Grade = studentCase.Grade,
            Categories = studentCase.Categories,
            Status = studentCase.Status,
            UpdatedAt = studentCase.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
            TimeWithoutModification = $"{aging.Days} días",
            Level = aging.Level,
            ColorBrush = aging.Brush
        };

        private static CaseNotification CreateReminderNotification(Reminder reminder) => new()
        {
            Title = string.IsNullOrWhiteSpace(reminder.Title) ? "Recordatorio programado" : reminder.Title,
            StudentName = reminder.Description,
            CaseId = $"REM-{reminder.Id}",
            Grade = "Recordatorio",
            Categories = reminder.Recurrence,
            Status = reminder.NotificationsEnabled ? "Notificación activa" : "Notificación desactivada",
            UpdatedAt = reminder.ScheduledAt.ToString("dd/MM/yyyy HH:mm"),
            TimeWithoutModification = "Programado",
            Level = "Recordatorio",
            ColorBrush = new SolidColorBrush(Color.FromRgb(217, 70, 168))
        };

        [RelayCommand]
        private async Task ShowActiveCasesAsync()
        {
            IsCalendarMode = false;
            CurrentMode = WorkspaceMode.Active;
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task ShowArchivedCasesAsync() { CurrentMode = WorkspaceMode.Archived; await LoadDataAsync(); }

        [RelayCommand]
        private async Task ShowTrashCasesAsync() { CurrentMode = WorkspaceMode.Trash; await LoadDataAsync(); }

        [RelayCommand]
        private async Task AddCaseAsync()
        {
            await OpenEditorAsync(null);
        }

        [RelayCommand(CanExecute = nameof(CanEditCase))]
        private async Task EditCaseAsync()
        {
            await OpenEditorAsync(SelectedCase);
        }

        private bool CanEditCase() => SelectedCase != null;

        public async Task OpenEditorAsync(StudentCase? studentCase)
        {
            var editVm = App.ServiceProvider.GetService(typeof(AddEditCaseViewModel)) as AddEditCaseViewModel;
            if (editVm == null) return;
            if (studentCase != null) await editVm.LoadCaseAsync(studentCase);

            var window = new Views.AddEditCaseWindow { DataContext = editVm };
            editVm.CloseAction = () => window.Close();
            window.ShowDialog();
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanUnarchiveSelected))]
        private async Task UnarchiveSelectedAsync()
        {
            var selected = _allLoadedCases.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0 && SelectedCase != null)
            {
                selected.Add(SelectedCase);
            }

            if (selected.Count == 0)
            {
                return;
            }

            foreach (var studentCase in selected)
            {
                studentCase.IsArchived = false;
                studentCase.UpdatedAt = DateTime.Now;
                await _caseRepository.UpdateCaseAsync(studentCase);
            }

            await LoadDataAsync();
        }

        private bool CanUnarchiveSelected() => IsArchivedMode && (HasSelectedCases || SelectedCase != null);

        [RelayCommand(CanExecute = nameof(HasSelectedCases))]
        private async Task RestoreSelectedAsync()
        {
            var selected = _allLoadedCases.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            foreach (var studentCase in selected)
            {
                studentCase.IsArchived = false;
                studentCase.IsDeleted = false;
                studentCase.DeletedAt = null;
                studentCase.UpdatedAt = DateTime.Now;
                await _caseRepository.UpdateCaseAsync(studentCase);
            }

            await LoadDataAsync();
        }

        [RelayCommand]
        private void ToggleSelection(StudentCase studentCase)
        {
            studentCase.IsSelected = !studentCase.IsSelected;
            NotifySelectionCommands();
        }

        [RelayCommand]
        private void ToggleSelectAll()
        {
            var select = Cases.Any(c => !c.IsSelected);
            foreach (var item in Cases) item.IsSelected = select;
            NotifySelectionCommands();
        }

        public bool AreAllVisibleCasesSelected => Cases.Count > 0 && Cases.All(c => c.IsSelected);
        public bool HasSelectedCases => _allLoadedCases.Any(c => c.IsSelected);
        private void NotifySelectionCommands()
        {
            OnPropertyChanged(nameof(AreAllVisibleCasesSelected));
            OnPropertyChanged(nameof(HasSelectedCases));
            OnPropertyChanged(nameof(SelectedCasesCount));
            OnPropertyChanged(nameof(SelectedCasesText));
            OnPropertyChanged(nameof(ShowReopenAction));
            OnPropertyChanged(nameof(ShowStatusActions));
            OnPropertyChanged(nameof(CanChangeStatus));
            EditCaseCommand.NotifyCanExecuteChanged();
            DeleteSelectedCommand.NotifyCanExecuteChanged();
            MarkFinalizedCommand.NotifyCanExecuteChanged();
            MarkCancelledCommand.NotifyCanExecuteChanged();
            RestoreSelectedCommand.NotifyCanExecuteChanged();
            ReopenSelectedCommand.NotifyCanExecuteChanged();
            UnarchiveSelectedCommand.NotifyCanExecuteChanged();
        }

        private void StudentCasePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StudentCase.IsSelected))
            {
                NotifySelectionCommands();
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedCases))]
        private async Task DeleteSelectedAsync()
        {
            var selected = _allLoadedCases.Where(c => c.IsSelected).ToList();
            var message = selected.Count == 1
                ? "¿Está seguro de que desea eliminar este caso?"
                : "¿Está seguro de que desea eliminar los casos seleccionados?";
            var result = MessageBox.Show(message, "Eliminar caso", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            foreach (var studentCase in selected)
            {
                studentCase.IsDeleted = true;
                studentCase.DeletedAt = DateTime.Now;
                await _caseRepository.UpdateCaseAsync(studentCase);
            }
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task MoveToTrashAsync()
        {
            if (SelectedCase == null)
            {
                return;
            }

            var result = MessageBox.Show(
                "¿Desea enviar este caso a la papelera?",
                "Mandar a la papelera",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            SelectedCase.IsDeleted = true;
            SelectedCase.IsArchived = false;
            SelectedCase.DeletedAt = DateTime.Now;
            SelectedCase.UpdatedAt = DateTime.Now;
            await _caseRepository.UpdateCaseAsync(SelectedCase);
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task ArchiveCaseAsync()
        {
            if (SelectedCase == null)
            {
                return;
            }

            var result = MessageBox.Show(
                "¿Desea archivar este caso?",
                "Archivar caso",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            SelectedCase.IsArchived = true;
            SelectedCase.IsDeleted = false;
            SelectedCase.DeletedAt = null;
            SelectedCase.UpdatedAt = DateTime.Now;
            await _caseRepository.UpdateCaseAsync(SelectedCase);
            await LoadDataAsync();
        }

        [RelayCommand(CanExecute = nameof(CanChangeStatus))]
        private async Task MarkFinalizedAsync() => await ChangeSelectedStatusAsync("Finalizado");

        [RelayCommand(CanExecute = nameof(CanChangeStatus))]
        private async Task MarkCancelledAsync() => await ChangeSelectedStatusAsync("Cancelado");

        private async Task ChangeSelectedStatusAsync(string status)
        {
            var selected = _allLoadedCases.Where(c => c.IsSelected).ToList();
            if (selected.Count == 0 || selected.Any(c => !IsTrackedStatus(c.Status)))
            {
                return;
            }

            var result = MessageBox.Show($"¿Desea marcar los casos seleccionados como \"{status}\"?", "Cambiar estado", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            foreach (var studentCase in selected)
            {
                if (studentCase.Status != status)
                {
                    if (IsTrackedStatus(studentCase.Status))
                    {
                        studentCase.PreviousStatus = studentCase.Status;
                    }
                    studentCase.Status = status;
                    studentCase.UpdatedAt = DateTime.Now;
                    await _caseRepository.UpdateCaseAsync(studentCase);
                }
            }
            await LoadDataAsync(true);
        }

        [RelayCommand(CanExecute = nameof(ShowReopenAction))]
        private async Task ReopenSelectedAsync()
        {
            var selected = _allLoadedCases.Where(c => c.IsSelected && IsClosedCase(c)).ToList();
            foreach (var studentCase in selected)
            {
                studentCase.Status = IsTrackedStatus(studentCase.PreviousStatus)
                    ? studentCase.PreviousStatus
                    : "En Proceso";
                studentCase.UpdatedAt = DateTime.Now;
                await _caseRepository.UpdateCaseAsync(studentCase);
            }

            await LoadDataAsync(true);
        }

        private static bool IsTrackedStatus(string status) => status is "En Proceso" or "Pendiente";
        private static bool IsClosedCase(StudentCase studentCase) => studentCase.Status is "Finalizado" or "Cancelado" or "TERMINADO" or "CANCELADO";

        [RelayCommand]
        private void OpenNotifications()
        {
            IsNotificationPanelOpen = !IsNotificationPanelOpen;
        }

        [RelayCommand]
        private void CloseNotifications() => IsNotificationPanelOpen = false;

        [RelayCommand]
        private void MarkNotificationAsRead(CaseNotification? notification)
        {
            if (notification == null || notification.IsRead)
            {
                return;
            }

            notification.IsRead = true;
            UpdateNotificationBadge();
        }

        [RelayCommand]
        private void MarkAllNotificationsAsRead()
        {
            foreach (var notification in Notifications)
            {
                notification.IsRead = true;
            }

            UpdateNotificationBadge();
        }

        [RelayCommand]
        private void DeleteNotification(CaseNotification? notification)
        {
            if (notification == null || !Notifications.Remove(notification))
            {
                return;
            }

            UpdateNotificationBadge();
        }

        [RelayCommand]
        private void DeleteAllNotifications()
        {
            if (Notifications.Count == 0)
            {
                return;
            }

            var result = MessageBox.Show(
                "¿Deseas eliminar todas las notificaciones?",
                "Borrar notificaciones",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            Notifications.Clear();
            UpdateNotificationBadge();
        }

        [RelayCommand]
        private async Task OpenNotificationSourceAsync(CaseNotification? notification)
        {
            if (notification == null)
            {
                return;
            }

            MarkNotificationAsRead(notification);
            IsNotificationPanelOpen = false;
            await NavigateToNotificationSourceAsync(notification.CaseId);
        }

        public async Task OpenToastSourceAsync(string sourceType, string sourceId)
        {
            if (sourceType.Equals("reminder", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(sourceId, out var reminderId))
            {
                var internalNotification = Notifications.FirstOrDefault(notification =>
                    string.Equals(notification.CaseId, $"REM-{reminderId}", StringComparison.OrdinalIgnoreCase));
                if (internalNotification != null)
                {
                    await OpenNotificationSourceAsync(internalNotification);
                    return;
                }

                await NavigateToNotificationSourceAsync($"REM-{reminderId}");
                return;
            }

            if (sourceType.Equals("case", StringComparison.OrdinalIgnoreCase))
            {
                var internalNotification = Notifications.FirstOrDefault(notification =>
                    string.Equals(notification.CaseId, sourceId, StringComparison.OrdinalIgnoreCase));
                if (internalNotification != null)
                {
                    await OpenNotificationSourceAsync(internalNotification);
                    return;
                }

                await NavigateToNotificationSourceAsync(sourceId);
            }
        }

        private async Task NavigateToNotificationSourceAsync(string sourceId)
        {
            if (sourceId.StartsWith("REM-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(sourceId[4..], out var reminderId))
            {
                var reminder = _allReminders.FirstOrDefault(item => item.Id == reminderId);
                if (reminder == null)
                {
                    await LoadRemindersAsync();
                    reminder = _allReminders.FirstOrDefault(item => item.Id == reminderId);
                }

                if (reminder != null)
                {
                    DisplayedMonth = new DateTime(reminder.ScheduledAt.Year, reminder.ScheduledAt.Month, 1);
                    SelectedDate = reminder.ScheduledAt.Date;
                    SelectedReminder = reminder;
                    IsCalendarMode = true;
                }

                return;
            }

            IsCalendarMode = false;
            CurrentMode = WorkspaceMode.Active;
            await LoadDataAsync();
            SelectedCase = _allLoadedCases.FirstOrDefault(item =>
                string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        }

        [RelayCommand]
        private void OpenStudents()
        {
            var studentsVm = App.ServiceProvider.GetService(typeof(StudentsViewModel)) as StudentsViewModel;
            _mainViewModel.NavigateTo(studentsVm!);
        }

        [RelayCommand]
        private async Task RestoreCaseAsync()
        {
            if (SelectedCase == null) return;
            SelectedCase.IsArchived = false;
            SelectedCase.IsDeleted = false;
            SelectedCase.UpdatedAt = DateTime.Now;
            await _caseRepository.UpdateCaseAsync(SelectedCase);
            await LoadDataAsync();
        }

        [RelayCommand]
        private async Task HardDeleteCaseAsync()
        {
            if (SelectedCase == null) return;
            var result = MessageBox.Show(
                "Este proceso es irreversible, al aceptar está de acuerdo en borrar este caso de manera definitiva.",
                "Eliminar caso definitivamente",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await _caseRepository.DeleteCasePermanentlyAsync(SelectedCase.Id);
                await LoadDataAsync();
            }
        }

        [RelayCommand]
        private async Task ExportToExcelAsync()
        {
            if (_allLoadedCases.Count == 0) { MessageBox.Show("No hay datos para exportar en esta vista.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dialog = new Microsoft.Win32.SaveFileDialog { FileName = $"Reporte_{ModeTitle}_{DateTime.Now:yyyyMMdd}", DefaultExt = ".csv", Filter = "Archivos CSV (Excel)|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                try { await Helpers.ExportHelper.ExportCasesToCsvAsync(dialog.FileName, _allLoadedCases); MessageBox.Show("Archivo exportado correctamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information); }
                catch (Exception ex) { MessageBox.Show($"Error al exportar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        [RelayCommand]
        private void PrintCase()
        {
            if (SelectedCase == null) { MessageBox.Show("Selecciona un caso de la tabla para imprimir el reporte.", "Impresión", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            try { Helpers.DocumentHelper.PrintStudentReport(SelectedCase); }
            catch (Exception ex) { MessageBox.Show($"Error al generar el reporte: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        [RelayCommand]
        private void ShowStatistics()
        {
            var statsVm = App.ServiceProvider.GetService(typeof(StatisticsViewModel)) as StatisticsViewModel;
            _mainViewModel.NavigateTo(statsVm!);
        }

        [RelayCommand]
        private void Logout()
        {
            _agingTimer.Stop();
            _profileService.Logout();
            var profileSelectionVm = App.ServiceProvider.GetService(typeof(ProfileSelectionViewModel)) as ProfileSelectionViewModel;
            _mainViewModel.NavigateTo(profileSelectionVm!);
        }
    }
}