namespace Fluxy.Core.Subscriptions.Enums
{
    /// <summary>
    /// How an admin operation on a tariff plan ended.
    /// </summary>
    /// <remarks>
    /// The service knows only these words. Which of them is a 201, which a 404 and which a 409
    /// is the transport's answer, stated beside the response table in the API layer.
    /// </remarks>
    public enum TariffPlanAction
    {
        /// <summary>The plan was created.</summary>
        Created,

        /// <summary>The change was written to the row.</summary>
        Updated,

        /// <summary>The plan was deleted.</summary>
        Deleted,

        /// <summary>No plan with that identifier.</summary>
        NotFound,

        /// <summary>Another plan already holds that name in some spelling.</summary>
        AlreadyExists,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
