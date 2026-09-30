namespace Fluxy.Application.Services.Registration
{
    /// <summary>
    /// How the registration code is produced. Bound from the <c>Registration</c> section.
    /// </summary>
    public sealed class RegistrationOptions
    {
        /// <summary>Configuration section this type is bound from.</summary>
        public const string SectionName = "Registration";

        /// <summary>Number of digits in a code.</summary>
        public int CodeLength { get; set; } = 6;

        /// <summary>How long a mailed code stays valid.</summary>
        public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromMinutes(15);
    }
}
