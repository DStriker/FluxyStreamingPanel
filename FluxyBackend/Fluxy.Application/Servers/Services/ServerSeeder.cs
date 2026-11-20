using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Servers.Utils;
using Microsoft.Extensions.Logging;

namespace Fluxy.Application.Servers.Services
{
    /// <summary>
    /// Makes sure the installation has its main server row, once, at startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The row is a runtime fact rather than a schema one: the address, the port and the
    /// operating system of the machine are only known once the process is running, which is why
    /// this is a seeder instead of an insert in the migration. It is idempotent - a row that
    /// already exists is left exactly as the operator left it, never overwritten.
    /// </para>
    /// <para>
    /// <b>It never throws</b> - except for a cancelled shutdown, which is not a failure of the
    /// seed and must reach the caller rather than be logged as one. A probe that failed, a
    /// database that refused the write or a row another instance created first are all reported
    /// and then forgotten: an installation that cannot seed its main row still serves, and
    /// failing the boot over it would take the whole application down for a value an operator
    /// can type in afterwards.
    /// </para>
    /// </remarks>
    public sealed class ServerSeeder : IServerSeeder
    {
        private readonly IServerRepository _servers;
        private readonly IServerIdentityProvider _identity;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ServerSeeder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSeeder"/> class.
        /// </summary>
        /// <param name="servers">Repository the main row is written through.</param>
        /// <param name="identity">Provider of what this machine says about itself.</param>
        /// <param name="unitOfWork">Commits the seeded row.</param>
        /// <param name="logger">Logger the seed and its failures are reported to.</param>
        public ServerSeeder(
            IServerRepository servers,
            IServerIdentityProvider identity,
            IUnitOfWork unitOfWork,
            ILogger<ServerSeeder> logger)
        {
            _servers = servers;
            _identity = identity;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (await _servers.ExistsMainAsync(cancellationToken))
                {
                    return;
                }

                var identity = _identity.Read();

                var write = new ServerWrite
                {
                    Id = MainServer.Id,
                    Name = MainServer.DefaultName,
                    Ip = identity.Ip,
                    Port = identity.Port,
                    ClientCount = MainServer.DefaultClientCount,
                    OsName = identity.OperatingSystem,
                    NetworkInterface = MainServer.DefaultNetworkInterface,
                    Latency = ServerLimits.LatencyMin,
                    Status = ServerStatus.Active,
                    GuaranteedSpeedKbps = ServerLimits.GuaranteedSpeedMinKbps,
                    TimeshiftOnly = false,
                    Hostname = null,
                    VpnIp = null,
                    CountryCodes = []
                };

                await _servers.AddAsync(write, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Seeded the main server: {Ip}:{Port} on {OperatingSystem} through {NetworkInterface}.",
                    identity.Ip,
                    identity.Port,
                    identity.OperatingSystem,
                    identity.NetworkInterface);
            }
            catch (OperationCanceledException)
            {
                // A shutdown is not a seed failure: the caller cancelled the work, and the row
                // is left for the next boot rather than reported as something that went wrong.
                throw;
            }
            catch (Exception exception)
            {
                // Reported rather than rethrown: startup continues without the row, and the
                // next run tries again. The message says which failure it was.
                _logger.LogWarning(
                    exception,
                    "Could not seed the main server. The installation starts without it; add the " +
                    "server by hand or restart to try again.");
            }
        }
    }
}
