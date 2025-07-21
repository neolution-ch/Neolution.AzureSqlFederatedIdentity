namespace Neolution.WorkloadIdentity.UnitTests
{
    using Neolution.WorkloadIdentity.Options;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="WorkloadIdentityOptionsValidator"/> class.
    /// </summary>
    public class WorkloadIdentityOptionsValidatorTests
    {
        /// <summary>
        /// Validates that the options fail when none are configured.
        /// </summary>
        [Fact]
        public void Given_NoneConfigured_When_Validate_Then_Fails()
        {
            // Arrange
            var opts = new WorkloadIdentityOptions();

            // Act
            var result = new WorkloadIdentityOptionsValidator().Validate(null, opts);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldNotBeNull();
            result.FailureMessage.ShouldContain("At least one workload identity section");
        }

        /// <summary>
        /// Validates that the options succeed when Azure SQL is correctly configured.
        /// </summary>
        [Fact]
        public void Given_AzureValid_When_Validate_Then_Succeeds()
        {
            // Arrange
            var opts = new WorkloadIdentityOptions();
            opts.AzureSql.Provider = WorkloadIdentityProvider.ManagedIdentity;
            opts.AzureSql.ManagedIdentity.UseSystemAssignedIdentity = true;

            // Act
            var result = new WorkloadIdentityOptionsValidator().Validate(null, opts);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }

        /// <summary>
        /// Validates that the options succeed when Blob Storage is correctly configured.
        /// </summary>
        [Fact]
        public void Given_BlobValid_When_Validate_Then_Succeeds()
        {
            // Arrange
            var opts = new WorkloadIdentityOptions();
            opts.BlobStorage.Provider = WorkloadIdentityProvider.ManagedIdentity;
            opts.BlobStorage.ManagedIdentity.UseSystemAssignedIdentity = true;

            // Act
            var result = new WorkloadIdentityOptionsValidator().Validate(null, opts);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }

        /// <summary>
        /// Validates that the options succeed when both Azure SQL and Blob Storage are correctly configured.
        /// </summary>
        [Fact]
        public void Given_BothValid_When_Validate_Then_Succeeds()
        {
            // Arrange
            var opts = new WorkloadIdentityOptions();
            opts.AzureSql.ManagedIdentity.UseSystemAssignedIdentity = true;
            opts.BlobStorage.ManagedIdentity.UseSystemAssignedIdentity = true;

            // Act
            var result = new WorkloadIdentityOptionsValidator().Validate(null, opts);

            // Assert
            result.Succeeded.ShouldBeTrue();
        }
    }
}
