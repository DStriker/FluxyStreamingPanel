namespace Fluxy.Application.Services
{
    /// <summary>
    /// Brings the database up to date and makes sure the installation has an administrator to
    /// sign in with.
    /// </summary>
    public interface IUserSeeder
    {
        /// <summary>
        /// Applies the pending migrations and creates the default administrator when the
        /// installation has none. Does nothing on every later run.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}