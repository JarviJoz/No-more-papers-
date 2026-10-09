using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NoMorePapers.Models;
using NoMorePapers.Repositories;
using NoMorePapers.Services;
using NoMorePapers.ViewModels;

namespace NoMorePapers
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                var services = new ServiceCollection();
                services.AddSingleton<AppDbContext>();
                services.AddSingleton<StudentDocumentStorageService>();
                services.AddSingleton<IProfileService, ProfileService>();
                services.AddSingleton<ICaseRepository, CaseRepository>();
                services.AddSingleton<IReminderRepository, ReminderRepository>();
                services.AddSingleton<ToastNotificationService>();
                services.AddSingleton<AppSettingsService>();
                services.AddSingleton<ContactSubmissionService>();
                services.AddSingleton<MainViewModel>();
                services.AddTransient<ProfileSelectionViewModel>();
                services.AddTransient<ConfigureProfileViewModel>();
                services.AddTransient<WorkspaceViewModel>();
                services.AddTransient<StudentsViewModel>();
                services.AddTransient<StatisticsViewModel>();
                services.AddTransient<AddEditCaseViewModel>();

                App.ServiceProvider = services.BuildServiceProvider();
                var dbContext = App.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.EnsureCreated();
                dbContext.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS NativeToastDeliveries (
                    DeduplicationKey TEXT NOT NULL CONSTRAINT PK_NativeToastDeliveries PRIMARY KEY,
                    CreatedAt TEXT NOT NULL
                );");
                EnsureStudentCaseColumn(dbContext, "NotificationsEnabled", "INTEGER NOT NULL DEFAULT 1");
                EnsureStudentCaseColumn(dbContext, "NotificationIntervalDays", "INTEGER NOT NULL DEFAULT 15");
                EnsureStudentCaseColumn(dbContext, "PreviousStatus", "TEXT NOT NULL DEFAULT 'En Proceso'");
                EnsureStudentCaseColumn(dbContext, "AvatarType", "TEXT NOT NULL DEFAULT 'DefaultMale'");
                EnsureStudentCaseColumn(dbContext, "AvatarImage", "BLOB NULL");
                EnsureStudentData(dbContext);
                EnsureUserProfileColumn(dbContext, "IsMenuOpen", "INTEGER NOT NULL DEFAULT 0");
                dbContext.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS CaseFollowUps (
                    Id INTEGER NOT NULL CONSTRAINT PK_CaseFollowUps PRIMARY KEY AUTOINCREMENT,
                    StudentCaseId TEXT NOT NULL,
                    FollowUpNumber INTEGER NOT NULL,
                    ContentRtf TEXT NOT NULL,
                    FollowUpDate TEXT NULL,
                    SignatureImage BLOB NULL,
                    SavedAt TEXT NOT NULL
                );");
                dbContext.Database.ExecuteSqlRaw(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_CaseFollowUps_StudentCaseId_FollowUpNumber
                    ON CaseFollowUps (StudentCaseId, FollowUpNumber);");
                dbContext.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Reminders (
                    Id INTEGER NOT NULL CONSTRAINT PK_Reminders PRIMARY KEY AUTOINCREMENT,
                    ProfileId INTEGER NOT NULL,
                    DateKey TEXT NOT NULL DEFAULT '',
                    ScheduledAt TEXT NOT NULL,
                    Title TEXT NOT NULL DEFAULT '',
                    NotificationsEnabled INTEGER NOT NULL DEFAULT 1,
                    Description TEXT NOT NULL DEFAULT '',
                    Recurrence TEXT NOT NULL DEFAULT 'No repetir',
                    RecurrenceEndDate TEXT NULL,
                    NotificationAdvanceMinutes INTEGER NOT NULL DEFAULT 0,
                    SoundEnabled INTEGER NOT NULL DEFAULT 1,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );");
                EnsureReminderColumn(dbContext, "DateKey", "TEXT NOT NULL DEFAULT ''");
                EnsureReminderColumn(dbContext, "Title", "TEXT NOT NULL DEFAULT ''");
                EnsureReminderColumn(dbContext, "RecurrenceEndDate", "TEXT NULL");
                EnsureReminderColumn(dbContext, "NotificationAdvanceMinutes", "INTEGER NOT NULL DEFAULT 0");
                EnsureReminderColumn(dbContext, "SoundEnabled", "INTEGER NOT NULL DEFAULT 1");
                dbContext.Database.ExecuteSqlRaw("DROP INDEX IF EXISTS IX_Reminders_ProfileId_DateKey");
                dbContext.Database.ExecuteSqlRaw("UPDATE Reminders SET DateKey = strftime('%Y-%m-%d', ScheduledAt) WHERE DateKey = ''");
                dbContext.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS IX_Reminders_ProfileId_DateKey ON Reminders (ProfileId, DateKey)");

                var app = new App();
                app.InitializeComponent();
                App.ServiceProvider.GetRequiredService<AppSettingsService>().ApplyVisualPreferences();

                var mainViewModel = App.ServiceProvider.GetRequiredService<MainViewModel>();
                mainViewModel.NavigateTo(App.ServiceProvider.GetRequiredService<ProfileSelectionViewModel>());

                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };
                var toastNotificationService = App.ServiceProvider.GetRequiredService<ToastNotificationService>();
                toastNotificationService.Activated += (_, activation) => mainWindow.Dispatcher.InvokeAsync(() =>
                {
                    if (mainWindow.WindowState == System.Windows.WindowState.Minimized)
                    {
                        mainWindow.WindowState = System.Windows.WindowState.Normal;
                    }

                    mainWindow.Activate();
                    _ = mainViewModel.HandleToastActivationAsync(activation);
                });
                mainWindow.Show();
                app.Run();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error crítico en el Main: {ex.Message}\n\n{ex.StackTrace}",
                    "Error Fatal", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private static void EnsureReminderColumn(AppDbContext dbContext, string columnName, string columnDefinition)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) connection.Open();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('Reminders') WHERE name = '{columnName}'";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    command.CommandText = $"ALTER TABLE Reminders ADD COLUMN {columnName} {columnDefinition}";
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                if (shouldClose) connection.Close();
            }
        }

        private static void EnsureStudentCaseColumn(AppDbContext dbContext, string columnName, string columnDefinition)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('StudentCases') WHERE name = '{columnName}'";
                var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (!exists)
                {
                    command.CommandText = $"ALTER TABLE StudentCases ADD COLUMN {columnName} {columnDefinition}";
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }

        private static void EnsureStudentData(AppDbContext dbContext)
        {
            dbContext.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS Students (
                Id TEXT NOT NULL CONSTRAINT PK_Students PRIMARY KEY,
                ProfileId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                BirthDate TEXT NULL,
                Grade TEXT NOT NULL DEFAULT '',
                ""Group"" TEXT NOT NULL DEFAULT '',
                Shift TEXT NOT NULL DEFAULT '',
                Guardian1Name TEXT NULL,
                Guardian1Phone TEXT NULL,
                Guardian1Relation TEXT NOT NULL DEFAULT '',
                Guardian2Name TEXT NULL,
                Guardian2Phone TEXT NULL,
                Guardian2Relation TEXT NOT NULL DEFAULT '',
                HasSiblings INTEGER NOT NULL DEFAULT 0,
                Status TEXT NOT NULL DEFAULT 'Activo',
                AvatarType TEXT NOT NULL DEFAULT 'DefaultMale',
                AvatarImage BLOB NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );");
            dbContext.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS StudentDocuments (
                Id INTEGER NOT NULL CONSTRAINT PK_StudentDocuments PRIMARY KEY AUTOINCREMENT,
                StudentId TEXT NOT NULL,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                FileExtension TEXT NOT NULL DEFAULT '',
                FileSizeBytes INTEGER NOT NULL DEFAULT 0,
                ContentHash TEXT NOT NULL DEFAULT '',
                UploadedAt TEXT NOT NULL
            );");
            EnsureStudentDocumentColumn(dbContext, "FileExtension", "TEXT NOT NULL DEFAULT ''");
            EnsureStudentDocumentColumn(dbContext, "FileSizeBytes", "INTEGER NOT NULL DEFAULT 0");
            EnsureStudentDocumentColumn(dbContext, "ContentHash", "TEXT NOT NULL DEFAULT ''");
            dbContext.Database.ExecuteSqlRaw(@"CREATE TABLE IF NOT EXISTS StudentSiblings (
                StudentId TEXT NOT NULL,
                SiblingStudentId TEXT NOT NULL,
                CONSTRAINT PK_StudentSiblings PRIMARY KEY (StudentId, SiblingStudentId)
            );");
            EnsureStudentCaseColumn(dbContext, "StudentId", "TEXT NULL");

            var cases = dbContext.StudentCases
                .Where(studentCase => string.IsNullOrWhiteSpace(studentCase.StudentId))
                .ToList();
            var studentsByKey = dbContext.Students
                .ToList()
                .ToDictionary(student => $"{student.ProfileId}:{NormalizeName(student.Name)}");

            foreach (var studentCase in cases)
            {
                var key = $"{studentCase.ProfileId}:{NormalizeName(studentCase.StudentName)}";
                if (!studentsByKey.TryGetValue(key, out var student))
                {
                    student = new Student
                    {
                        Id = $"STU-{Guid.NewGuid():N}",
                        ProfileId = studentCase.ProfileId,
                        Name = studentCase.StudentName,
                        Grade = studentCase.Grade,
                        Group = studentCase.Group,
                        Shift = studentCase.Shift,
                        Guardian1Name = studentCase.GuardianName,
                        Guardian1Phone = studentCase.GuardianPhone,
                        Guardian1Relation = studentCase.GuardianRelation,
                        AvatarType = studentCase.AvatarType,
                        AvatarImage = studentCase.AvatarImage,
                        CreatedAt = studentCase.CreatedAt,
                        UpdatedAt = studentCase.UpdatedAt
                    };
                    dbContext.Students.Add(student);
                    studentsByKey[key] = student;
                }

                studentCase.StudentId = student.Id;
            }

            if (cases.Count > 0)
            {
                dbContext.SaveChanges();
            }
        }

        private static string NormalizeName(string value) =>
            string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static void EnsureStudentDocumentColumn(AppDbContext dbContext, string columnName, string columnDefinition)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;
            if (shouldClose) connection.Open();

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('StudentDocuments') WHERE name = '{columnName}'";
                if (Convert.ToInt32(command.ExecuteScalar()) == 0)
                {
                    command.CommandText = $"ALTER TABLE StudentDocuments ADD COLUMN {columnName} {columnDefinition}";
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                if (shouldClose) connection.Close();
            }
        }

        private static void EnsureUserProfileColumn(AppDbContext dbContext, string columnName, string columnDefinition)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != System.Data.ConnectionState.Open;

            if (shouldClose)
            {
                connection.Open();
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('UserProfiles') WHERE name = '{columnName}'";
                var exists = Convert.ToInt32(command.ExecuteScalar()) > 0;

                if (!exists)
                {
                    command.CommandText = $"ALTER TABLE UserProfiles ADD COLUMN {columnName} {columnDefinition}";
                    command.ExecuteNonQuery();
                }
            }
            finally
            {
                if (shouldClose)
                {
                    connection.Close();
                }
            }
        }
    }
}