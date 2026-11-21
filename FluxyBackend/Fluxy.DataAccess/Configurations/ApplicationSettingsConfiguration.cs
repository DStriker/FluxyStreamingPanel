using Fluxy.Core.Persistence.Base;
using Fluxy.Core.Settings.Utils;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="ApplicationSettingsEntity"/> to the <c>application_settings</c> table. It is
    /// discovered by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    /// <remarks>
    /// The table holds exactly one row: the settings are the installation's, not an operator's.
    /// Every column is non-nullable and no default is declared, per the project rule; the
    /// defaults live in <see cref="ApplicationSettingsDefaults"/> and are written explicitly.
    /// </remarks>
    public sealed class ApplicationSettingsConfiguration
        : IEntityTypeConfiguration<ApplicationSettingsEntity>
    {
        /// <summary>Name of the table.</summary>
        public const string TableName = "application_settings";

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<ApplicationSettingsEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(settings => settings.Id);
            builder.Property(settings => settings.Id).HasColumnName("id");

            builder.Property(settings => settings.ServiceName)
                .HasColumnName("service_name")
                .HasMaxLength(SettingsLimits.ServiceNameMaxLength)
                .IsRequired();

            builder.Property(settings => settings.DefaultTimeZone)
                .HasColumnName("default_timezone")
                .HasMaxLength(SettingsLimits.DefaultTimeZoneMaxLength)
                .IsRequired();

            builder.Property(settings => settings.Enigma2BouquetName)
                .HasColumnName("enigma2_bouquet_name")
                .HasMaxLength(SettingsLimits.Enigma2BouquetNameMaxLength)
                .IsRequired();

            builder.Property(settings => settings.RecaptchaSiteKey)
                .HasColumnName("recaptcha_site_key")
                .HasMaxLength(SettingsLimits.RecaptchaKeyMaxLength)
                .IsRequired();

            builder.Property(settings => settings.RecaptchaSecretKey)
                .HasColumnName("recaptcha_secret_key")
                .HasMaxLength(SettingsLimits.RecaptchaKeyMaxLength)
                .IsRequired();

            builder.Property(settings => settings.EnableTrials)
                .HasColumnName("enable_trials")
                .IsRequired();

            // Zero is "unlimited", a real state rather than a missing value.
            builder.Property(settings => settings.PublicApiFloodLimit)
                .HasColumnName("public_api_flood_limit")
                .IsRequired();

            // A list stored as a list, the shape the servers' country codes use.
            builder.Property(settings => settings.PublicApiFloodWhitelist)
                .HasColumnName("public_api_flood_whitelist")
                .HasColumnType("text[]")
                .IsRequired();

            builder.Property(settings => settings.RequireUserAgent)
                .HasColumnName("require_user_agent")
                .IsRequired();

            builder.Property(settings => settings.RetentionUserLogs)
                .HasColumnName("retention_user_logs")
                .IsRequired();

            builder.Property(settings => settings.RetentionCreditLogs)
                .HasColumnName("retention_credit_logs")
                .IsRequired();

            builder.Property(settings => settings.RetentionConnectionLogs)
                .HasColumnName("retention_connection_logs")
                .IsRequired();

            builder.Property(settings => settings.RetentionApiRequestFailures)
                .HasColumnName("retention_api_request_failures")
                .IsRequired();

            builder.Property(settings => settings.RetentionStreamProblems)
                .HasColumnName("retention_stream_problems")
                .IsRequired();

            builder.Property(settings => settings.RetentionMagEvents)
                .HasColumnName("retention_mag_events")
                .IsRequired();

            builder.Property(settings => settings.BackupEnabled)
                .HasColumnName("backup_enabled")
                .IsRequired();

            // Stored as smallint, like every other enum here: the members are states, not a scale.
            builder.Property(settings => settings.BackupPeriod)
                .HasColumnName("backup_period")
                .HasConversion<short>()
                .IsRequired();

            // Zero is "keep every backup", a real state rather than a missing value.
            builder.Property(settings => settings.BackupRotationCount)
                .HasColumnName("backup_rotation_count")
                .IsRequired();

            builder.Property(settings => settings.FallbackStreamDownUrl)
                .HasColumnName("fallback_stream_down_url")
                .HasMaxLength(SettingsLimits.FallbackUrlMaxLength)
                .IsRequired();

            builder.Property(settings => settings.FallbackUserBannedUrl)
                .HasColumnName("fallback_user_banned_url")
                .HasMaxLength(SettingsLimits.FallbackUrlMaxLength)
                .IsRequired();

            builder.Property(settings => settings.FallbackUserExpiredUrl)
                .HasColumnName("fallback_user_expired_url")
                .HasMaxLength(SettingsLimits.FallbackUrlMaxLength)
                .IsRequired();

            builder.Property(settings => settings.FallbackMaxConnectionsUrl)
                .HasColumnName("fallback_max_connections_url")
                .HasMaxLength(SettingsLimits.FallbackUrlMaxLength)
                .IsRequired();

            builder.Property(nameof(AuditableEntity.CreatedAt)).HasColumnName("created_at");
            builder.Property(nameof(AuditableEntity.UpdatedAt)).HasColumnName("updated_at");
        }
    }
}
