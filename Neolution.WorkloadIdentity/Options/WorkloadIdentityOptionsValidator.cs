namespace Neolution.WorkloadIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validates top-level <see cref="WorkloadIdentityOptions"/>, ensuring at least one resource section is valid.
    /// </summary>
    public class WorkloadIdentityOptionsValidator : IValidateOptions<WorkloadIdentityOptions>
    {
        /// <summary>
        /// Validates the <see cref="WorkloadIdentityOptions"/> instance.
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating the result of the validation.</returns>
        public ValidateOptionsResult Validate(string? name, WorkloadIdentityOptions options)
        {
            // Validate individual sections
            var azureResult = new AzureSqlOptionsValidator().Validate(name, options.AzureSql);
            var blobResult = new BlobStorageOptionsValidator().Validate(name, options.BlobStorage);

            if (!azureResult.Succeeded && !blobResult.Succeeded)
            {
                return ValidateOptionsResult.Fail("At least one workload identity section (AzureSql or BlobStorage) must be validly configured.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
