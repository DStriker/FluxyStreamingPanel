using Fluxy.Application.Servers.Utils;
using Fluxy.Core.Persistence.Exceptions;
using Fluxy.Core.Persistence.Interfaces;
using Fluxy.Core.Persistence.Utils;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Interfaces;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.Outcomes;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Servers.Utils;

namespace Fluxy.Application.Servers.Services
{
    /// <summary>
    /// Reads the servers and changes them on an administrator's behalf.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A server is an address the installation may relay through, plus what the machine behind
    /// it is and how it is doing. What the service decides is what an operator may write: the
    /// trimmed text, every numeric bound the model states, the address literals, and the country
    /// list in its canonical form. What it does not decide is how a page is filtered and
    /// ordered - that is the repository's SQL shape.
    /// </para>
    /// <para>
    /// <b>Uniqueness is checked twice on purpose.</b> The pre-check answers the friendly message
    /// for the ordinary case; the named index is what actually guarantees it, because two
    /// requests can both pass the check and only one can win the insert. Both paths answer
    /// <see cref="ServerAction.AlreadyExists"/>, so a caller cannot tell which one it met - and a
    /// violation from any other index is rethrown rather than dressed up as a taken name.
    /// </para>
    /// <para>
    /// <b>The main row is editable and not deletable.</b> Its identifier is
    /// <see cref="MainServer.Id"/> and that identifier is the whole rule: every field write
    /// reaches it like any other row, and only <see cref="DeleteAsync"/> refuses. The existence
    /// check comes before that refusal, so a missing identifier answers
    /// <see cref="ServerAction.NotFound"/> rather than claiming a row that was never there is
    /// the main one.
    /// </para>
    /// <para>
    /// Nothing here knows about HTTP. Which <see cref="ServerAction"/> becomes a 201 and which a
    /// 409 is the transport layer's business.
    /// </para>
    /// </remarks>
    public sealed class ServerService : IServerService
    {
        private readonly IServerRepository _servers;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerService"/> class.
        /// </summary>
        /// <param name="servers">Repository holding the servers.</param>
        /// <param name="unitOfWork">Commits the changes one operation stages.</param>
        public ServerService(IServerRepository servers, IUnitOfWork unitOfWork)
        {
            _servers = servers;
            _unitOfWork = unitOfWork;
        }

        /// <inheritdoc />
        public Task<ServerPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            ServerSortField sortBy,
            ServerSortOrder sortOrder,
            CancellationToken cancellationToken = default)
            => _servers.GetPageAsync(page, pageSize, search, sortBy, sortOrder, cancellationToken);

        /// <inheritdoc />
        public async Task<ServerView?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var server = await _servers.GetAsync(id, cancellationToken);

