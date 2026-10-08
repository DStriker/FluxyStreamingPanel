namespace Fluxy.Application.Services
{
    /// <summary>
    /// The rule a display time zone has to pass before it is stored.
    /// </summary>
    /// <remarks>
    /// Lives between the two services that accept a zone - the profile, where a visitor picks
    /// their own, and the admin form, where an operator picks somebody else's - because those
    /// two refusing different identifiers would be the same field saying two things depending
    /// on who was standing at it.
    ///
    /// The check is <c>TimeZoneInfo.FindSystemTimeZoneById</c> rather than a regular expression
    /// over the shape of an IANA name: a name this installation's clock cannot resolve would be
    /// stored and discovered later by whatever tries to format a date with it. On Linux the same
    /// API reads IANA ids natively, so a name that passes here works where the application runs.
    /// </remarks>
    public static class TimeZonePolicy
    {
        /// <summary>Longest accepted identifier, matching the column width.</summary>
        /// <remarks>
        /// Mirrored rather than imported from the table configuration for the same reason the
        /// registration rules keep their own copies of their numbers: the limit belongs to the
        /// input being accepted, and the column is what happens to agree with it.
        /// </remarks>
        public const int MaxLength = 64;

        /// <summary>
        /// Checks a requested zone and returns it in the form it is stored in.
        /// </summary>
        /// <param name="value">
        /// The identifier as the caller spelled it, or null / empty for "follow the browser".
        /// </param>
        /// <param name="timeZone">
        /// The trimmed identifier, or null when the browser should decide. Meaningless when
        /// the answer is false.
        /// </param>
        /// <returns>
        /// False when the value is one this system cannot resolve, which includes a string
        /// longer than <see cref="MaxLength"/>.
        /// </returns>
        public static bool TryNormalize(string? value, out string? timeZone)
        {
            var requested = value?.Trim() ?? string.Empty;
            timeZone = null;

            if (requested.Length == 0)
            {
                return true;
            }

            if (requested.Length > MaxLength)
            {
                return false;
            }

            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(requested);
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }

            timeZone = requested;

            return true;
        }
    }
}
