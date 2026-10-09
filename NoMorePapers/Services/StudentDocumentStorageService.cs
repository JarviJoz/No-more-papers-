using NoMorePapers.Models;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NoMorePapers.Services
{
    public sealed class StudentDocumentStorageService
    {
        public const long LargeFileWarningThresholdBytes = 50L * 1024 * 1024;

        private readonly string _storageRoot;

        public StudentDocumentStorageService()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _storageRoot = Path.Combine(localAppData, "NoMorePapers", "StudentDocuments");
        }

        public async Task<string> CalculateContentHashAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            await using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash);
        }

        public async Task<StudentDocument> StoreCopyAsync(string studentId, string sourcePath, string contentHash, CancellationToken cancellationToken = default)
        {
            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            var studentDirectory = GetStudentDirectory(studentId);
            Directory.CreateDirectory(studentDirectory);

            var storedName = $"DOC-{Guid.NewGuid():N}{extension}";
            var destinationPath = Path.Combine(studentDirectory, storedName);
            var temporaryPath = destinationPath + ".tmp";

            try
            {
                await using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await using (var destination = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await source.CopyToAsync(destination, cancellationToken);
                    await destination.FlushAsync(cancellationToken);
                }

                File.Move(temporaryPath, destinationPath);
                return new StudentDocument
                {
                    StudentId = studentId,
                    FileName = Path.GetFileName(sourcePath),
                    FilePath = destinationPath,
                    FileExtension = extension,
                    FileSizeBytes = new FileInfo(destinationPath).Length,
                    ContentHash = contentHash,
                    UploadedAt = DateTime.Now
                };
            }
            catch
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                throw;
            }
        }

        public Task DeleteStoredCopyAsync(string studentId, string filePath)
        {
            var studentDirectory = Path.GetFullPath(GetStudentDirectory(studentId)) + Path.DirectorySeparatorChar;
            var fullPath = Path.GetFullPath(filePath);
            if (!fullPath.StartsWith(studentDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La ruta del documento no pertenece al almacenamiento de este estudiante.");
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            return Task.CompletedTask;
        }

        private string GetStudentDirectory(string studentId)
        {
            var studentKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(studentId)));
            return Path.Combine(_storageRoot, studentKey);
        }
    }
}
