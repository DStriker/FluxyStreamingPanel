using Fluxy.Core.Settings.ReadModels;

namespace Fluxy.Core.Settings.Interfaces
{
    /// <summary>
    /// Hands out the installation settings without a database round trip on every request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations cache one immutable reading of each table for
    /// <see cref="Utils.SettingsLimits.SnapshotTtl"/>, and a write reloads it eagerly. A reader
    /// therefore sees an operator's change at once and another process's change within one
    /// window.
    /// </para>
    /// <para>
    /// The properties never throw and never answer null: a repository failure leaves the
    /// previous reading in place, and a row that has never been read answers the defaults
    /// statics.
    /// </para>
    /// </remarks>
    public interface ISettingsSnapshotProvider
    {
        /// <summary>The current application settings.</summary>
        ApplicationSettingsSnapshot ApplicationSnapshot { get; }

        /// <summary>The current MAG settings.</summary>
        MagSettingsSnapshot MagSnapshot { get; }

        /// <summary>
        /// Reloads both readings, or leaves the cached ones in place when the read fails.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ReloadAsync(CancellationToken cancellationToken = default);
    }
}
