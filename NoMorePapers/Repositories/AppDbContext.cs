using Microsoft.EntityFrameworkCore;
using NoMorePapers.Models;
using System;

namespace NoMorePapers.Repositories
{
    public class AppDbContext : DbContext
    {
        public DbSet<StudentCase> StudentCases { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<StudentDocument> StudentDocuments { get; set; }
        public DbSet<StudentSibling> StudentSiblings { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<CaseFollowUp> CaseFollowUps { get; set; }
        public DbSet<Reminder> Reminders { get; set; }

        public AppDbContext() { }
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string appFolder = System.IO.Path.Combine(folder, "NoMorePapers");
                System.IO.Directory.CreateDirectory(appFolder);
                string dbPath = System.IO.Path.Combine(appFolder, "nomorepapers.db");
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CaseFollowUp>()
                .HasIndex(followUp => new { followUp.StudentCaseId, followUp.FollowUpNumber })
                .IsUnique();

            modelBuilder.Entity<StudentCase>()
                .HasOne(studentCase => studentCase.Student)
                .WithMany()
                .HasForeignKey(studentCase => studentCase.StudentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudentSibling>()
                .HasKey(sibling => new { sibling.StudentId, sibling.SiblingStudentId });

            modelBuilder.Entity<StudentDocument>()
                .HasIndex(document => document.StudentId);

            modelBuilder.Entity<Reminder>()
                .HasIndex(reminder => new { reminder.ProfileId, reminder.DateKey });
        }
    }
}