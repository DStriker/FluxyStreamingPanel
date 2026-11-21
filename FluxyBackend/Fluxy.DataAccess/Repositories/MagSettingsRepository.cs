using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Repositories;
using Fluxy.Core.Settings.Utils;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IMagSettingsRepository"/>.
    /// </summary>
    /// <remarks>
    /// The shape is the application settings repository's: one row addressed by its fixed
    /// identifier, a null read when it is absent, and a staged write the service commits.
    /// </remarks>
    internal sealed class MagSettingsRepository : IMagSettingsRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="MagSettingsRepository"/> class.
        /// </summary>
        /// <param name="context">Context the settings row is read from and staged in.</param>
        public MagSettingsRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public Task<MagSettings?> GetAsync(CancellationToken cancellationToken = default)
            => _context.MagSettings
                .AsNoTracking()
                .Where(settings => settings.Id == MagSettingsDefaults.Id)
                .Select(settings => settings.ToModel())
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task AddAsync(MagSettings settings, CancellationToken cancellationToken = default)
        {
            var entity = new MagSettingsEntity(settings.Id);

            entity.FromModel(settings);

            _context.MagSettings.Add(entity);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            MagSettings settings,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.MagSettings
                .FirstOrDefaultAsync(row => row.Id == settings.Id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            entity.FromModel(settings);

            return true;
        }
    }
}
