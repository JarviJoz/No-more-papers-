using Microsoft.EntityFrameworkCore;
using Microsoft.Toolkit.Uwp.Notifications;
using NoMorePapers.Repositories;
using System;
using System.Globalization;

namespace NoMorePapers.Services
{
    public sealed record ToastNotificationRequest(
        string DeduplicationKey,
        string Title,
        string Message,
        string SourceType,
        string SourceId,
        int ProfileId);

    public sealed record ToastNotificationActivation(string SourceType, string SourceId, int ProfileId);

    public sealed class ToastNotificationService
    {
        private readonly AppDbContext _dbContext;

        public event EventHandler<ToastNotificationActivation>? Activated;

        public ToastNotificationService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;
        }

        public bool ShowOnce(ToastNotificationRequest request)
        {
            try
            {
                var connection = _dbContext.Database.GetDbConnection();
                var shouldClose = connection.State != System.Data.ConnectionState.Open;
                if (shouldClose)
                {
                    connection.Open();
                }

                int inserted;
                try
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = "INSERT OR IGNORE INTO NativeToastDeliveries (DeduplicationKey, CreatedAt) VALUES ($key, $createdAt)";
                    var keyParameter = command.CreateParameter();
                    keyParameter.ParameterName = "$key";
                    keyParameter.Value = request.DeduplicationKey;
                    command.Parameters.Add(keyParameter);
                    var createdAtParameter = command.CreateParameter();
                    createdAtParameter.ParameterName = "$createdAt";
                    createdAtParameter.Value = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);
                    command.Parameters.Add(createdAtParameter);
                    inserted = command.ExecuteNonQuery();
                }
                finally
                {
                    if (shouldClose)
                    {
                        connection.Close();
                    }
                }

                if (inserted == 0)
                {
                    return false;
                }

                new ToastContentBuilder()
                    .AddText(request.Title)
                    .AddText(request.Message)
                    .AddArgument("sourceType", request.SourceType)
                    .AddArgument("sourceId", request.SourceId)
                    .AddArgument("profileId", request.ProfileId.ToString(CultureInfo.InvariantCulture))
                    .Show();

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void OnToastActivated(ToastNotificationActivatedEventArgsCompat eventArgs)
        {
            try
            {
                var arguments = ToastArguments.Parse(eventArgs.Argument);
                if (arguments.TryGetValue("sourceType", out var sourceType) &&
                    arguments.TryGetValue("sourceId", out var sourceId) &&
                    arguments.TryGetValue("profileId", out var profileIdText) &&
                    int.TryParse(profileIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var profileId))
                {
                    Activated?.Invoke(this, new ToastNotificationActivation(sourceType, sourceId, profileId));
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
