using Microsoft.EntityFrameworkCore;
using NoMorePapers.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NoMorePapers.Repositories
{
    public sealed class ReminderRepository : IReminderRepository
    {
        private readonly AppDbContext _context;

        public ReminderRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<List<Reminder>> GetByProfileAsync(int profileId) =>
            _context.Reminders
                .Where(reminder => reminder.ProfileId == profileId)
                .OrderBy(reminder => reminder.ScheduledAt)
                .ToListAsync();

        public Task<bool> ExistsOnDateAsync(int profileId, DateTime date, int? excludedId = null)
        {
            var dateKey = date.ToString("yyyy-MM-dd");
            return _context.Reminders.AnyAsync(reminder =>
                reminder.ProfileId == profileId &&
                reminder.DateKey == dateKey &&
                (!excludedId.HasValue || reminder.Id != excludedId.Value));
        }

        public async Task AddAsync(Reminder reminder)
        {
            reminder.DateKey = reminder.ScheduledAt.ToString("yyyy-MM-dd");

            _context.Reminders.Add(reminder);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(Reminder reminder)
        {
            reminder.DateKey = reminder.ScheduledAt.ToString("yyyy-MM-dd");

            reminder.UpdatedAt = DateTime.Now;
            _context.Reminders.Update(reminder);
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(Reminder reminder)
        {
            _context.Reminders.Remove(reminder);
            await _context.SaveChangesAsync();
        }
    }
}
