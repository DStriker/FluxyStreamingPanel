namespace Fluxy.Core.Models.Users
{
    /// <summary>
    /// The three groups that must exist, always, under any circumstances: one for each role,
    /// each holding every permission its role has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Their identifiers are constants rather than values the database invents, because
    /// three different places have to name the same rows without ever having seen each
    /// other: the migration that backfills existing accounts onto them, the startup seeder
    /// that reconciles them with the catalog, and the service that refuses to delete them.
    /// A random identifier would mean the seeder and the migration would each be free to
    /// create their own "the admin group", and an installation would end up with two of
    /// them.
    /// </para>
    /// <para>
    /// The numbers are deliberately readable rather than meaningful - they are placeholders
    /// in a uuid, not anything derived from the role - so that a query log showing
    /// <c>00000000-0000-0000-0000-000000000003</c> is recognisable as a seeded row instead
    /// of looking like an accident.
    /// </para>
    /// <para>
    /// A group is a base group if and only if its identifier is one of these three. That is
    /// the whole rule the write endpoints need: it cannot be lost by renaming, by changing
    /// the role, or by any row state at all, which is what "renameable and nothing else" is
    /// supposed to guarantee.
    /// </para>
    /// </remarks>
    public static class BaseUserGroups
    {
        /// <summary>The base group every client is a member of by default.</summary>
        public static readonly Guid Clients =
            Guid.Parse("00000000-0000-0000-0000-000000000001");

        /// <summary>The base group every reseller is a member of by default.</summary>
        public static readonly Guid Resellers =
            Guid.Parse("00000000-0000-0000-0000-000000000002");

        /// <summary>The base group every administrator is a member of by default.</summary>
        public static readonly Guid Administrators =
            Guid.Parse("00000000-0000-0000-0000-000000000003");

        /// <summary>
        /// The three identifiers, in the order <see cref="UserRole"/> declares its members.
        /// </summary>
        public static IReadOnlyList<Guid> All { get; } = [Clients, Resellers, Administrators];

        /// <summary>
        /// Whether an identifier names one of the three groups the installation cannot
        /// operate without.
        /// </summary>
        /// <param name="id">Identifier to test.</param>
        /// <returns>True for a base group, false for every other group and for any other value.</returns>
        public static bool IsBase(Guid id) => id == Clients || id == Resellers || id == Administrators;

        /// <summary>The base group of a given role.</summary>
        /// <param name="role">Role whose base group is wanted.</param>
        /// <returns>The identifier of that group.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="role"/> names no member of the enum - including the zero value,
        /// which is not a level of access this system has.
        /// </exception>
        public static Guid ForRole(UserRole role) => role switch
        {
            UserRole.Client => Clients,
            UserRole.Reseller => Resellers,
            UserRole.Admin => Administrators,
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "There is no base group for that role.")
        };

        /// <summary>
        /// The name each base group is created with. A starting point rather than a
        /// contract: the admin may rename all three, and none of the rules above care what
        /// they are called.
        /// </summary>
        public static string NameOf(UserRole role) => role switch
        {
            UserRole.Client => "Clients",
            UserRole.Reseller => "Resellers",
            UserRole.Admin => "Administrators",
            _ => throw new ArgumentOutOfRangeException(
                nameof(role),
                role,
                "There is no base group for that role.")
        };
    }
}
