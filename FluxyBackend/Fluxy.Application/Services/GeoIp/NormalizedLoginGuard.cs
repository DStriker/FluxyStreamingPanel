namespace Fluxy.Application.Services.GeoIp
{
    /// <summary>
    /// A login guard after validation: both switches and the three lists in the exact form the
    /// database stores them, with every value already canonical.
    /// </summary>
    /// <remarks>
    /// The shape deliberately mirrors <c>LoginGuardSettings</c> property for property, because
    /// one type used to serve both halves of that work - the request that is checked and the
    /// staged copy that is written into <c>pending_changes.payload</c> and read back on
    /// confirmation. It is a separate type rather than the settings themselves so that a value
    /// that has been validated cannot be confused with one that has not: an unchecked guard is
    /// a <c>LoginGuardSettings</c>, a checked one is a <c>NormalizedLoginGuard</c>, and the
    /// two are only ever produced from one another by the normalizer.
    ///
    /// The property names are load-bearing in one place: the staged copy is serialized as JSON
    /// and read back on confirmation, so renaming a property would make every row already
    /// waiting for a code unreadable.
    /// </remarks>
    public sealed record NormalizedLoginGuard(
        bool GeoProtectionEnabled,
        bool BindSessionToIp,
        string[] AllowedIps,
        string? AllowedCountry,
        int? AllowedAutonomousSystemNumber);
}
