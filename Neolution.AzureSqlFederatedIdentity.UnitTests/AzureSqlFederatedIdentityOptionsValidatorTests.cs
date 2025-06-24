namespace Neolution.AzureSqlFederatedIdentity.UnitTests
{
    using Neolution.AzureSqlFederatedIdentity.Options;
    using Shouldly;
    using Xunit;

    /// <summary>
    /// Tests for <see cref="AzureSqlFederatedIdentityOptionsValidator"/>.
    /// </summary>
    public class AzureSqlFederatedIdentityOptionsValidatorTests
    {
        /// <summary>
        /// The validator instance under test.
        /// </summary>
        private readonly AzureSqlFederatedIdentityOptionsValidator validator = new();

        /// <summary>
        /// Validates that passing null options fails validation.
        /// </summary>
        [Fact]
        public void Validate_NullOptions_Fails()
        {
            // Act
            var result = this.validator.Validate(name: null, options: new AzureSqlFederatedIdentityOptions());

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain("cannot be null");
        }

        /// <summary>
        /// Validates that managed identity options succeed validation.
        /// </summary>
        [Fact]
        public void Validate_ManagedIdentity_Succeeds()
        {
            // Arrange
            var options = new AzureSqlFederatedIdentityOptions
            {
                Provider = FederatedIdentityProvider.ManagedIdentity,
            };

            // Act
            var result = this.validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }

        /// <summary>
        /// Validates that missing Google ClientId fails validation.
        /// </summary>
        [Fact]
        public void Validate_GoogleMissingFields_FailsOnClientId()
        {
            // Arrange
            var options = new AzureSqlFederatedIdentityOptions
            {
                Provider = FederatedIdentityProvider.Google,
                Google = new GoogleOptions(),
            };

            // Act
            var result = this.validator.Validate(name: null, options: options);

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain("Google:ClientId must be provided");
        }

        /// <summary>
        /// Validates that missing Google TenantId fails validation.
        /// </summary>
        [Fact]
        public void Validate_GoogleMissingTenant_FailsOnTenantId()
        {
            // Arrange
            var options = new AzureSqlFederatedIdentityOptions
            {
                Provider = FederatedIdentityProvider.Google,
                Google = new GoogleOptions
                {
                    ClientId = "client",
                },
            };

            // Act
            var result = this.validator.Validate(name: null, options: options);

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain("Google:TenantId must be provided");
        }

        /// <summary>
        /// Validates that missing Google ServiceAccountEmail fails validation.
        /// </summary>
        [Fact]
        public void Validate_GoogleMissingServiceAccountEmail_FailsOnServiceAccountEmail()
        {
            // Arrange
            var options = new AzureSqlFederatedIdentityOptions
            {
                Provider = FederatedIdentityProvider.Google,
                Google = new GoogleOptions
                {
                    ClientId = "client",
                    TenantId = "tenant",
                },
            };

            // Act
            var result = this.validator.Validate(name: null, options: options);

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain("Google:ServiceAccountEmail must be provided");
        }

        /// <summary>
        /// Validates that all required Google fields provided succeed validation.
        /// </summary>
        [Fact]
        public void Validate_GoogleAllFieldsProvided_Succeeds()
        {
            // Arrange
            var options = new AzureSqlFederatedIdentityOptions
            {
                Provider = FederatedIdentityProvider.Google,
                Google = new GoogleOptions
                {
                    ClientId = "client",
                    TenantId = "tenant",
                    ServiceAccountEmail = "email@project.iam.gserviceaccount.com",
                },
            };

            // Act
            var result = this.validator.Validate(name: null, options: options);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }
    }
}
