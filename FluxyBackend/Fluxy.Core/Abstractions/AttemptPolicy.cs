namespace Fluxy.Core.Abstractions
{
    /// <summary>
    /// How many attempts one caller may spend on an operation before the operation has to
    /// refuse it for the rest of the window.
    /// </summary>
    /// <param name="Limit">
    /// Number of attempts allowed inside <paramref name="Window"/>. A limit of one turns the
    /// policy into "once per window", which is what a single registration per client means.
    /// </param>
    /// <param name="Window">
    /// Length of the counting window. It starts with the first attempt of a key rather than
    /// running on a shared clock, so a burst cannot straddle a boundary and double the budget.
    /// </param>
    public readonly record struct AttemptPolicy(int Limit, TimeSpan Window);
}
