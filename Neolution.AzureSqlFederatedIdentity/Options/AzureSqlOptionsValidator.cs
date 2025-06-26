namespace Neolution.AzureSqlFederatedIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validates <see cref="AzureSqlOptions"/> to ensure all required configuration values are set.
    /// </summary>
    public class AzureSqlOptionsValidator : IValidateOptions<AzureSqlOptions>
    {
        /// <summary>
        /// Validates the specified <see cref="AzureSqlOptions"/> instance.
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        public ValidateOptionsResult Validate(string? name, AzureSqlOptions options)
        {
            if (options is null)
            {
                return ValidateOptionsResult.Fail("AzureSqlOptions cannot be null.");
            }

            // Dispatch based on enum provider
            switch (options.Provider)
            {
                case WorkloadIdentityProvider.ManagedIdentity:
                    return ValidateOptionsResult.Success;

                case WorkloadIdentityProvider.Google:
                    return options.Google is null
                        ? ValidateOptionsResult.Fail("Google settings must be provided when Provider is 'Google'.")
                        : ValidateGoogleSettings(options.Google);

                default:
                    return ValidateOptionsResult.Fail($"Unknown provider '{options.Provider}'.");
            }
        }

        /// <summary>
        /// Validates required fields for Google federated identity options.
        /// </summary>
        /// <param name="g">The Google options to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        private static ValidateOptionsResult ValidateGoogleSettings(GoogleOptions g)
        {
            if (string.IsNullOrWhiteSpace(g.ClientId))
            {
                return ValidateOptionsResult.Fail("Google:ClientId must be provided when Provider is 'Google'.");
            }

            if (string.IsNullOrWhiteSpace(g.TenantId))
            {
                return ValidateOptionsResult.Fail("Google:TenantId must be provided when Provider is 'Google'.");
            }

            if (string.IsNullOrWhiteSpace(g.ServiceAccountEmail))
            {
                return ValidateOptionsResult.Fail("Google:ServiceAccountEmail must be provided when Provider is 'Google'.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
