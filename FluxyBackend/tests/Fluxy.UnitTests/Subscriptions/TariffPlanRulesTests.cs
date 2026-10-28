using Fluxy.Application.Subscriptions.Utils;
using Fluxy.Core.Subscriptions.Commands;
using Fluxy.Core.Subscriptions.Enums;
using Fluxy.Core.Subscriptions.Utils;

namespace Fluxy.UnitTests.Subscriptions
{
    /// <summary>
    /// The rules a tariff plan is judged by: a name that fits the column, a price that is not
    /// negative, a validity period that is at least one unit, a trial held inside thirty days, at
    /// least one delivery method and one output format, a strict STB check only beside MAG
    /// support, and a connection ceiling inside its range.
    /// </summary>
    /// <remarks>
    /// Each rule is exercised on its own as well as through <see cref="TariffPlanRules.Validate"/>,
    /// because the service calls the combined form and the form mirrors the individual ones - a
    /// rule that only ever ran as part of the set would be the one place a regression could hide.
    /// </remarks>
    public sealed class TariffPlanRulesTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void ABlankNameIsNotUsable(string name)
        {
            Assert.False(TariffPlanRules.IsUsableName(name));
        }

        [Fact]
        public void ANameAtTheColumnWidthIsUsableAndOneCharacterMoreIsNot()
        {
            Assert.True(TariffPlanRules.IsUsableName(
                new string('a', TariffPlanLimits.NameMaxLength)));
            Assert.False(TariffPlanRules.IsUsableName(
                new string('a', TariffPlanLimits.NameMaxLength + 1)));
        }

        [Fact]
        public void AFreePlanIsACostTheDomainHasAndANegativeOneIsNot()
        {
            Assert.True(TariffPlanRules.IsUsableCost(0));
            Assert.True(TariffPlanRules.IsUsableCost(120));
            Assert.False(TariffPlanRules.IsUsableCost(-1));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(24, true)]
        public void AValidityPeriodIsAtLeastOneUnit(int value, bool usable)
        {
            Assert.Equal(usable, TariffPlanRules.IsUsableDurationValue(value));
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(10000, true)]
        [InlineData(0, false)]
        [InlineData(10001, false)]
        [InlineData(-5, false)]
        public void AConnectionCeilingStaysInsideItsRange(int connections, bool usable)
        {
            Assert.Equal(usable, TariffPlanRules.IsUsableConnections(connections));
        }

        [Theory]
        // The ceiling stated in the two units it can be stated in exactly.
        [InlineData(1, TariffPlanDurationUnit.Hours, true)]
        [InlineData(TariffPlanLimits.TrialMaxHours, TariffPlanDurationUnit.Hours, true)]
        [InlineData(TariffPlanLimits.TrialMaxHours + 1, TariffPlanDurationUnit.Hours, false)]
        [InlineData(1, TariffPlanDurationUnit.Days, true)]
        [InlineData(TariffPlanLimits.TrialMaxDays, TariffPlanDurationUnit.Days, true)]
        [InlineData(TariffPlanLimits.TrialMaxDays + 1, TariffPlanDurationUnit.Days, false)]
        // A month is not thirty days in every month and a year is not twelve months in every
        // year, so neither can be held inside "at most thirty days".
        [InlineData(1, TariffPlanDurationUnit.Months, false)]
        [InlineData(1, TariffPlanDurationUnit.Years, false)]
        public void ATrialMayOnlyBeExpressedInTheTwoUnitsTheCeilingCanHold(
            int value,
            TariffPlanDurationUnit unit,
            bool allowed)
        {
            Assert.Equal(allowed, TariffPlanRules.IsTrialDurationAllowed(value, unit));
        }

        [Fact]
        public void APlanWithoutADeliveryMethodIsRefused()
        {
            var errors = Validate(
                supportsM3u: false,
                supportsMag: false,
                supportsEnigma2: false);

            Assert.Contains(nameof(NewTariffPlan.SupportsM3u), errors.Keys);
        }

