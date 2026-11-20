using System.Linq.Expressions;
using Fluxy.Core.Servers.Commands;
using Fluxy.Core.Servers.Enums;
using Fluxy.Core.Servers.Models;
using Fluxy.Core.Servers.ReadModels;
using Fluxy.Core.Servers.Repositories;
using Fluxy.Core.Servers.Utils;
using Fluxy.DataAccess.Context;
using Fluxy.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fluxy.DataAccess.Repositories
{
    /// <summary>
    /// EF Core implementation of <see cref="IServerRepository"/>.
    /// </summary>
    /// <remarks>
    /// Search covers the name, the host name and the address, because those are the three text
    /// columns a reader scans the table by. The order of every sort ends on the identifier, so
    /// two rows written in the same transaction cannot make two pages show or skip one of them.
    /// </remarks>
    internal sealed class ServerRepository : IServerRepository
    {
        private readonly FluxyDbContext _context;

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerRepository"/> class.
        /// </summary>
        /// <param name="context">Context the server rows are read from and staged in.</param>
        public ServerRepository(FluxyDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc />
        public async Task<ServerPage> GetPageAsync(
            int page,
            int pageSize,
            string? search,
            ServerSortField sortBy,
            ServerSortOrder sortOrder,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Servers.AsNoTracking();

            var needle = search?.Trim().ToLowerInvariant() ?? string.Empty;
            if (needle.Length > 0)
            {
                query = query.Where(server =>
                    server.Name.ToLower().Contains(needle)
                    || (server.Hostname ?? string.Empty).ToLower().Contains(needle)
                    || server.Ip.ToLower().Contains(needle));
            }

            // Counted under the same predicate the page is cut from, and before the order is
            // applied: a total counted after Skip/Take would describe the page, not the list.
            var total = await query.CountAsync(cancellationToken);

            query = Order(query, sortBy, sortOrder);

            var rows = await query
                .Select(server => new StoredServer
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
                })
                .Skip((int)Math.Min((long)(page - 1) * pageSize, int.MaxValue))
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new ServerPage
            {
                Items = rows.Select(ToView).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<Server?> GetAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Servers
                .AsNoTracking()
                .Where(server => server.Id == id)
                .Select(server => server.ToModel())
                .FirstOrDefaultAsync(cancellationToken);

        /// <inheritdoc />
        public Task<bool> ExistsMainAsync(CancellationToken cancellationToken = default)
            => _context.Servers
                .AsNoTracking()
                .AnyAsync(server => server.Id == MainServer.Id, cancellationToken);

        /// <inheritdoc />
        public Task<bool> IsNameTakenAsync(
            string name,
            Guid? exceptServer,
            CancellationToken cancellationToken = default)
            => _context.Servers
                .AsNoTracking()
                .AnyAsync(
                    server => server.Name == name
                        && (exceptServer == null || server.Id != exceptServer),
                    cancellationToken);

        /// <inheritdoc />
        /// <remarks>
        /// The only caller that sets <see cref="ServerWrite.Id"/> is the startup seeder, and the
        /// main-server flag is answered from that identifier rather than carried by the write:
        /// the row is the main one exactly when it is <see cref="MainServer.Id"/>. That keeps the
        /// one flag nobody may edit out of every operator's payload.
        /// </remarks>
        public Task<Guid> AddAsync(
            ServerWrite server,
            CancellationToken cancellationToken = default)
        {
            var id = server.Id ?? Guid.NewGuid();

            var entity = new ServerEntity(id) { IsMain = MainServer.IsMain(id) };

            entity.FromModel(ToModel(id, server));

            _context.Servers.Add(entity);

            return Task.FromResult(entity.Id);
        }

        /// <inheritdoc />
        public async Task<bool> UpdateAsync(
            Guid id,
            ServerWrite server,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Servers
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            // Every editable field is written, including the nulls: the patch carries the whole
            // row, so a field the operator cleared must become null rather than keep what it
            // held. FromModel leaves the identifier, the stamps and the main-server flag alone,
            // so a write can never move the one flag an operator does not own.
            entity.FromModel(ToModel(id, server));

            return true;
        }

        /// <inheritdoc />
        public async Task<bool> RemoveAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _context.Servers
                .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

            if (entity is null)
            {
                return false;
            }

            _context.Servers.Remove(entity);
            return true;
        }

        /// <summary>
        /// Shapes a resolved write as the model the row is copied from. <c>IsMain</c> is false
        /// here on purpose: the only row that carries it is the seeder's, and every other write
        /// reaches a row through <see cref="ServerEntity.FromModel"/>, which never touches it.
        /// </summary>
        /// <remarks>
        /// <see cref="Server.CreatedAt"/> is left at its default deliberately: the stamp belongs
        /// to the row the context is holding, and <see cref="ServerEntity.FromModel"/> never
        /// copies it. A fabricated instant here would be a second opinion about when the row was
        /// written.
        /// </remarks>
        private static Server ToModel(Guid id, ServerWrite server) => new()
        {
            Id = id,
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
            CountryCodes = server.CountryCodes.ToArray(),
            IsMain = false,
            CreatedAt = default
        };

        /// <summary>Projects a stored row into the shape a response is built from.</summary>
        private static ServerView ToView(StoredServer row) => new()
        {
            Id = row.Id,
            Name = row.Name,
            Ip = row.Ip,
            Port = row.Port,
            ClientCount = row.ClientCount,
            OsName = row.OsName,
            NetworkInterface = row.NetworkInterface,
            Latency = row.Latency,
            Status = row.Status,
            GuaranteedSpeedKbps = row.GuaranteedSpeedKbps,
            TimeshiftOnly = row.TimeshiftOnly,
            Hostname = row.Hostname,
            VpnIp = row.VpnIp,
            CountryCodes = row.CountryCodes,
            IsMain = row.IsMain,
            CreatedAt = row.CreatedAt
        };

        private static IQueryable<ServerEntity> Order(
            IQueryable<ServerEntity> query,
            ServerSortField field,
            ServerSortOrder order)
        {
            var descending = order is ServerSortOrder.Descending;

            return field switch
            {
                ServerSortField.Status => Chain(
                    query, server => server.Status, descending),

                ServerSortField.Latency => Chain(
                    query, server => server.Latency, descending),

                // Servers without a host name group last in either direction: a page ordered by
                // host name must not open with the ones that have none.
                ServerSortField.Hostname => ChainNullsLast(
                    query, server => server.Hostname, descending),

                ServerSortField.Ip => Chain(
                    query, server => server.Ip, descending),

                ServerSortField.ClientCount => Chain(
                    query, server => server.ClientCount, descending),

                ServerSortField.CreatedAt => Chain(
                    query, server => server.CreatedAt, descending),

                _ => Chain(query, server => server.Name, descending)
            };
        }

        /// <summary>
        /// Orders by one column and then by the identifier, which is unique and therefore makes
        /// the page boundary deterministic.
        /// </summary>
        private static IOrderedQueryable<ServerEntity> Chain<TKey>(
            IQueryable<ServerEntity> source,
            Expression<Func<ServerEntity, TKey>> primary,
            bool descending)
        {
            var ordered = descending
                ? source.OrderByDescending(primary)
                : source.OrderBy(primary);

            return descending
                ? ordered.ThenByDescending(server => server.Id)
                : ordered.ThenBy(server => server.Id);
        }

        /// <summary>Like <see cref="Chain{TKey}"/>, except that rows without a value go last.</summary>
        private static IOrderedQueryable<ServerEntity> ChainNullsLast<TKey>(
            IQueryable<ServerEntity> source,
            Expression<Func<ServerEntity, TKey>> primary,
            bool descending)
        {
            var isNull = Expression.Lambda<Func<ServerEntity, bool>>(
                Expression.Equal(
                    primary.Body,
                    Expression.Constant(null, primary.Body.Type)),
                primary.Parameters);

            var withGrouped = source.OrderBy(isNull);

            return descending
                ? withGrouped.ThenByDescending(primary).ThenByDescending(server => server.Id)
                : withGrouped.ThenBy(primary).ThenBy(server => server.Id);
        }

        /// <summary>The columns a page read draws, before they are shaped for a response.</summary>
        private sealed class StoredServer
        {
            public Guid Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public string Ip { get; init; } = string.Empty;
            public int Port { get; init; }
            public int ClientCount { get; init; }
            public string OsName { get; init; } = string.Empty;
            public string NetworkInterface { get; init; } = string.Empty;
            public double Latency { get; init; }
            public ServerStatus Status { get; init; }
            public int GuaranteedSpeedKbps { get; init; }
            public bool TimeshiftOnly { get; init; }
            public string? Hostname { get; init; }
            public string? VpnIp { get; init; }
            public string[] CountryCodes { get; init; } = [];
            public bool IsMain { get; init; }
            public DateTimeOffset CreatedAt { get; init; }
        }
    }
}
