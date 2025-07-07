using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Neolution.WorkloadIdentity.Options;
using Shouldly;
using Xunit;

namespace Neolution.WorkloadIdentity.UnitTests
{
    public class ManagedIdentityOptionsValidatorTests
    {
        private readonly IFixture fixture;

        public ManagedIdentityOptionsValidatorTests()
        {
            fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        }

        [Fact]
        public void Given_NullOptions_When_ValidateStatic_Then_Fails()
        {
            // Act
            var result = ManagedIdentityOptionsValidator.ValidateStatic(null, null);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldContain(nameof(ManagedIdentityOptions));
        }

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

        [Fact]
        public void Given_NoClientId_When_ValidateStatic_Then_Fails()
        {
            // Arrange
            var options = new ManagedIdentityOptions { UseSystemAssignedIdentity = false, ClientId = string.Empty };

            // Act
            var result = ManagedIdentityOptionsValidator.ValidateStatic("name", options);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldContain(nameof(ManagedIdentityOptions.ClientId));
        }

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
