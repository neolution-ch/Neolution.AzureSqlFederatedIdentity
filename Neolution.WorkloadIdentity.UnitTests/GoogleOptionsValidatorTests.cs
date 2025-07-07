using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Microsoft.Extensions.Options;
using Neolution.WorkloadIdentity.Options;
using Shouldly;
using Xunit;

namespace Neolution.WorkloadIdentity.UnitTests
{
    public class GoogleOptionsValidatorTests
    {
        private readonly IFixture fixture;

        public GoogleOptionsValidatorTests()
        {
            fixture = new Fixture().Customize(new AutoNSubstituteCustomization());
        }

        [Fact]
        public void Given_NullOptions_When_ValidateStatic_Then_Fails()
        {
            // Act
            var result = GoogleOptionsValidator.ValidateStatic(null, null);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldContain(nameof(GoogleOptions));
        }

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
                ServiceAccountEmail = serviceAccountEmail
            };

            // Act
            var result = GoogleOptionsValidator.ValidateStatic("test", options);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldContain(expectedField);
        }

        [Fact]
        public void Given_AllFieldsValid_When_ValidateStatic_Then_Succeeds()
        {
            // Arrange
            var options = fixture.Create<GoogleOptions>();
            options.ClientId = "client";
            options.TenantId = "tenant";
            options.ServiceAccountEmail = "email";

            // Act
            var result = GoogleOptionsValidator.ValidateStatic("testName", options);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }
    }
}