        [Theory]
        [InlineData(true, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        public void OneDeliveryMethodIsEnough(bool m3u, bool mag, bool enigma2)
        {
            var errors = Validate(supportsM3u: m3u, supportsMag: mag, supportsEnigma2: enigma2);

            Assert.DoesNotContain(nameof(NewTariffPlan.SupportsM3u), errors.Keys);
        }

        [Fact]
        public void APlanWithoutAnOutputFormatIsRefused()
        {
            var errors = Validate(outputMpegTs: false, outputHls: false);

            Assert.Contains(nameof(NewTariffPlan.OutputMpegTs), errors.Keys);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void OneOutputFormatIsEnough(bool mpegTs, bool hls)
        {
            var errors = Validate(outputMpegTs: mpegTs, outputHls: hls);

            Assert.DoesNotContain(nameof(NewTariffPlan.OutputMpegTs), errors.Keys);
        }

        [Fact]
        public void AStrictStbCheckIsRefusedWithoutMagSupport()
        {
            var errors = Validate(supportsMag: false, strictStbDeviceId: true);

            Assert.Contains(nameof(NewTariffPlan.StrictStbDeviceId), errors.Keys);
        }

        [Fact]
        public void AStrictStbCheckIsAcceptedBesideMagSupport()
        {
            var errors = Validate(supportsMag: true, strictStbDeviceId: true);

            Assert.DoesNotContain(nameof(NewTariffPlan.StrictStbDeviceId), errors.Keys);
        }

        [Fact]
        public void ATrialOfOneMonthIsRefused()
        {
            var errors = Validate(
                isTrial: true,
                durationValue: 1,
                durationUnit: TariffPlanDurationUnit.Months);

            Assert.Contains(nameof(NewTariffPlan.DurationUnit), errors.Keys);
        }

        [Fact]
        public void ATrialOfThirtyDaysIsAccepted()
        {
            var errors = Validate(
                isTrial: true,
                durationValue: TariffPlanLimits.TrialMaxDays,
                durationUnit: TariffPlanDurationUnit.Days);

            Assert.DoesNotContain(nameof(NewTariffPlan.DurationUnit), errors.Keys);
        }

        [Fact]
        public void ANonTrialMayStateItsLengthInMonthsAndYears()
        {
            var months = Validate(
                isTrial: false,
                durationValue: 1,
                durationUnit: TariffPlanDurationUnit.Months);

            var years = Validate(
                isTrial: false,
                durationValue: 1,
                durationUnit: TariffPlanDurationUnit.Years);

            Assert.DoesNotContain(nameof(NewTariffPlan.DurationUnit), months.Keys);
            Assert.DoesNotContain(nameof(NewTariffPlan.DurationUnit), years.Keys);
        }

        [Fact]
        public void AValidPlanProducesNoErrors()
        {
            var errors = Validate();

            Assert.Empty(errors);
        }

        /// <summary>
        /// Runs the combined validation with a valid plan behind every override, so a test that
        /// names one broken value proves that value and nothing else.
        /// </summary>
        private static Dictionary<string, string[]> Validate(
            string name = "Standard",
            bool isTrial = false,
            int cost = 100,
            int durationValue = 1,
            TariffPlanDurationUnit durationUnit = TariffPlanDurationUnit.Months,
            bool supportsM3u = true,
            bool supportsMag = true,
            bool supportsEnigma2 = true,
            bool outputMpegTs = true,
            bool outputHls = true,
            bool strictStbDeviceId = false,
            int maxConnections = 2)
            => TariffPlanRules.Validate(
                name,
                isTrial,
                cost,
                durationValue,
                durationUnit,
                supportsM3u,
                supportsMag,
                supportsEnigma2,
                outputMpegTs,
                outputHls,
                strictStbDeviceId,
                maxConnections);
    }
}
