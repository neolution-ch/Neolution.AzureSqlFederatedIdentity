using Neolution.WorkloadIdentity.Options;
using Shouldly;
using Xunit;

namespace Neolution.WorkloadIdentity.UnitTests
{
    public class WorkloadIdentityOptionsValidatorTests
    {
        [Fact]
        public void Given_NoneConfigured_When_Validate_Then_Fails()
        {
            // Arrange
            var opts = new WorkloadIdentityOptions(); // defaults are both ManagedIdentity with no client ID

            // Act
            var result = new WorkloadIdentityOptionsValidator().Validate(null, opts);

            // Assert
            result.Succeeded.ShouldBeFalse();
            result.FailureMessage.ShouldContain("At least one workload identity section");
        }

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
