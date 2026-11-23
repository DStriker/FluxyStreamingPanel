namespace Fluxy.Core.Backups.Options
{
    /// <summary>
    /// How the backup feature reaches the database and where it keeps the archives.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bound from the <c>Backups</c> configuration section. The type lives in Core rather than in
    /// the layer that reads it because two layers read it: the application service that writes
    /// and rotates the files, and the data access tool that runs <c>docker exec</c> against the
    /// container. It is the same reason <c>AuthenticationOptions</c> lives here.
    /// </para>
    /// <para>
    /// <see cref="Directory"/> is resolved against the API host's content root by
    /// <c>Program.cs</c> before the options instance is registered, so every consumer sees an
    /// absolute path and no service has to know where the host was started from.
    /// </para>
    /// </remarks>
    public sealed class BackupsOptions
    {
        /// <summary>Configuration section these values are bound from.</summary>
        public const string SectionName = "Backups";

        /// <summary>Name of the Docker container that runs PostgreSQL.</summary>
        public string ContainerName { get; set; } = "postgres";

        /// <summary>Directory the archives are written to; an absolute path once wired.</summary>
        public string Directory { get; set; } = "backups";

        /// <summary>How long the scheduler waits between two due-check ticks.</summary>
        public int TickSeconds { get; set; } = 60;
    }
}
