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

        /// <summary>
        /// A subscription is still sold under the plan, so deleting it would silently re-mean
        /// the parameters that subscription derives.
        /// </summary>
        /// <remarks>
        /// The foreign key from <c>subscriptions.tariff_plan_id</c> is <c>RESTRICT</c> and would
        /// refuse the delete anyway; this outcome is the service asking first, so the operator
        /// is told which plan is standing in the way and how many subscriptions hold it instead
        /// of being handed a provider error.
        /// </remarks>
        InUse,

        /// <summary>One of the values did not pass validation; which one is in the errors.</summary>
        Invalid
    }
}
