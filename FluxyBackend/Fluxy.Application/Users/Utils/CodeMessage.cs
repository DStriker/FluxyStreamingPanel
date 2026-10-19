namespace Fluxy.Application.Users.Utils
{
    /// <summary>
    /// The frame a message that carries a one-time code is written in.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three messages carry a code - the registration code, the password reset code and the
    /// confirmation code of a profile change - and each of them was written out in full, with
    /// the same greeting and the same footer wrapped around a paragraph that is not the same
    /// at all. The three differ in what the code confirms, and the wording of that is the
    /// point of each message; the greeting and the footer are not.
    /// </para>
    /// <para>
    /// So the frame lives here and the two paragraphs do not: the caller says what the code is
    /// for, and this says what the message looks like. The wording of the paragraphs is left
    /// exactly as it was, because a message a reader has already seen should not read
    /// differently after a refactoring.
    /// </para>
    /// </remarks>
    public static class CodeMessage
    {
        /// <summary>
        /// Writes the message.
        /// </summary>
        /// <param name="username">The name the message greets.</param>
        /// <param name="headline">The line that carries the code itself.</param>
        /// <param name="details">The paragraph that says what the code confirms and what to do.</param>
        /// <returns>The plain text body of the message.</returns>
        public static string Build(string username, string headline, string details)
            => $"""
                Hello {username},

                {headline}

                {details}

                {Environment.NewLine}-
                {Environment.NewLine}This is an automated message, please do not reply.
                """;
    }
}
