namespace Neolution.WorkloadIdentity.UnitTests
{
    using Neolution.WorkloadIdentity.Options;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="GoogleOptionsValidator"/> class.
    /// </summary>
    public class GoogleOptionsValidatorTests
    {
        /// <summary>
        /// Validates that the method fails when null options are provided.
        /// </summary>
        [Fact]
        public void Given_NullOptions_When_ValidateStatic_Then_Fails()
        {
            // Act
            var result = GoogleOptionsValidator.ValidateStatic(null, new GoogleOptions());

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain(nameof(GoogleOptions));
        }

        /// <summary>
        /// Validates that the method fails when invalid fields are provided.
        /// </summary>
        /// <param name="clientId">The client ID.</param>
        /// <param name="tenantId">The tenant ID.</param>
        /// <param name="serviceAccountEmail">The service account email.</param>
        /// <param name="expectedField">The expected field causing the failure.</param>
        [Theory]
        [InlineData("", "tenant", "email", nameof(GoogleOptions.ClientId))]
        [InlineData("client", "", "email", nameof(GoogleOptions.TenantId))]
        [InlineData("client", "tenant", "", nameof(GoogleOptions.ServiceAccountEmail))]
        public void Given_InvalidFields_When_ValidateStatic_Then_Fails(string clientId, string tenantId, string serviceAccountEmail, string expectedField)
        {
            // Arrange
            var options = new GoogleOptions
            {
                ClientId = clientId,
                TenantId = tenantId,
                ServiceAccountEmail = serviceAccountEmail,
            };

            // Act
            var result = GoogleOptionsValidator.ValidateStatic("test", options);

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain(expectedField);
        }

        /// <summary>
        /// Validates that the method succeeds when all fields are valid.
        /// </summary>
        [Fact]
        public void Given_AllFieldsValid_When_ValidateStatic_Then_Succeeds()
        {
            // Arrange
            var options = new GoogleOptions
            {
                ClientId = "client",
                TenantId = "tenant",
                ServiceAccountEmail = "email",
            };

            // Act
            var result = GoogleOptionsValidator.ValidateStatic("testName", options);

            // Assert
            result.ShouldNotBeNull();
            result.Succeeded.ShouldBeTrue();
        }
    }
}
