using Fluxy.Core.Abstractions;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fluxy.DataAccess.Configurations
{
    /// <summary>
    /// Maps <see cref="RefreshTokenEntity"/> to the <c>refresh_tokens</c> table. It is discovered
    /// by <c>FluxyDbContext.OnModelCreating</c>, so no registration is needed.
    /// </summary>
    public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshTokenEntity>
    {
        /// <summary>
        /// Name of the table. Tables use the plural form of the entity name, columns use
        /// snake_case, and both live in the default schema of the database.
        /// </summary>
        public const string TableName = "refresh_tokens";

        /// <summary>
        /// Length of the stored hash. SHA-256 rendered as hexadecimal is always 64 characters,
        /// so the column is sized for exactly that and nothing else fits.
        /// </summary>
        public const int TokenHashLength = 64;

        /// <summary>
        /// Longest address text kept on a row. 45 is the longest IPv6 form including the scope
        /// and the IPv4-mapped prefix, so a value longer than this was not produced by a socket
        /// and is truncated rather than rejected - the column records where a session came from
        /// and losing the tail of a malformed address costs nothing.
        /// </summary>
        public const int ClientAddressMaxLength = 45;

        /// <summary>
        /// Longest user agent kept on a row. The value is informational and every real agent
        /// fits; an unusually long one is cut rather than refused, because refusing to open a
        /// session over it would be a worse outcome than storing a shortened version.
        /// </summary>
        public const int UserAgentMaxLength = 255;

        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<RefreshTokenEntity> builder)
        {
            builder.ToTable(TableName);

            builder.HasKey(token => token.Id);
            builder.Property(token => token.Id)
                .HasColumnName("id");

            builder.Property(token => token.UserId)
                .HasColumnName("user_id")
                .IsRequired();

            builder.Property(token => token.TokenHash)
                .HasColumnName("token_hash")
                .HasMaxLength(TokenHashLength)
                .IsRequired();

            builder.Property(token => token.SessionId)
                .HasColumnName("session_id")
                .IsRequired();

            builder.Property(token => token.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            builder.Property(token => token.RevokedAt)
                .HasColumnName("revoked_at");

            builder.Property(token => token.ReplacedById)
                .HasColumnName("replaced_by_id");

            builder.Property(token => token.ClientAddress)
                .HasColumnName("client_address")
                .HasMaxLength(ClientAddressMaxLength);

            builder.Property(token => token.UserAgent)
                .HasColumnName("user_agent")
                .HasMaxLength(UserAgentMaxLength);

            // Audit columns inherited from AuditableEntity, listed explicitly like every other
            // column for the same reason they are on the users table.
            builder.Property(nameof(AuditableEntity.CreatedAt))
                .HasColumnName("created_at");

            builder.Property(nameof(AuditableEntity.UpdatedAt))
                .HasColumnName("updated_at");

            // The lookup every refresh performs. Unique as well as indexed, because two tokens
            // hashing to the same value would mean the generator produced a collision, and
            // silently accepting the second one would hand one account another account's session.
            builder.HasIndex(token => token.TokenHash)
                .IsUnique();

            // Ending a session revokes every row of one chain, which is the only query that
            // walks the session, so it is the second index that has to exist.
            builder.HasIndex(token => token.SessionId);

            // Listing or purging the sessions of one account. Signing out everywhere, and any
            // future cleanup, both need it.
            builder.HasIndex(token => token.UserId);

            // No HasDefaultValue anywhere on purpose, for the reason it holds on every other
            // table: a value the database invents when the application forgot a column hides the
            // mistake instead of surfacing it.
        }
    }
}
