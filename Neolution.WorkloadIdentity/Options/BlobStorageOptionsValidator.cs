namespace Neolution.WorkloadIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validates <see cref="BlobStorageOptions"/> to ensure all required configuration values are set.
    /// </summary>
    public class BlobStorageOptionsValidator : IValidateOptions<BlobStorageOptions>
    {
        /// <summary>
        /// Validates the specified <see cref="BlobStorageOptions"/> instance.
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The <see cref="BlobStorageOptions"/> to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        public ValidateOptionsResult Validate(string? name, BlobStorageOptions options)
        {
            return ProviderOptionsValidator.Validate<BlobStorageOptions>(name, options);
        }
    }
}
