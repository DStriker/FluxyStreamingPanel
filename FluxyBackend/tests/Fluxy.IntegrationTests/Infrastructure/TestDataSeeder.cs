using Fluxy.Application.Users.Utils;
using Fluxy.Core.Content.Commands;
using Fluxy.Core.Content.Enums;
using Fluxy.Core.Content.Repositories;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Streams.Commands;
using Fluxy.Core.Streams.Enums;
using Fluxy.Core.Streams.Repositories;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Repositories;
using Fluxy.Core.Users.Commands;
using Fluxy.Core.Users.Enums;
using Fluxy.Core.Users.Repositories;
using Fluxy.Core.Users.Utils;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Fluxy.IntegrationTests.Infrastructure
{
    /// <summary>
    /// Creates the rows a test starts from without going through the API.
    /// </summary>
    public static class TestDataSeeder
    {
        /// <summary>
        /// Recreates the three groups the installation cannot operate without, with the permissions
        /// the catalog assigns to their roles.
        /// </summary>
        public static async Task EnsureBaseGroupsAsync(
            IServiceProvider services,
            CancellationToken cancellationToken = default)
        {
            var groups = services.GetRequiredService<IUserGroupRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            await EnsureBaseGroupsAsync(groups, unitOfWork, cancellationToken);
        }

        /// <summary>
        /// Recreates the three base groups through repositories already resolved from a scope.
        /// </summary>
        public static async Task EnsureBaseGroupsAsync(
            IUserGroupRepository groups,
            IUnitOfWork unitOfWork,
            CancellationToken cancellationToken = default)
        {
            foreach (var role in new[] { UserRole.Client, UserRole.Reseller, UserRole.Admin })
            {
                var id = BaseUserGroups.ForRole(role);
                var wanted = UserPermissionCatalog.ForRole(role).ToHashSet();
                var existing = await groups.GetSummaryAsync(id, cancellationToken);

                if (existing is null)
                {
                    await groups.AddWithPermissionsAsync(
                        new NewUserGroup
                        {
                            Id = id,
                            Name = BaseUserGroups.NameOf(role),
                            Role = role,
                            Status = UserStatus.Registered
                        },
                        wanted,
                        cancellationToken);

                    continue;
                }

                if (existing.Role != role)
                {
                    await groups.SetRoleAsync(id, role, cancellationToken);
                }

                if (existing.Status != UserStatus.Registered)
                {
                    await groups.SetStatusAsync(id, UserStatus.Registered, cancellationToken);
                }

                var current = (await groups.GetPermissionsAsync(id, cancellationToken)).ToHashSet();
                if (!current.SetEquals(wanted))
                {
                    await groups.ReplacePermissionsAsync(id, wanted, cancellationToken);
                }
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Creates an account in its role's base group, or in <paramref name="groupId"/> when one is
        /// named. The password is hashed with a low cost factor; only tests ever read it.
        /// </summary>
        public static async Task<Guid> CreateUserAsync(
            IServiceProvider services,
            string username,
            string password,
            UserRole role,
            UserStatus status = UserStatus.Registered,
            Guid? groupId = null,
            string? email = null,
            string? timeZone = null,
            bool geoProtectionEnabled = false,
            bool bindSessionToIp = false,
            CancellationToken cancellationToken = default)
        {
            var users = services.GetRequiredService<IUserRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();
            var now = DateTimeOffset.UtcNow;

            var id = await users.AddAsync(
                new NewAccount
                {
                    Username = username,
                    Email = EmailNormalizer.Normalize(email ?? $"{username}@example.test"),
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
                    GroupId = groupId ?? BaseUserGroups.ForRole(role),
                    Status = status,
                    RegisteredAt = status is UserStatus.Unregistered ? null : now,
                    TimeZone = timeZone,
                    GeoProtectionEnabled = geoProtectionEnabled,
                    BindSessionToIp = bindSessionToIp
                },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Creates a custom group with the permissions the role is allowed to hold.
        /// </summary>
        public static async Task<Guid> CreateGroupAsync(
            IServiceProvider services,
            string name,
            UserRole role,
            UserStatus status = UserStatus.Registered,
            CancellationToken cancellationToken = default,
            params UserPermission[] permissions)
        {
            var groups = services.GetRequiredService<IUserGroupRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var wanted = permissions
                .Where(permission => UserPermissionCatalog.IsValidForRole(permission, role))
                .ToHashSet();

            var id = await groups.AddWithPermissionsAsync(
                new NewUserGroup
                {
                    Name = name,
                    Role = role,
                    Status = status
                },
                wanted,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Creates a subscription owned by <paramref name="ownerId"/> and issued by
        /// <paramref name="issuerId"/>, carrying a playlist and no bouquets.
        /// </summary>
        /// <remarks>
        /// Planted through the Core repository, like every other row this class writes. The
        /// service's minimum-one-bouquet rule is a rule of the service, not of the table, and a
        /// test that only needs an owner for a connection has no reason to walk the bouquet
        /// surface to get one.
        /// </remarks>
        public static async Task<Guid> CreateSubscriptionAsync(
            IServiceProvider services,
            Guid ownerId,
            Guid issuerId,
            CancellationToken cancellationToken = default)
        {
            var subscriptions = services.GetRequiredService<ISubscriptionRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var id = await subscriptions.AddAsync(
                new NewSubscription
                {
                    UserId = ownerId,
                    ResellerId = issuerId,
                    PlaylistId = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()),
                    BouquetIds = []
                },
                [],
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Creates a video stream of the installation's own, under a category of the kind.
        /// </summary>
        /// <remarks>
        /// A connection names its stream by an opaque identifier with no foreign key, so the title
        /// a page draws is resolved from this row when it exists - which is exactly what these
        /// helpers exist to plant, and why the category has to exist first.
        /// </remarks>
        public static async Task<Guid> CreateVideoStreamAsync(
            IServiceProvider services,
            string name,
            CancellationToken cancellationToken = default)
        {
            var categories = services.GetRequiredService<IContentCategoryRepository>();
            var streams = services.GetRequiredService<IVideoStreamRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var categoryId = await categories.AddAsync(
                new NewContentCategory
                {
                    Name = $"Video {Guid.NewGuid():N}",
                    Type = ContentType.VideoStream
                },
                1,
                cancellationToken);

            var id = await streams.AddAsync(
                new VideoStreamWrite
                {
                    Name = name,
                    Type = VideoStreamType.Permanent,
                    Status = VideoStreamStatus.Running,
                    CategoryId = categoryId,
                    DelayMinutes = 0,
                    ArchiveDays = 0,
                    VodItems = [],
                    Sources = ["http://example.test/stream"],
                    Files = []
                },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>Creates an audio stream of the installation's own, under a category of the kind.</summary>
        public static async Task<Guid> CreateAudioStreamAsync(
            IServiceProvider services,
            string name,
            CancellationToken cancellationToken = default)
        {
            var categories = services.GetRequiredService<IContentCategoryRepository>();
            var streams = services.GetRequiredService<IAudioStreamRepository>();
            var unitOfWork = services.GetRequiredService<IUnitOfWork>();

            var categoryId = await categories.AddAsync(
                new NewContentCategory
                {
                    Name = $"Audio {Guid.NewGuid():N}",
                    Type = ContentType.AudioStream
                },
                1,
                cancellationToken);

            var id = await streams.AddAsync(
                new AudioStreamWrite
                {
                    Name = name,
                    Type = AudioStreamType.OnDemand,
                    Status = AudioStreamStatus.Running,
                    CategoryId = categoryId,
                    Sources = ["http://example.test/audio"]
                },
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return id;
        }

        /// <summary>
        /// Plants one connection log row directly, and returns its identifier.
        /// </summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="subscriptionId">Subscription the connection was made under.</param>
        /// <param name="streamKind">Which medium it delivered.</param>
        /// <param name="streamId">Opaque identifier of the stream.</param>
        /// <param name="startedAt">When the session opened.</param>
        /// <param name="endedAt">When it finished, or null while it is live.</param>
        /// <param name="ip">Address the session came from.</param>
        /// <param name="userAgent">User agent the session presented, or null.</param>
        /// <param name="countryCode">Stored upper-case country, or null.</param>
        /// <param name="isp">Stored provider, or null.</param>
        /// <param name="broadcastPid">Reported process identifier, or null.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <remarks>
        /// <para>
        /// <b>A deliberate SQL fixture.</b> A connection log row has no capture path in this pass -
        /// the process that relays a stream will own it - and the row itself carries nothing but
        /// stored values, so it is written as a plain statement naming exactly the columns the
        /// assertion reads.
        /// </para>
        /// <para>
        /// The insert intent that does exist, <see cref="IConnectionLogRepository.AddRangeAsync"/>,
        /// cannot serve this fixture: it answers <c>int</c> - how many rows it staged - and the row's
        /// identifier is generated inside the entity, so the caller never learns the id a test has
        /// to assert a filtered page against. Its remarks also place it with the Development trial
        /// seeder, and no production endpoint reaches it. The statement below is what gives the
        /// test the id it needs.
        /// </para>
        /// <para>
        /// It names the table and its columns, exactly as the expired-registration-code fixture
        /// names <c>users</c>, and never an entity, a context or any other persistence type - the
        /// rule the test project follows. Everything the row points at - the account, the
        /// subscription, the stream - is planted through its Core repository above.
        /// </para>
        /// </remarks>
        public static async Task<Guid> PlantConnectionLogAsync(
            NpgsqlConnection connection,
            Guid subscriptionId,
            ConnectionStreamKind streamKind,
            Guid streamId,
            DateTimeOffset startedAt,
            DateTimeOffset? endedAt = null,
            string ip = "203.0.113.9",
            string? userAgent = "Mozilla/5.0 (Test)",
            string? countryCode = "DE",
            string? isp = "Example ISP",
            int? broadcastPid = null,
            CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();

            await using var command = new NpgsqlCommand(
                "insert into connection_logs " +
                "(id, subscription_id, stream_kind, stream_id, ip, user_agent, country_code, " +
                "isp, started_at, ended_at, broadcast_pid, created_at, updated_at) " +
                "values (@id, @subscription_id, @stream_kind, @stream_id, @ip, @user_agent, " +
                "@country_code, @isp, @started_at, @ended_at, @broadcast_pid, @now, @now)",
                connection);

            var now = DateTimeOffset.UtcNow;

            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("subscription_id", subscriptionId);
            command.Parameters.AddWithValue("stream_kind", (short)streamKind);
            command.Parameters.AddWithValue("stream_id", streamId);
            command.Parameters.AddWithValue("ip", ip);
            command.Parameters.AddWithValue("user_agent", (object?)userAgent ?? DBNull.Value);
            command.Parameters.AddWithValue("country_code", (object?)countryCode ?? DBNull.Value);
            command.Parameters.AddWithValue("isp", (object?)isp ?? DBNull.Value);
            command.Parameters.AddWithValue("started_at", startedAt);
            command.Parameters.AddWithValue("ended_at", (object?)endedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("broadcast_pid", (object?)broadcastPid ?? DBNull.Value);
            command.Parameters.AddWithValue("now", now);

            await command.ExecuteNonQueryAsync(cancellationToken);

            return id;
        }

        /// <summary>Counts the connection log rows, so a test can prove a refusal wrote nothing.</summary>
        /// <param name="connection">Open connection to the test database.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        public static async Task<int> CountConnectionLogsAsync(
            NpgsqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            await using var command = new NpgsqlCommand(
                "select count(*) from connection_logs",
                connection);

            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
    }
}
