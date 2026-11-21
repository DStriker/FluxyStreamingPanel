namespace Fluxy.Core.Settings.Interfaces
{
    /// <summary>
    /// Makes sure the installation has both settings rows, once, at startup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The migration seeds the two rows; this seeder is the second guard, for the installation
    /// whose rows were removed by hand and for the fixture that truncates the database. It is
    /// idempotent and never overwrites a row an operator has changed.
    /// </para>
    /// <para>
    /// It exists as a contract so the composition root can call it without naming an
    /// implementation, the shape every other seeder here follows.
    /// </para>
    /// </remarks>
    public interface ISettingsSeeder
    {
        /// <summary>
        /// Creates either row that is absent, and copies the configured reCAPTCHA secret into the
        /// application row on its first creation only.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
