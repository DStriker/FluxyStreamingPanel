namespace Fluxy.Core.Subscriptions.Interfaces
{
    /// <summary>
    /// Fills an empty connection log with trial rows in a Development installation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Development only, empty table only.</b> The capture path that will write real rows
    /// belongs to the relaying process and does not exist yet, so a fresh Development database
    /// has a screen with nothing to draw and every filter and click-through untestable. This
    /// seeder is the answer to that, and to nothing else: outside Development it is a no-op, and
    /// inside Development it refuses to touch a table that already holds anything, because trial
    /// rows mixed into a real log would be invented evidence.
    /// </para>
    /// <para>
    /// The rows name real subscriptions and real streams, which is why the contract lives here
    /// rather than being done with raw SQL: the foreign key on the subscription exists to refuse
    /// a fabricated identifier, and the title the screen resolves comes from a real stream row.
    /// </para>
    /// </remarks>
    public interface IConnectionLogSeeder
    {
        /// <summary>
        /// Inserts a handful of trial connections when the environment is Development, the log is
        /// empty, and the installation holds at least one subscription and one stream of each
        /// medium. Otherwise it logs one line and writes nothing.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
