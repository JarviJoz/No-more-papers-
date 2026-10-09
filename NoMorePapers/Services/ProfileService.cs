using Microsoft.EntityFrameworkCore;
using NoMorePapers.Helpers;
using NoMorePapers.Models;
using NoMorePapers.Repositories;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NoMorePapers.Services
{
    public class ProfileService : IProfileService
    {
        private readonly AppDbContext _context;
        private readonly StudentDocumentStorageService _documentStorage;
        public UserProfile? CurrentProfile { get; private set; }

        public ProfileService(AppDbContext context, StudentDocumentStorageService documentStorage)
        {
            _context = context;
            _documentStorage = documentStorage;
        }

        public ProfileService(AppDbContext context) : this(context, new StudentDocumentStorageService())
        {
        }

        public async Task<List<UserProfile>> GetAllProfilesAsync()
        {
            var profilesInDb = await _context.Set<UserProfile>().ToListAsync();
            var allProfiles = new List<UserProfile>();

            // Siempre devolvemos 3 slots fijos para la pantalla de inicio
            for (int i = 1; i <= 3; i++)
            {
                var profile = profilesInDb.Find(p => p.SlotNumber == i);
                if (profile != null)
                {
                    allProfiles.Add(profile);
                }
                else
                {
                    // Si el slot está vacío en la base de datos, lo marcamos como "No Configurado"
                    allProfiles.Add(new UserProfile { SlotNumber = i });
                }
            }
            return allProfiles;
        }

        public async Task<bool> ConfigureProfileAsync(int slotNumber, string name, DateTime birthDate, string? password, string? imagePath)
        {
            var profile = new UserProfile
            {
                SlotNumber = slotNumber,
                Name = name,
                BirthDate = birthDate,
                ProfileImagePath = imagePath
            };

            // Solo encriptamos la contraseña si el usuario escribió una
            if (!string.IsNullOrWhiteSpace(password))
            {
                profile.PasswordHash = SecurityHelper.HashPassword(password);
            }

            _context.Set<UserProfile>().Add(profile);
            await _context.SaveChangesAsync();
            return true;
        }

        public bool Login(UserProfile profile, string? inputPassword)
        {
            if (!profile.IsConfigured) return false;

            // Si el perfil tiene contraseña, la verificamos
            if (!string.IsNullOrEmpty(profile.PasswordHash))
            {
                if (string.IsNullOrEmpty(inputPassword) || !SecurityHelper.VerifyPassword(inputPassword, profile.PasswordHash))
                {
                    return false; // Contraseña incorrecta
                }
            }

            // Si todo está bien, lo asignamos como el perfil activo
            CurrentProfile = profile;
            return true;
        }

        public async Task DeleteProfileAsync(UserProfile profile)
        {
            var students = await _context.Students
                .Where(student => student.ProfileId == profile.Id)
                .ToListAsync();
            var studentIds = students.Select(student => student.Id).ToList();
            var cases = await _context.StudentCases
                .Where(studentCase => studentCase.ProfileId == profile.Id ||
                    (studentCase.StudentId != null && studentIds.Contains(studentCase.StudentId)))
                .ToListAsync();

            var caseIds = cases.Select(studentCase => studentCase.Id).ToList();
            var followUps = await _context.CaseFollowUps
                .Where(followUp => caseIds.Contains(followUp.StudentCaseId))
                .ToListAsync();
            var documents = await _context.StudentDocuments
                .Where(document => studentIds.Contains(document.StudentId))
                .ToListAsync();
            var siblings = await _context.StudentSiblings
                .Where(relation => studentIds.Contains(relation.StudentId) || studentIds.Contains(relation.SiblingStudentId))
                .ToListAsync();
            var reminders = await _context.Reminders
                .Where(reminder => reminder.ProfileId == profile.Id)
                .ToListAsync();

            _context.CaseFollowUps.RemoveRange(followUps);
            _context.StudentDocuments.RemoveRange(documents);
            _context.StudentSiblings.RemoveRange(siblings);
            _context.StudentCases.RemoveRange(cases);
            _context.Reminders.RemoveRange(reminders);
            _context.Students.RemoveRange(students);
            _context.UserProfiles.Remove(profile);
            await _context.SaveChangesAsync();

            foreach (var document in documents)
            {
                try
                {
                    await _documentStorage.DeleteStoredCopyAsync(document.StudentId, document.FilePath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
                {
                    Trace.TraceError("No se pudo eliminar el documento {0} del perfil {1}: {2}", document.FilePath, profile.Id, exception);
                }
            }
        }

        public async Task<bool> ResetPasswordAsync(UserProfile profile, DateTime birthDate, string newPassword)
        {
            if (!profile.IsConfigured || profile.BirthDate?.Date != birthDate.Date || string.IsNullOrWhiteSpace(newPassword))
            {
                return false;
            }

            profile.PasswordHash = SecurityHelper.HashPassword(newPassword);
            _context.UserProfiles.Update(profile);
            await _context.SaveChangesAsync();
            return true;
        }

        public void Logout()
        {
            CurrentProfile = null;
        }
    }
}