namespace Fluxy.Core.Streams.Interfaces
{
    /// <summary>
    /// Fills an empty stream problem journal with trial rows in a Development installation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Development only, empty table only.</b> The producer that will write real rows does not
    /// exist yet, so a fresh Development database has a screen with nothing to draw and every
    /// filter and click-through untestable. This seeder is the answer to that, and to nothing
    /// else: outside Development it is a no-op, and inside Development it refuses to touch a table
    /// that already holds anything, because trial rows mixed into a real journal would be invented
    /// evidence.
    /// </para>
    /// <para>
    /// <b>The rows need a real stream.</b> The stream is a foreign key, so the seeder reads the
    /// streams this installation already has and never invents one; without at least one stream
    /// there is nothing a problem could be about, and it says so once and writes nothing.
    /// </para>
    /// </remarks>
    public interface IStreamProblemSeeder
    {
        /// <summary>
        /// Inserts about a dozen trial problems when the environment is Development, the journal
        /// is empty and the installation has a stream. Otherwise it logs one line and writes
        /// nothing.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
