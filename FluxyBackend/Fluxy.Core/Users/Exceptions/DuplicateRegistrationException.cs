namespace Fluxy.Core.Users.Exceptions
{
    /// <summary>
    /// Raised when a registration lost the race for a value the database requires to be unique.
    /// </summary>
    /// <remarks>
    /// It exists so the transport layer can answer the loser with the same refusal the winner
    /// would have received, instead of a database error. A sequential check cannot see the race,
    /// which is why this type exists at all.
    /// <para>
    /// It lives in Core beside <see cref="Persistence.Exceptions.UniqueViolationException"/>
    /// because the transport catches it, and the transport is not allowed to know the application
    /// layer's internals. The two types answer the same question at two levels: the inner one
    /// says <i>which index refused</i>, this one says <i>what that means for a registration</i>.
    /// </para>
    /// </remarks>
    public sealed class DuplicateRegistrationException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DuplicateRegistrationException"/> class.
        /// </summary>
        /// <param name="innerException">The database failure that triggered it.</param>
        public DuplicateRegistrationException(Exception innerException)
            : base("The username or the email was taken by a concurrent request.", innerException)
        {
        }
    }
}
