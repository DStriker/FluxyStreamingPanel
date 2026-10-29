namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// How an admin operation on one subscription ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which a 404 and which a 409
    /// is the transport's answer, stated beside the response table in the API layer.
    /// </remarks>
    public enum SubscriptionAction
    {
        /// <summary>The subscription was issued.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The subscription was deleted.</summary>
        Deleted,

        /// <summary>No subscription with that identifier.</summary>
        NotFound,

        /// <summary>Another subscription already holds that playlist id or MAC.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
