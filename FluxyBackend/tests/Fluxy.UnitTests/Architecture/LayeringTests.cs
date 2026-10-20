using Fluxy.Application.Users.Services;
using Fluxy.Core.Users.Models;

namespace Fluxy.UnitTests.Architecture
{
    public sealed class LayeringTests
    {
        private static readonly string[] ForbiddenApplicationReferences =
        [
            "Fluxy.DataAccess",
            "Microsoft.EntityFrameworkCore",
            "Npgsql",
            "StackExchange.Redis",
            "MaxMind.GeoIp2"
        ];

        [Fact]
        public void CoreReferencesNoFluxyProject()
        {
            var references = typeof(User).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .ToArray();

            Assert.DoesNotContain(references, name => name.StartsWith("Fluxy.", StringComparison.Ordinal));
        }

        [Fact]
        public void ApplicationReferencesCoreButNoInfrastructure()
        {
            var references = typeof(RegistrationService).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty)
                .ToArray();

            Assert.Contains("Fluxy.Core", references);
            Assert.DoesNotContain(
                references,
                name => ForbiddenApplicationReferences.Contains(name, StringComparer.Ordinal));
        }

        [Fact]
        public void ApplicationDoesNotReferenceTheApiLayer()
        {
            var references = typeof(RegistrationService).Assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name ?? string.Empty);

            Assert.DoesNotContain("Fluxy.API", references);
        }
    }
}
