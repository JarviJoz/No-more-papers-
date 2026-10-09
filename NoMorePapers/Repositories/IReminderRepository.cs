using NoMorePapers.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoMorePapers.Repositories
{
    public interface IReminderRepository
    {
        Task<List<Reminder>> GetByProfileAsync(int profileId);
        Task<bool> ExistsOnDateAsync(int profileId, DateTime date, int? excludedId = null);
        Task AddAsync(Reminder reminder);
        Task UpdateAsync(Reminder reminder);
        Task DeleteAsync(Reminder reminder);
    }
}
