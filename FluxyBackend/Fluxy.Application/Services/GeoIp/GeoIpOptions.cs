namespace Fluxy.Application.Services.GeoIp
{
    /// <summary>
    /// Where the GeoIP databases live. Bound from the <c>GeoIp</c> section.
    /// </summary>
    /// <remarks>
    /// The files are MaxMind <c>.mmdb</c> databases, downloaded with a license key and kept out
    /// of git next to the compose secrets. Paths are filesystem paths as this process sees them,
    /// so a containerized host needs the files mounted where these point.
    ///
    /// A missing file is not a startup error: the resolver answers <c>Unknown</c> for every
    /// address and logs a warning, and accounts with the guard on are refused rather than let
    /// through - failing open would silently switch the protection off the moment a file went
    /// stale. What must never happen is a sign-in path that throws over a database file.
    /// </remarks>
    public sealed class GeoIpOptions
    {
        /// <summary>Configuration section this type is bound from.</summary>
        public const string SectionName = "GeoIp";

        /// <summary>
        /// Filesystem path of the city database (country lookup), or null when not installed.
        /// </summary>
        /// <remarks>
        /// A relative path is read against the API output folder, which is where the build
        /// copies the solution level <c>.mmdb</c> files - so a bare file name works from any
        /// working directory, and an absolute path keeps working for a mounted volume.
        /// </remarks>
        public string? CityDbPath { get; set; }

        /// <summary>
        /// Filesystem path of the ASN database (provider lookup), or null when not installed.
        /// </summary>
        /// <remarks>
        /// Relative paths resolve the same way <see cref="CityDbPath"/> does.
        /// </remarks>
        public string? AsnDbPath { get; set; }
    }
}
