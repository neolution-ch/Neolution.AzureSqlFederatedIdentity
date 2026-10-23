namespace Csag.AzureSqlFederatedIdentity.UnitTests
{
    using Csag.AzureSqlFederatedIdentity.Options;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="AzureSqlFederatedIdentityOptionsValidator"/> class.
    /// </summary>
    public class AzureSqlFederatedIdentityOptionsValidatorTests
    {
        /// <summary>
        /// The validator under test.
        /// </summary>
        private readonly AzureSqlFederatedIdentityOptionsValidator validator = new();

        /// <summary>
        /// Verifies that complete options pass validation.
        /// </summary>
        [Fact]
        public void Given_CompleteOptions_When_Validate_Then_Succeeds()
        {
            // Arrange
            var options = CreateCompleteOptions();

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that a blank tenant ID fails validation.
        /// </summary>
        /// <param name="tenantId">The blank tenant ID.</param>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Given_BlankTenantId_When_Validate_Then_Fails(string tenantId)
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.TenantId = tenantId;

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldNotBeNull().ShouldContain(nameof(AzureSqlFederatedIdentityOptions.TenantId));
        }

        /// <summary>
        /// Verifies that a blank client ID fails validation.
        /// </summary>
        /// <param name="clientId">The blank client ID.</param>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Given_BlankClientId_When_Validate_Then_Fails(string clientId)
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.ClientId = clientId;

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldNotBeNull().ShouldContain(nameof(AzureSqlFederatedIdentityOptions.ClientId));
        }

        /// <summary>
        /// Verifies that a missing Google section fails validation.
        /// </summary>
        [Fact]
        public void Given_MissingGoogleSection_When_Validate_Then_Fails()
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.Google = null;

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldNotBeNull().ShouldContain("Google section");
        }

        /// <summary>
        /// Verifies that a blank service account email fails validation.
        /// </summary>
        /// <param name="serviceAccountEmail">The blank service account email.</param>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Given_BlankServiceAccountEmail_When_Validate_Then_Fails(string? serviceAccountEmail)
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.Google = new GoogleOptions { ServiceAccountEmail = serviceAccountEmail };

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldNotBeNull().ShouldContain("Google:ServiceAccountEmail");
        }

        /// <summary>
        /// Verifies that a refresh-ahead window that is not positive fails validation.
        /// </summary>
        /// <param name="minutes">The window length in minutes.</param>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Given_NonPositiveRefreshAheadWindow_When_Validate_Then_Fails(int minutes)
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.RefreshAheadWindow = TimeSpan.FromMinutes(minutes);

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldNotBeNull().ShouldContain(nameof(AzureSqlFederatedIdentityOptions.RefreshAheadWindow));
        }

        /// <summary>
        /// Verifies that a refresh-ahead window longer than five minutes fails validation.
        /// </summary>
        /// <param name="seconds">The window length in seconds.</param>
        [Theory]
        [InlineData(301)]
        [InlineData(600)]
        public void Given_RefreshAheadWindowAboveFiveMinutes_When_Validate_Then_Fails(int seconds)
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.RefreshAheadWindow = TimeSpan.FromSeconds(seconds);

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldNotBeNull().ShouldContain(nameof(AzureSqlFederatedIdentityOptions.RefreshAheadWindow));
        }

        /// <summary>
        /// Verifies that a refresh-ahead window of exactly five minutes passes validation.
        /// </summary>
        [Fact]
        public void Given_RefreshAheadWindowOfFiveMinutes_When_Validate_Then_Succeeds()
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.RefreshAheadWindow = TimeSpan.FromMinutes(5);

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }

        /// <summary>
        /// Verifies that every failed check is reported, not only the first.
        /// </summary>
        [Fact]
        public void Given_SeveralInvalidValues_When_Validate_Then_ReportsEveryFailure()
        {
            // Arrange
            var options = CreateCompleteOptions();
            options.TenantId = string.Empty;
            options.ClientId = string.Empty;
            options.Google = null;
            options.RefreshAheadWindow = TimeSpan.Zero;

            // Act
            var result = this.validator.Validate(null, options);

            // Assert
            result.Failed.ShouldBeTrue();
            result.Failures.ShouldNotBeNull().Count().ShouldBe(4);
        }

        /// <summary>
        /// Creates options with every required value set.
        /// </summary>
        /// <returns>The options.</returns>
        private static AzureSqlFederatedIdentityOptions CreateCompleteOptions()
        {
            return new AzureSqlFederatedIdentityOptions
            {
                TenantId = "tenant",
                ClientId = "client",
                Google = new GoogleOptions { ServiceAccountEmail = "sa@example.iam.gserviceaccount.com" },
            };
        }
    }
}
