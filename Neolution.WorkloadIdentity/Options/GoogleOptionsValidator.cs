namespace Neolution.WorkloadIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validates <see cref="GoogleOptions"/> to ensure all required Google federated identity fields are provided.
    /// </summary>
    public class GoogleOptionsValidator : IValidateOptions<GoogleOptions>
    {
        /// <summary>
        /// Static entry point for validation, used by <see cref="ProviderOptionsValidator"/>.
        /// </summary>
        /// <param name="name">The named options instance.</param>
        /// <param name="options">The <see cref="GoogleOptions"/> to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        public static ValidateOptionsResult ValidateStatic(string? name, GoogleOptions options)
        {
            if (options is null)
            {
                return ValidateOptionsResult.Fail($"{nameof(GoogleOptions)} cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                return ValidateOptionsResult.Fail($"{nameof(GoogleOptions)}.{nameof(GoogleOptions.ClientId)} must be provided when using Google provider.");
            }

            if (string.IsNullOrWhiteSpace(options.TenantId))
            {
                return ValidateOptionsResult.Fail($"{nameof(GoogleOptions)}.{nameof(GoogleOptions.TenantId)} must be provided when using Google provider.");
            }

            if (string.IsNullOrWhiteSpace(options.ServiceAccountEmail))
            {
                return ValidateOptionsResult.Fail($"{nameof(GoogleOptions)}.{nameof(GoogleOptions.ServiceAccountEmail)} must be provided when using Google provider.");
            }

            return ValidateOptionsResult.Success;
        }

        /// <inheritdoc/>
        public ValidateOptionsResult Validate(string? name, GoogleOptions options)
        {
            return ValidateStatic(name, options);
        }
    }
}
