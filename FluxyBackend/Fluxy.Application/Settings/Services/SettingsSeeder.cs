using Fluxy.Application.Security.Options;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Settings.Interfaces;
using Fluxy.Core.Settings.Models;
using Fluxy.Core.Settings.Repositories;
using Fluxy.Core.Settings.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Settings.Services
{
    /// <summary>
    /// Makes sure the installation has both settings rows, once, at startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The migration writes the two rows; this seeder is the runtime guard behind it, for the
    /// installation whose rows were removed by hand and for the integration fixture that
    /// truncates the database. It is idempotent - a row that exists is left exactly as the
    /// operator left it - and it never overwrites a stored value.
    /// </para>
    /// <para>
    /// <b>The configured reCAPTCHA secret is copied on the first creation only.</b> Before the
    /// admin screen existed, <c>RECAPTCHA_SECRET_KEY</c> was the only place the key could live;
    /// copying it once keeps such an installation working. After that the database is the only
    /// source - editing <c>.env</c> later changes nothing, because an operator who cleared the
    /// key in the panel must not have it silently restored by a restart.
    /// </para>
    /// <para>
    /// <b>It never throws</b> except for a cancelled shutdown. A failed write is reported and the
    /// boot continues: the snapshot provider answers the defaults until the next attempt, and an
    /// installation whose settings row is missing is still an installation.
    /// </para>
    /// </remarks>
    public sealed class SettingsSeeder : ISettingsSeeder
    {
        private readonly IApplicationSettingsRepository _applications;
        private readonly IMagSettingsRepository _mag;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IConfiguration _configuration;
        private readonly ISettingsSnapshotProvider _snapshots;
        private readonly ILogger<SettingsSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsSeeder"/> class.
        /// </summary>
        /// <param name="applications">Repository the application settings row is written through.</param>
        /// <param name="mag">Repository the MAG settings row is written through.</param>
        /// <param name="unitOfWork">Commits the rows the seed stages.</param>
        /// <param name="configuration">Configuration the one-time reCAPTCHA secret is read from.</param>
        /// <param name="snapshots">Cache primed once the rows are in place.</param>
        /// <param name="logger">Logger the seed and its failures are reported to.</param>
        public SettingsSeeder(
            IApplicationSettingsRepository applications,
            IMagSettingsRepository mag,
            IUnitOfWork unitOfWork,
            IConfiguration configuration,
            ISettingsSnapshotProvider snapshots,
            ILogger<SettingsSeeder> logger)
        {
            _applications = applications;
            _mag = mag;
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _snapshots = snapshots;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var staged = false;

                if (await _applications.GetAsync(cancellationToken) is null)
                {
                    var defaults = ApplicationSettingsDefaults.Create();

                    // The one-time bootstrap copy: only a non-empty configured secret replaces the
                    // empty default, and only while creating the row.
                    var configured = _configuration[
                        $"{RecaptchaOptions.SectionName}:SecretKey"]?.Trim();

                    var application = string.IsNullOrEmpty(configured)
                        ? defaults
                        : defaults with { RecaptchaSecretKey = configured! };

                    await _applications.AddAsync(application, cancellationToken);
                    staged = true;

                    _logger.LogInformation(
                        "Seeded the application settings row{Recaptcha}.",
                        string.IsNullOrEmpty(configured)
                            ? string.Empty
                            : " and copied the configured reCAPTCHA secret into it");
                }

                if (await _mag.GetAsync(cancellationToken) is null)
                {
                    await _mag.AddAsync(MagSettingsDefaults.Create(), cancellationToken);
                    staged = true;

                    _logger.LogInformation("Seeded the MAG settings row.");
                }

                if (staged)
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }

                // Primed here rather than lazily: the first guest request after a boot reads the
                // reCAPTCHA state, and a cold cache would answer the defaults for one window
                // even though the row already holds an operator's change.
                await _snapshots.ReloadAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // A shutdown is not a seed failure: the rows are left for the next boot.
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not seed the installation settings. The installation starts with the " +
                    "built-in defaults; restart to try again.");
            }
        }
    }
}
