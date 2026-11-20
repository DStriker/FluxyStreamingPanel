using Fluxy.Core.Servers.Models;

namespace Fluxy.Core.Servers.Interfaces
{
    /// <summary>
    /// Answers what the machine this process runs on looks like: its address, the port it serves
    /// on, the operating system and the network interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The provider is best-effort and never throws.</b> It inspects the machine it happens to
    /// run on, and that machine may have no usable interface, no resolvable address or an
    /// operating system whose name the runtime cannot state. Every such case is answered with
    /// the documented fallback of <see cref="Utils.MainServer"/> rather than an exception: a
    /// server that cannot describe itself is still a server, and a startup seeder that failed
    /// because of it would leave the installation with no row at all.
    /// </para>
    /// <para>
    /// Nothing here is a business rule. The seeder writes the answer once, as the starting point
    /// an operator may then change like any other column.
    /// </para>
    /// </remarks>
    public interface IServerIdentityProvider
    {
        /// <summary>
        /// Reads what the machine says about itself. Never throws and never returns null; an
        /// unreadable fact is replaced by the documented fallback.
        /// </summary>
        /// <returns>The identity, complete enough to become the main server row.</returns>
        ServerIdentity Read();
    }
}
