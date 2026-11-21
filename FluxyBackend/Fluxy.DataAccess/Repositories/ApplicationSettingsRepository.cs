using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Repositories;
using Fluxy.Core.Settings.Utils;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IApplicationSettingsRepository"/>.
    /// </summary>
    /// <remarks>
    /// The table holds one row, addressed by <see cref="ApplicationSettingsDefaults.Id"/> rather
    /// than by a query. The write stages the change and answers whether a row was there to
    /// change; the service decides what a missing row means.
    /// </remarks>
    internal sealed class ApplicationSettingsRepository : IApplicationSettingsRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationSettingsRepository"/> class.
        /// </summary>
        /// <param name="context">Context the settings row is read from and staged in.</param>
        public ApplicationSettingsRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public Task<ApplicationSettings?> GetAsync(CancellationToken cancellationToken = default)
            => _context.ApplicationSettings
                .AsNoTracking()
                .Where(settings => settings.Id == ApplicationSettingsDefaults.Id)
                .Select(settings => settings.ToModel())
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task AddAsync(
            ApplicationSettings settings,
            CancellationToken cancellationToken = default)
        {
            var entity = new ApplicationSettingsEntity(settings.Id);

            entity.FromModel(settings);

            _context.ApplicationSettings.Add(entity);

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            ApplicationSettings settings,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.ApplicationSettings
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
