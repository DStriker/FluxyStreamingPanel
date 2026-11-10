namespace Fluxy.API.Contracts.Streams
{
    /// <summary>
    /// The stable codes the stream problem endpoints answer with.
    /// </summary>
    /// <remarks>
    /// Only the clear has a code of its own; the read answers a page or a validation refusal, and
    /// neither carries a feature code. The spelling follows the other logs
    /// (<c>user_logs_cleared</c>, <c>api_request_failures_cleared</c>), so a client that knows one
    /// knows this one.
    /// </remarks>
    public static class StreamProblemResponses
    {
        /// <summary>Code reported once every row of the journal has been deleted.</summary>
        public const string ClearedCode = "stream_problems_cleared";
    }
}