            return server is null ? null : ToView(server);
        }

        /// <inheritdoc />
        public async Task<ServerOutcome> CreateAsync(
            NewServer input,
            CancellationToken cancellationToken = default)
        {
            // The three absent values are resolved before the rules run, so a create that names
            // none of them is judged as the row it will become rather than as the holes it left.
            var candidate = new ServerWrite
            {
                Name = input.Name,
                Ip = input.Ip,
                Port = input.Port,
                ClientCount = input.ClientCount ?? ServerLimits.ClientCountMin,
                OsName = input.OsName,
                NetworkInterface = input.NetworkInterface,
                Latency = ServerLimits.LatencyMin,
                Status = input.Status ?? ServerStatus.Active,
                GuaranteedSpeedKbps = input.GuaranteedSpeedKbps ?? ServerLimits.GuaranteedSpeedMinKbps,
                TimeshiftOnly = input.TimeshiftOnly,
                Hostname = input.Hostname,
                VpnIp = input.VpnIp,
                CountryCodes = input.CountryCodes ?? []
            };

            var errors = ServerRules.Validate(candidate);

            if (errors.Count > 0)
            {
                return Invalid(errors);
            }

            var write = ServerRules.Normalize(candidate);

            if (await _servers.IsNameTakenAsync(write.Name, exceptServer: null, cancellationToken))
            {
                return AlreadyExists(nameof(NewServer.Name));
            }

            var serverId = await _servers.AddAsync(write, cancellationToken);

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } createFailure)
            {
                return createFailure.IsConstraint(ServerLimits.NameIndexName)
                    ? AlreadyExists(nameof(NewServer.Name))
                    : throw createFailure;
            }

            return new ServerOutcome
            {
                Action = ServerAction.Created,
                ServerId = serverId
            };
        }

        /// <inheritdoc />
        public async Task<ServerOutcome> UpdateAsync(
            Guid id,
            ServerPatch patch,
            CancellationToken cancellationToken = default)
        {
            var server = await _servers.GetAsync(id, cancellationToken);

            if (server is null)
            {
                return NotFound(id);
            }

            // The patch is the whole row, so it is judged as it stands rather than merged with
            // what the row holds - there is no column the body may leave out.
            var candidate = new ServerWrite
            {
                Name = patch.Name,
                Ip = patch.Ip,
                Port = patch.Port,
                ClientCount = patch.ClientCount,
                OsName = patch.OsName,
                NetworkInterface = patch.NetworkInterface,
                Latency = patch.Latency,
                Status = patch.Status,
                GuaranteedSpeedKbps = patch.GuaranteedSpeedKbps,
                TimeshiftOnly = patch.TimeshiftOnly,
                Hostname = patch.Hostname,
                VpnIp = patch.VpnIp,
                CountryCodes = patch.CountryCodes
            };

            var errors = ServerRules.Validate(candidate);

            if (errors.Count > 0)
            {
                return Invalid(errors, id);
            }

            var write = ServerRules.Normalize(candidate);

            if (await _servers.IsNameTakenAsync(write.Name, id, cancellationToken))
            {
                return AlreadyExists(nameof(ServerPatch.Name), id);
            }

            // The write reports false for a row that no longer exists: a server deleted between
            // the read above and this call would otherwise commit an empty save and answer 200
            // for a record the same id's GET answers 404 for.
            if (await _servers.UpdateAsync(id, write, cancellationToken) is false)
            {
                return NotFound(id);
            }

            if (await _unitOfWork.TrySaveAsync(cancellationToken) is { } updateFailure)
            {
                return updateFailure.IsConstraint(ServerLimits.NameIndexName)
                    ? AlreadyExists(nameof(ServerPatch.Name), id)
                    : throw updateFailure;
            }

            return new ServerOutcome
            {
                Action = ServerAction.Updated,
                ServerId = id
            };
        }

        /// <inheritdoc />
        /// <remarks>
        /// The existence check comes first and the main-row refusal second, deliberately: a
        /// missing identifier is answered as the absence it is, and only a row that exists can be
        /// refused for being the one the installation cannot operate without.
        /// </remarks>
        public async Task<ServerOutcome> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            if (await _servers.GetAsync(id, cancellationToken) is null)
            {
                return NotFound(id);
            }

            if (MainServer.IsMain(id))
            {
                return new ServerOutcome
                {
                    Action = ServerAction.ImmutableMain,
                    ServerId = id
                };
            }

            if (await _servers.RemoveAsync(id, cancellationToken) is false)
            {
                return NotFound(id);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ServerOutcome
            {
                Action = ServerAction.Removed,
                ServerId = id
            };
        }

        /// <summary>Turns the stored model into the shape a response is built from.</summary>
        private static ServerView ToView(Server server) => new()
        {
            Id = server.Id,
            Name = server.Name,
            Ip = server.Ip,
            Port = server.Port,
            ClientCount = server.ClientCount,
            OsName = server.OsName,
            NetworkInterface = server.NetworkInterface,
            Latency = server.Latency,
            Status = server.Status,
            GuaranteedSpeedKbps = server.GuaranteedSpeedKbps,
            TimeshiftOnly = server.TimeshiftOnly,
            Hostname = server.Hostname,
            VpnIp = server.VpnIp,
            CountryCodes = server.CountryCodes,
            IsMain = server.IsMain,
            CreatedAt = server.CreatedAt
        };

        private static ServerOutcome NotFound(Guid id)
            => new() { Action = ServerAction.NotFound, ServerId = id };

        private static ServerOutcome AlreadyExists(string field, Guid id = default)
            => new()
            {
                Action = ServerAction.AlreadyExists,
                ServerId = id,
                Errors = Field(field, ["Another server already has that name."])
            };

        private static ServerOutcome Invalid(
            IReadOnlyDictionary<string, string[]> errors,
            Guid id = default)
            => new() { Action = ServerAction.Invalid, ServerId = id, Errors = errors };

        private static Dictionary<string, string[]> Field(string name, string[] sentences)
            => new(StringComparer.Ordinal) { [name] = sentences };
    }
}
