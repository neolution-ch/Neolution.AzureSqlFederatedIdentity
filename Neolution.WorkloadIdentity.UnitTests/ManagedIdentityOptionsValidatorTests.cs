namespace Neolution.WorkloadIdentity.UnitTests
{
    using Neolution.WorkloadIdentity.Options;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="ManagedIdentityOptionsValidator"/> class.
    /// </summary>
    public class ManagedIdentityOptionsValidatorTests
    {
        /// <summary>
        /// Validates that the method fails when null options are provided.
        /// </summary>
        [Fact]
        public void Given_NullOptions_When_ValidateStatic_Then_Fails()
        {
            // Act
            var result = ManagedIdentityOptionsValidator.ValidateStatic(null, new ManagedIdentityOptions());

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain(nameof(ManagedIdentityOptions));
        }

        /// <summary>
        /// Validates that the method succeeds when using a system-assigned identity.
        /// </summary>
        [Fact]
        public void Given_UseSystemAssignedIdentity_When_ValidateStatic_Then_Succeeds()
        {
            // Arrange
            var options = new ManagedIdentityOptions { UseSystemAssignedIdentity = true };

            // Act
            var result = ManagedIdentityOptionsValidator.ValidateStatic("name", options);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }

        /// <summary>
        /// Validates that the method fails when no client ID is provided.
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
        /// Validates that the method succeeds when a client ID is provided.
        /// </summary>
        [Fact]
        public void Given_ClientId_When_ValidateStatic_Then_Succeeds()
        {
            // Arrange
            var options = new ManagedIdentityOptions { UseSystemAssignedIdentity = false, ClientId = "id" };

            // Act
            var result = ManagedIdentityOptionsValidator.ValidateStatic("name", options);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }
    }
}
