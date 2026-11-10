namespace Fluxy.Core.Requests.Interfaces
{
    /// <summary>
    /// Fills an empty API request failure log with trial rows in a Development installation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Development only, empty table only.</b> The capture path that will write real rows
    /// belongs to the future public API and does not exist yet, so a fresh Development database
    /// has a screen with nothing to draw and every filter and click-through untestable. This
    /// seeder is the answer to that, and to nothing else: outside Development it is a no-op, and
    /// inside Development it refuses to touch a table that already holds anything, because trial
    /// rows mixed into a real log would be invented evidence.
    /// </para>
    /// <para>
    /// The rows need no prerequisite of the installation - the columns are snapshots and carry no
    /// foreign key - which is why this seeder can write them without reading anything first.
    /// </para>
    /// </remarks>
    public interface IApiRequestFailureSeeder
    {
        /// <summary>
        /// Inserts a dozen trial refusals when the environment is Development and the log is
        /// empty. Otherwise it logs one line and writes nothing.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
