using Fluxy.Core.Servers.Models;

namespace Fluxy.Core.Servers.Utils
{
    /// <summary>
    /// The one server the installation cannot operate without: the row every other server is an
    /// addition to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Its identifier is a constant rather than a value the database invents, because three
    /// different places have to name the same row without ever having seen each other: the
    /// startup seeder that creates it, the service that refuses to delete it, and any later code
    /// that reads the installation's own address. A random identifier would leave the seeder
    /// free to invent a second "the main server" after an operator deleted the first.
    /// </para>
    /// <para>
    /// The number is deliberately readable rather than meaningful - a placeholder in a uuid, not
    /// anything derived from the row - so that a query log showing
    /// <c>00000000-0000-0000-0000-000000000401</c> is recognisable as a seeded row instead of
    /// looking like an accident.
    /// </para>
    /// <para>
    /// <b>The row cannot be deleted.</b> A server is the main one if and only if its identifier
    /// is this constant. That is the whole rule the write endpoints need: it cannot be lost by
    /// renaming, by changing any other column, or by any row state at all, however that state is
    /// written. An operator who removes it would leave the installation with no address of its
    /// own, so the deletion is refused and the refusal is named
    /// <see cref="Enums.ServerAction.ImmutableMain"/>.
    /// </para>
    /// <para>
    /// The defaults below are the values the seeder writes on a fresh installation. They are
    /// starting points an operator may change, not a contract: nothing in the domain cares what
    /// the row is called or which port it listens on.
    /// </para>
    /// </remarks>
    public static class MainServer
    {
        /// <summary>The identifier of the main server, the same on every installation.</summary>
        public static readonly Guid Id = new("00000000-0000-0000-0000-000000000401");

        /// <summary>The name the main server is created with.</summary>
        public const string DefaultName = "Main server";

        /// <summary>The client ceiling the main server is created with.</summary>
        public const int DefaultClientCount = 1000;

        /// <summary>The network interface name the main server is created with.</summary>
        public const string DefaultNetworkInterface = "eth0";

        /// <summary>The address the main server falls back to when nothing better is known.</summary>
        public const string FallbackIp = "127.0.0.1";

        /// <summary>The port the main server falls back to when nothing better is known.</summary>
        public const int FallbackPort = 5159;

        /// <summary>The operating system name the main server falls back to when nothing is known.</summary>
        public const string FallbackOsName = "unknown";

        /// <summary>
        /// Whether an identifier names the server the installation cannot operate without.
        /// </summary>
        /// <param name="id">Identifier to test.</param>
        /// <returns>True for the main server, false for every other row and for any other value.</returns>
        public static bool IsMain(Guid id) => id == Id;

        /// <summary>
        /// Whether a row is the server the installation cannot operate without.
        /// </summary>
        /// <param name="server">Row to test.</param>
        /// <returns>True for the main server, false for every other row.</returns>
        /// <remarks>
        /// The answer comes from the identifier alone, never from the stored
        /// <see cref="Server.IsMain"/> flag: the flag is a column the seeder writes, and a rule
        /// that read it could be broken by a row somebody edited by hand.
        /// </remarks>
        public static bool IsMain(Server server) => IsMain(server.Id);
    }
}
