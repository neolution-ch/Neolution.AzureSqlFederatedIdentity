using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Extensions.Options;
using Neolution.WorkloadIdentity.Options;
using Shouldly;
using Xunit;

namespace Neolution.WorkloadIdentity.UnitTests
{
    public class ProviderOptionsValidatorTests
    {
        private readonly IFixture fixture;

        public ProviderOptionsValidatorTests()
        {
            fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        }

        [Fact]
        public void Given_NullOptions_When_Validate_Then_Fails()
        {
            // Act
            var result = ProviderOptionsValidator.Validate<AzureSqlOptions>("n", null);

            // Assert
            result.Succeeded.ShouldBeFalse();
        }

        [Fact]
        public void Given_ManagedIdentityWithNoClientId_When_Validate_Then_Fails()
        {
            // Arrange
            var opts = new AzureSqlOptions { Provider = WorkloadIdentityProvider.ManagedIdentity, ManagedIdentity = { UseSystemAssignedIdentity = false, ClientId = "" } };

            // Act
            var result = ProviderOptionsValidator.Validate<AzureSqlOptions>(null, opts);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldContain(nameof(ManagedIdentityOptions.ClientId));
        }

        [Fact]
        public void Given_GoogleWithMissingFields_When_Validate_Then_Fails()
        {
            // Arrange
            var opts = new AzureSqlOptions { Provider = WorkloadIdentityProvider.Google, Google = { ClientId = "", TenantId = "", ServiceAccountEmail = "" } };

            // Act
            var result = ProviderOptionsValidator.Validate<AzureSqlOptions>("x", opts);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldContain(nameof(GoogleOptions.ClientId));
        }

        [Fact]
        public void Given_ValidManagedIdentity_When_Validate_Then_Succeeds()
        {
            // Arrange
            var opts = new AzureSqlOptions { Provider = WorkloadIdentityProvider.ManagedIdentity, ManagedIdentity = { UseSystemAssignedIdentity = true } };

            // Act
            var result = ProviderOptionsValidator.Validate<AzureSqlOptions>(null, opts);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }
    }
}
