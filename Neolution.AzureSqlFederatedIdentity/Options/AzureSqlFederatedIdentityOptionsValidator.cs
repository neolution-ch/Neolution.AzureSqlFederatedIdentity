namespace Neolution.AzureSqlFederatedIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validates <see cref="AzureSqlFederatedIdentityOptions"/> to ensure all required configuration values are set.
    /// </summary>
    public class AzureSqlFederatedIdentityOptionsValidator : IValidateOptions<AzureSqlFederatedIdentityOptions>
    {
        /// <summary>
        /// Validates the specified <see cref="AzureSqlFederatedIdentityOptions"/> instance.
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        public ValidateOptionsResult Validate(string? name, AzureSqlFederatedIdentityOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (string.IsNullOrWhiteSpace(options.TenantId))
            {
                return ValidateOptionsResult.Fail("TenantId must be provided.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                return ValidateOptionsResult.Fail("ClientId must be provided.");
            }

            if (options.Google != null && string.IsNullOrWhiteSpace(options.Google.ServiceAccountEmail))
            {
                return ValidateOptionsResult.Fail("Google:ServiceAccountEmail must be provided.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
