using Fluxy.Application.Settings.Utils;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Settings.Commands;
using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Outcomes;
using Fluxy.Core.Settings.ReadModels;
using Fluxy.Core.Settings.Repositories;
using Fluxy.Core.Settings.Utils;

namespace Fluxy.Application.Settings.Services
{
    /// <summary>
    /// Reads the MAG settings and writes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// The shape is the application settings service's: one row, a full write, the snapshot
    /// refreshed after the single save. Nothing consumes the values in this pass, so the write
    /// exists to store and expose them.
    /// </remarks>
    public sealed class MagSettingsService : IMagSettingsService
    {
        private readonly IMagSettingsRepository _settings;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISettingsSnapshotProvider _snapshots;

        /// <summary>
        /// Initializes a new instance of the <see cref="MagSettingsService"/> class.
        /// </summary>
        /// <param name="settings">Repository holding the single MAG settings row.</param>
        /// <param name="unitOfWork">Commits the change the operation stages.</param>
        /// <param name="snapshots">Cache the change is published to after the save.</param>
        public MagSettingsService(
            IMagSettingsRepository settings,
            IUnitOfWork unitOfWork,
            ISettingsSnapshotProvider snapshots)
        {
            _settings = settings;
            _unitOfWork = unitOfWork;
            _snapshots = snapshots;
        }

        /// <inheritdoc />
        public async Task<MagSettingsSnapshot> GetAsync(
            CancellationToken cancellationToken = default)
        {
            var settings = await _settings.GetAsync(cancellationToken);

            return ToSnapshot(settings ?? MagSettingsDefaults.Create());
        }

        /// <inheritdoc />
        public async Task<MagSettingsOutcome> UpdateAsync(
            MagSettingsPatch patch,
            CancellationToken cancellationToken = default)
        {
            var errors = MagSettingsRules.Validate(patch);

            if (errors.Count > 0)
            {
                return new MagSettingsOutcome
                {
                    Action = MagSettingsAction.Invalid,
                    Errors = errors
                };
            }

            var stored = await _settings.GetAsync(cancellationToken);
            var settings = MagSettingsRules.Normalize(
                patch,
                stored?.Id ?? MagSettingsDefaults.Id);

            if (stored is null)
            {
                await _settings.AddAsync(settings, cancellationToken);
            }
            else if (!await _settings.UpdateAsync(settings, cancellationToken))
            {
                // The row vanished between the read and the write; re-staging it as an insert is
                // the truthful repair, exactly as the application settings service does.
                await _settings.AddAsync(settings, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _snapshots.ReloadAsync(cancellationToken);

            return new MagSettingsOutcome
            {
                Action = MagSettingsAction.Updated
            };
        }

        /// <summary>Turns the stored model into the snapshot other callers read.</summary>
        private static MagSettingsSnapshot ToSnapshot(MagSettings settings) => new()
        {
            ShowAllCategories = settings.ShowAllCategories,
            StrictSecurity = settings.StrictSecurity,
            ShowChannelLogos = settings.ShowChannelLogos,
            ShowPreviewLogos = settings.ShowPreviewLogos,
            DefaultContainer = settings.DefaultContainer,
            DefaultTheme = settings.DefaultTheme,
            RecordingMaxLengthMinutes = settings.RecordingMaxLengthMinutes,
            MaxLocalRecordings = settings.MaxLocalRecordings,
            PlaybackLimit = settings.PlaybackLimit,
            AllowedStbTypes = [.. settings.AllowedStbTypes],
            AllowedRecordingStbTypes = [.. settings.AllowedRecordingStbTypes]
        };
    }
}
