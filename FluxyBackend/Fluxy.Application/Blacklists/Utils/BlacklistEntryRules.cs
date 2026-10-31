using System.Net;
using Fluxy.Core.Blacklists.Commands;
using Fluxy.Core.Blacklists.Enums;
using Fluxy.Core.Blacklists.Utils;

namespace Fluxy.Application.Blacklists.Utils
{
    /// <summary>
    /// The rules and sentences every write to a blacklist entry shares.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A create and a patch reach the same row through different requests, so the rules that
    /// are a fact about the row rather than about the request live here once. What is not here
    /// is what a request alone decides: a create names the list and a patch resolves "absent
    /// means keep", and both stay with the service.
    /// </para>
    /// <para>
    /// The server is the authority here, exactly as it is for the guide sources: the frontend
    /// mirrors these three rules for instant feedback, and a request that gets past the mirror
    /// is judged again in this layer.
    /// </para>
    /// </remarks>
    public static class BlacklistEntryRules
    {
        /// <summary>The sentence a value that is blank or too long gets.</summary>
        public static readonly string[] ValueRule =
        [
            "The value must be between 1 and " + BlacklistEntryLimits.ValueMaxLength +
                " characters."
        ];

        /// <summary>The sentence an <c>ip</c> value that is not an address gets.</summary>
        public static readonly string[] IpValueRule =
        [
            "An IP blacklist entry must be a valid IPv4 or IPv6 address."
        ];

        /// <summary>The sentence a note that is too long gets.</summary>
        public static readonly string[] NoteRule =
        [
            "The note must be at most " + BlacklistEntryLimits.NoteMaxLength + " characters."
        ];

        /// <summary>
        /// The unique index making a value unique within its list. Matched by exact name when
        /// the database refuses the write, because the service's own pre-check is only the
        /// friendly path - the index is the guarantee.
        /// </summary>
        public const string TypeValueIndexName = "ux_blacklist_entries_type_value";

        /// <summary>Whether a value is non-blank and short enough to store.</summary>
        /// <param name="value">Value to judge.</param>
        public static bool IsUsableValue(string value)
            => value.Trim().Length is >= 1 and <= BlacklistEntryLimits.ValueMaxLength;

        /// <summary>
        /// Whether a value can be blacklisted as an address: a single IPv4 or IPv6 literal.
        /// </summary>
        /// <remarks>
        /// <see cref="IPAddress.TryParse"/> covers both families and refuses everything else,
        /// which is the whole rule. Subnet and CIDR semantics are deliberately out of scope in
        /// this pass - an operator who wants a range describes it in the note until the
        /// enforcement understands ranges.
        /// </remarks>
        /// <param name="value">Value to judge.</param>
        public static bool IsAddress(string value)
            => IPAddress.TryParse(value.Trim(), out _);

        /// <summary>Whether a note is short enough to store, or absent.</summary>
        /// <param name="note">Note to judge, possibly null.</param>
        public static bool IsUsableNote(string? note)
            => note is null || note.Length <= BlacklistEntryLimits.NoteMaxLength;

        /// <summary>
        /// Checks everything about an entry that can be judged without asking the database.
        /// </summary>
        /// <remarks>
        /// The keys are the C# property names shared by <see cref="NewBlacklistEntry"/> and
        /// <see cref="BlacklistEntryPatch"/> for these fields - <c>Value</c> and <c>Note</c> -
        /// and the transport normalizes them to camelCase for the client.
        /// </remarks>
        /// <param name="type">Which of the three lists the entry belongs to.</param>
        /// <param name="value">The value that is refused.</param>
        /// <param name="note">Free-text note, possibly null.</param>
        public static Dictionary<string, string[]> Validate(
            BlacklistType type,
            string value,
            string? note)
        {
            var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

            if (!IsUsableValue(value))
            {
                errors[nameof(NewBlacklistEntry.Value)] = ValueRule;
            }
            else if (type is BlacklistType.Ip && !IsAddress(value))
            {
                // Reported under the value field as well: what broke is the value, and the
                // sentence says which rule it broke rather than repeating the length one.
                errors[nameof(NewBlacklistEntry.Value)] = IpValueRule;
            }

            if (!IsUsableNote(note))
            {
                errors[nameof(NewBlacklistEntry.Note)] = NoteRule;
            }

            return errors;
        }
    }
}
