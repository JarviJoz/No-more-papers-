using NoMorePapers.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoMorePapers.Repositories
{
    public interface ICaseRepository
    {
        Task<List<Student>> GetStudentsByProfileAsync(int profileId);
        Task<Student?> GetStudentByIdAsync(string id);
        Task<List<Student>> FindStudentsByNameAsync(int profileId, string name, string? excludedStudentId = null);
        Task<List<StudentCase>> GetCasesByStudentAsync(string studentId);
        Task AddStudentAsync(Student student);
        Task UpdateStudentAsync(Student student);
        Task<List<StudentDocument>> DeleteStudentsPermanentlyAsync(IReadOnlyCollection<string> studentIds);
        Task AddSiblingRelationAsync(string studentId, string siblingStudentId);
        Task RemoveSiblingRelationAsync(string studentId, string siblingStudentId);
        Task<List<Student>> GetSiblingsAsync(string studentId);
        Task<List<StudentDocument>> GetStudentDocumentsAsync(string studentId);
        Task AddStudentDocumentAsync(StudentDocument document);
        Task<StudentDocument?> DeleteStudentDocumentAsync(string studentId, int documentId);
        Task<List<StudentCase>> GetActiveCasesByProfileAsync(int profileId);
        Task<List<StudentCase>> GetArchivedCasesByProfileAsync(int profileId);
        Task<List<StudentCase>> GetDeletedCasesByProfileAsync(int profileId);
        Task<StudentCase?> GetCaseByIdAsync(string id);
        Task AddCaseAsync(StudentCase studentCase);
        Task UpdateCaseAsync(StudentCase studentCase);
        Task<List<CaseFollowUp>> GetFollowUpsAsync(string studentCaseId);
        Task SaveFollowUpAsync(CaseFollowUp followUp);
        Task DeleteFollowUpAsync(string studentCaseId, int followUpNumber);
        Task DeleteCasePermanentlyAsync(string id);
    }
}