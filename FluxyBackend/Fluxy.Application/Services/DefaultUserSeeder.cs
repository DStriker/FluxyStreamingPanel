using Fluxy.Core.Abstractions;
using Fluxy.Core.Models.Users;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Fluxy.Application.Services
{
    /// <summary>
    /// Applies the migrations and creates the first administrator of an installation.
    /// </summary>
    /// <remarks>
    /// The password is generated at run time, hashed, and reported once in the log. Nothing
    /// stores it in clear text - not the repository, not the database - which is the reason
    /// this is a service and not <c>HasData</c> inside the migration: a migration is a file in
    /// git, and a bootstrap password baked into one stays readable there forever.
    /// </remarks>
    public sealed class DefaultUserSeeder : IUserSeeder
    {
        /// <summary>Login name of the generated account.</summary>
        public const string DefaultUsername = "admin";

        /// <summary>Email address of the generated account.</summary>
        public const string DefaultEmail = "admin@localhost";

        /// <summary>Number of characters in a generated password.</summary>
        public const int PasswordLength = 20;

        // Visually ambiguous characters are left out: the password is read from a console
        // line and retyped into a login form, so l/1/I and 0/O would cost support time.
        private const string PasswordAlphabet =
            "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private readonly FluxyDbContext _context;
        private readonly ILogger<DefaultUserSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultUserSeeder"/> class.
        /// </summary>
        /// <param name="context">Context used to apply the migrations and insert the account.</param>
        /// <param name="logger">Logger the credentials are reported to.</param>
        public DefaultUserSeeder(FluxyDbContext context, ILogger<DefaultUserSeeder> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            // A no-op once the schema is current. Doing it here is what makes the install a
            // single step: "docker compose up" plus "dotnet run" is enough.
            await _context.Database.MigrateAsync(cancellationToken);

            var hasAdmin = await _context.Users
                .AnyAsync(user => user.Role == UserRole.Admin, cancellationToken);

            if (hasAdmin)
            {
                return;
            }

            var password = GeneratePassword();

            _context.Users.Add(new UserEntity
            {
                Username = DefaultUsername,
                Email = DefaultEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = UserRole.Admin,
                Status = UserStatus.Registered,
                RegisteredAt = DateTimeOffset.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created the default administrator for this installation.{NewLine}  Username: {Username}{NewLine}  Password: {Password}{NewLine}The password is shown once and is stored nowhere - save it now.",
                Environment.NewLine,
                DefaultUsername,
                Environment.NewLine,
                password,
                Environment.NewLine);
        }

        /// <summary>
        /// Builds a random password out of <see cref="PasswordAlphabet"/>.
        /// </summary>
        private static string GeneratePassword()
        {
            var characters = RandomNumberGenerator.GetItems(
                PasswordAlphabet.AsSpan(),
                PasswordLength);

            return new string(characters);
        }
    }
}