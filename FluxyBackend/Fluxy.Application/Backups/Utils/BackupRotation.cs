using Fluxy.Core.Backups.Utils;

namespace Fluxy.Application.Backups.Utils
{
    /// <summary>
    /// Which archives a rotation removes, given how many the operator chose to keep.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Rotation is a pure decision so it can be tested without a filesystem.</b> The service
    /// enumerates the directory and deletes what this returns; the rule itself is one sentence:
    /// keep the newest <c>keep</c> pattern-matching names and remove the rest.
    /// </para>
    /// <para>
    /// <b><c>0</c> keeps everything</b> - it is the "keep all" sentinel the settings screen
    /// documents, not a request to delete every backup. <b>A name that does not match the naming
    /// rule is never touched</b>: an operator's own copy dropped beside the dumps, a stray
    /// <c>.tmp</c> or a hand-renamed file is not this feature's to remove.
    /// </para>
    /// <para>
    /// The order is lexicographic descending, which is chronological descending for this format,
    /// and a collision suffix sits before the extension - so two names of one second are ordered
    /// by the same rule as any two names, and the rotation keeps whichever the ordinal comparison
    /// puts first. Within one second the suffixed names therefore sort before the unsuffixed one;
    /// that is a consequence of the ratified format, not a second ordering rule.
    /// </para>
    /// </remarks>
    public static class BackupRotation
    {
        /// <summary>The archives a rotation deletes, oldest first.</summary>
        /// <param name="fileNames">Every name found in the directory.</param>
        /// <param name="keep">How many newest archives to keep; <c>0</c> keeps all.</param>
        /// <returns>The names to delete, oldest first; empty when nothing is to go.</returns>
        public static IReadOnlyList<string> SelectForDeletion(
            IReadOnlyCollection<string> fileNames,
            int keep)
        {
            if (keep <= 0)
            {
                return [];
            }

            var matching = fileNames
                .Where(name => BackupFileName.TryParse(name, out _))
                .OrderByDescending(name => name, StringComparer.Ordinal)
                .ToList();

            return matching.Count <= keep
                ? []
                : matching.Skip(keep).Reverse().ToList();
        }
    }
}
