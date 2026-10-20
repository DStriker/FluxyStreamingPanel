using Fluxy.Core.Security.Enums;
using Fluxy.Core.Security.Interfaces;

namespace Fluxy.IntegrationTests.Infrastructure.Fakes
{
    /// <summary>
    /// Answers reCAPTCHA checks without contacting Google.
    /// </summary>
    public sealed class FakeRecaptchaValidator : IRecaptchaValidator
    {
        public RecaptchaValidationOutcome Outcome { get; set; } = RecaptchaValidationOutcome.Passed;

        public Task<RecaptchaValidationOutcome> ValidateAsync(
            string? token,
            string? expectedAction,
            string? remoteAddress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Outcome);
    }
}
