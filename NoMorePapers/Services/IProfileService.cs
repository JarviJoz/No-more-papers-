using NoMorePapers.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NoMorePapers.Services
{
    public interface IProfileService
    {
        UserProfile? CurrentProfile { get; }
        Task<List<UserProfile>> GetAllProfilesAsync();
        Task<bool> ConfigureProfileAsync(int slotNumber, string name, DateTime birthDate, string? password, string? imagePath);
        bool Login(UserProfile profile, string? inputPassword);
        Task DeleteProfileAsync(UserProfile profile);
        Task<bool> ResetPasswordAsync(UserProfile profile, DateTime birthDate, string newPassword);
        void Logout();
    }
}