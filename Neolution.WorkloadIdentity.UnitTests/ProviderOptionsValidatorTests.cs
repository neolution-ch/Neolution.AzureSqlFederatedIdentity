namespace Neolution.WorkloadIdentity.UnitTests
{
    using Neolution.WorkloadIdentity.Options;
    using Shouldly;

    /// <summary>
    /// Unit tests for validating provider options.
    /// </summary>
    public class ProviderOptionsValidatorTests
    {
        /// <summary>
        /// Validates that null options fail validation.
        /// </summary>
        [Fact]
        public void Given_NullOptions_When_Validate_Then_Fails()
        {
            // Act
            var result = ProviderOptionsValidator.Validate<AzureSqlOptions>("n", new AzureSqlOptions());

            // Assert
            result.Succeeded.ShouldBeFalse();
        }

        /// <summary>
        /// Validates that missing client ID in static validation fails.
        /// </summary>
        [Fact]
        public void Given_NoClientId_When_ValidateStatic_Then_Fails()
        {
            // Arrange
            var options = new ManagedIdentityOptions { UseSystemAssignedIdentity = false, ClientId = string.Empty };

            // Act
            var result = ManagedIdentityOptionsValidator.ValidateStatic("name", options);

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain(nameof(ManagedIdentityOptions.ClientId));
        }

        /// <summary>
        /// Validates that Google provider with missing fields fails validation.
        /// </summary>
        [Fact]
        public void Given_GoogleWithMissingFields_When_Validate_Then_Fails()
        {
            // Arrange
            var opts = new AzureSqlOptions
            {
                Provider = WorkloadIdentityProvider.Google,
                Google = { ClientId = string.Empty, TenantId = string.Empty, ServiceAccountEmail = string.Empty },
            };

            // Act
            var result = ProviderOptionsValidator.Validate<AzureSqlOptions>("x", opts);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain(nameof(GoogleOptions.ClientId));
        }

        /// <summary>
        /// Validates that a valid managed identity succeeds validation.
        /// </summary>
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
