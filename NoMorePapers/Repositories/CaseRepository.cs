using Microsoft.EntityFrameworkCore;
using NoMorePapers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoMorePapers.Repositories
{
    public class CaseRepository : ICaseRepository
    {
        private readonly AppDbContext _context;

        public CaseRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Student>> GetStudentsByProfileAsync(int profileId)
        {
            return await _context.Students
                .Where(student => student.ProfileId == profileId)
                .OrderBy(student => student.Name)
                .ToListAsync();
        }

        public async Task<Student?> GetStudentByIdAsync(string id)
        {
            return await _context.Students.FindAsync(id);
        }

        public async Task<List<Student>> FindStudentsByNameAsync(int profileId, string name, string? excludedStudentId = null)
        {
            var queryTokens = NormalizeName(name).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (queryTokens.Length == 0 || queryTokens.Any(token => token.Length < 2))
            {
                return new List<Student>();
            }

            var candidates = await _context.Students
                .Where(student => student.ProfileId == profileId && student.Id != excludedStudentId)
                .ToListAsync();

            return candidates
                .Select(student => new
                {
                    Student = student,
                    Score = GetNameMatchScore(queryTokens, NormalizeName(student.Name).Split(' ', StringSplitOptions.RemoveEmptyEntries))
                })
                .Where(result => result.Score >= 0.65)
                .OrderByDescending(result => result.Score)
                .ThenBy(result => result.Student.Name)
                .Select(result => result.Student)
                .ToList();
        }

        public async Task<List<StudentCase>> GetCasesByStudentAsync(string studentId)
        {
            return await _context.StudentCases
                .Where(studentCase => studentCase.StudentId == studentId)
                .OrderByDescending(studentCase => studentCase.CreatedAt)
                .ToListAsync();
        }

        public async Task AddStudentAsync(Student student)
        {
            _context.Students.Add(student);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateStudentAsync(Student student)
        {
            student.UpdatedAt = DateTime.Now;
            _context.Students.Update(student);
            await _context.SaveChangesAsync();
        }

        public async Task<List<StudentDocument>> DeleteStudentsPermanentlyAsync(IReadOnlyCollection<string> studentIds)
        {
            var students = await _context.Students
                .Where(student => studentIds.Contains(student.Id))
                .ToListAsync();
            if (students.Count == 0)
            {
                return new List<StudentDocument>();
            }

            var documents = await _context.StudentDocuments
                .Where(document => studentIds.Contains(document.StudentId))
                .ToListAsync();
            var studentCases = await _context.StudentCases
                .Where(studentCase => studentIds.Contains(studentCase.StudentId!))
                .ToListAsync();
            var caseIds = studentCases.Select(studentCase => studentCase.Id).ToList();
            var followUps = await _context.CaseFollowUps
                .Where(followUp => caseIds.Contains(followUp.StudentCaseId))
                .ToListAsync();
            var siblingRelations = await _context.StudentSiblings
                .Where(relation => studentIds.Contains(relation.StudentId) || studentIds.Contains(relation.SiblingStudentId))
                .ToListAsync();

            await using var transaction = await _context.Database.BeginTransactionAsync();
            _context.CaseFollowUps.RemoveRange(followUps);
            _context.StudentCases.RemoveRange(studentCases);
            _context.StudentDocuments.RemoveRange(documents);
            _context.StudentSiblings.RemoveRange(siblingRelations);
            await _context.SaveChangesAsync();

            _context.Students.RemoveRange(students);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return documents;
        }

        public async Task AddSiblingRelationAsync(string studentId, string siblingStudentId)
        {
            if (studentId == siblingStudentId) return;

            var students = await _context.Students
                .Where(student => student.Id == studentId || student.Id == siblingStudentId)
                .Select(student => new { student.Id, student.ProfileId })
                .ToListAsync();
            if (students.Count != 2 || students[0].ProfileId != students[1].ProfileId)
            {
                return;
            }

            var existing = await _context.StudentSiblings.AnyAsync(relation =>
                relation.StudentId == studentId && relation.SiblingStudentId == siblingStudentId);
            if (!existing)
            {
                _context.StudentSiblings.Add(new StudentSibling
                {
                    StudentId = studentId,
                    SiblingStudentId = siblingStudentId
                });
            }

            var reverseExisting = await _context.StudentSiblings.AnyAsync(relation =>
                relation.StudentId == siblingStudentId && relation.SiblingStudentId == studentId);
            if (!reverseExisting)
            {
                _context.StudentSiblings.Add(new StudentSibling
                {
                    StudentId = siblingStudentId,
                    SiblingStudentId = studentId
                });
            }

            await _context.SaveChangesAsync();
        }

        public async Task RemoveSiblingRelationAsync(string studentId, string siblingStudentId)
        {
            var relations = await _context.StudentSiblings
                .Where(relation =>
                    (relation.StudentId == studentId && relation.SiblingStudentId == siblingStudentId) ||
                    (relation.StudentId == siblingStudentId && relation.SiblingStudentId == studentId))
                .ToListAsync();
            if (relations.Count == 0)
            {
                return;
            }

            _context.StudentSiblings.RemoveRange(relations);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Student>> GetSiblingsAsync(string studentId)
        {
            return await _context.Students
                .Where(student => _context.StudentSiblings.Any(relation =>
                    relation.StudentId == studentId && relation.SiblingStudentId == student.Id))
                .OrderBy(student => student.Name)
                .ToListAsync();
        }

        public async Task<List<StudentDocument>> GetStudentDocumentsAsync(string studentId)
        {
            return await _context.StudentDocuments
                .Where(document => document.StudentId == studentId)
                .OrderByDescending(document => document.UploadedAt)
                .ToListAsync();
        }

        public async Task AddStudentDocumentAsync(StudentDocument document)
        {
            _context.StudentDocuments.Add(document);
            await _context.SaveChangesAsync();
        }

        public async Task<StudentDocument?> DeleteStudentDocumentAsync(string studentId, int documentId)
        {
            var document = await _context.StudentDocuments
                .SingleOrDefaultAsync(item => item.Id == documentId && item.StudentId == studentId);
            if (document == null) return null;
            _context.StudentDocuments.Remove(document);
            await _context.SaveChangesAsync();
            return document;
        }

        public async Task<List<StudentCase>> GetActiveCasesByProfileAsync(int profileId)
        {
            return await _context.Set<StudentCase>()
                .Where(c => c.ProfileId == profileId && !c.IsArchived && !c.IsDeleted)
                .ToListAsync();
        }

        public async Task<List<StudentCase>> GetArchivedCasesByProfileAsync(int profileId)
        {
            return await _context.Set<StudentCase>()
                .Where(c => c.ProfileId == profileId && c.IsArchived && !c.IsDeleted)
                .ToListAsync();
        }

        public async Task<List<StudentCase>> GetDeletedCasesByProfileAsync(int profileId)
        {
            return await _context.Set<StudentCase>()
                .Where(c => c.ProfileId == profileId && c.IsDeleted)
                .ToListAsync();
        }

        public async Task<StudentCase?> GetCaseByIdAsync(string id)
        {
            return await _context.Set<StudentCase>().FindAsync(id);
        }

        public async Task AddCaseAsync(StudentCase studentCase)
        {
            _context.Set<StudentCase>().Add(studentCase);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateCaseAsync(StudentCase studentCase)
        {
            _context.Set<StudentCase>().Update(studentCase);
            await _context.SaveChangesAsync();
        }

        public async Task<List<CaseFollowUp>> GetFollowUpsAsync(string studentCaseId)
        {
            return await _context.CaseFollowUps
                .Where(followUp => followUp.StudentCaseId == studentCaseId)
                .OrderBy(followUp => followUp.FollowUpNumber)
                .ToListAsync();
        }

        public async Task SaveFollowUpAsync(CaseFollowUp followUp)
        {
            var existingFollowUp = await _context.CaseFollowUps
                .SingleOrDefaultAsync(existing =>
                    existing.StudentCaseId == followUp.StudentCaseId &&
                    existing.FollowUpNumber == followUp.FollowUpNumber);

            if (existingFollowUp == null)
            {
                _context.CaseFollowUps.Add(followUp);
            }
            else
            {
                existingFollowUp.ContentRtf = followUp.ContentRtf;
                existingFollowUp.FollowUpDate = followUp.FollowUpDate;
                existingFollowUp.SignatureImage = followUp.SignatureImage;
                existingFollowUp.SavedAt = followUp.SavedAt;
            }

            await _context.SaveChangesAsync();
        }

        public async Task DeleteFollowUpAsync(string studentCaseId, int followUpNumber)
        {
            var followUp = await _context.CaseFollowUps
                .SingleOrDefaultAsync(existing =>
                    existing.StudentCaseId == studentCaseId &&
                    existing.FollowUpNumber == followUpNumber);

            if (followUp != null)
            {
                _context.CaseFollowUps.Remove(followUp);
                await _context.SaveChangesAsync();
            }
        }

        public async Task DeleteCasePermanentlyAsync(string id)
        {
            var studentCase = await GetCaseByIdAsync(id);
            if (studentCase != null)
            {
                _context.Set<StudentCase>().Remove(studentCase);
                await _context.SaveChangesAsync();
            }
        }

        private static string NormalizeName(string value) =>
            string.Join(' ', value.Trim().ToUpperInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

        private static double GetNameMatchScore(string[] queryTokens, string[] nameTokens)
        {
            if (nameTokens.Length == 0)
            {
                return 0;
            }

            var tokenScores = queryTokens.Select(queryToken => nameTokens
                .Select(nameToken => GetTokenMatchScore(queryToken, nameToken))
                .Max());
            return tokenScores.Min();
        }

        private static double GetTokenMatchScore(string queryToken, string nameToken)
        {
            if (nameToken == queryToken)
            {
                return 1;
            }

            if (nameToken.Contains(queryToken, StringComparison.Ordinal))
            {
                return 0.9;
            }

            var distance = GetLevenshteinDistance(queryToken, nameToken);
            return 1d - (double)distance / Math.Max(queryToken.Length, nameToken.Length);
        }

        private static int GetLevenshteinDistance(string first, string second)
        {
            var previous = Enumerable.Range(0, second.Length + 1).ToArray();
            var current = new int[second.Length + 1];

            for (var firstIndex = 1; firstIndex <= first.Length; firstIndex++)
            {
                current[0] = firstIndex;
                for (var secondIndex = 1; secondIndex <= second.Length; secondIndex++)
                {
                    var cost = first[firstIndex - 1] == second[secondIndex - 1] ? 0 : 1;
                    current[secondIndex] = Math.Min(
                        Math.Min(current[secondIndex - 1] + 1, previous[secondIndex] + 1),
                        previous[secondIndex - 1] + cost);
                }

                (previous, current) = (current, previous);
            }

            return previous[second.Length];
        }
    }
}