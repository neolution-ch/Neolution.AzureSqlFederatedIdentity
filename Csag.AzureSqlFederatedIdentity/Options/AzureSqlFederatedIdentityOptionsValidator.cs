namespace Csag.AzureSqlFederatedIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validates <see cref="AzureSqlFederatedIdentityOptions"/> to ensure all required configuration values are set.
    /// Every failed check is reported, so a single startup failure names every problem.
    /// </summary>
    public class AzureSqlFederatedIdentityOptionsValidator : IValidateOptions<AzureSqlFederatedIdentityOptions>
    {
        /// <summary>
        /// The longest refresh-ahead window that still obtains a fresh token. The credential caches its tokens in
        /// MSAL, which keeps returning the cached token until five minutes before it expires, so an earlier refresh
        /// gets the same token back.
        /// </summary>
        private static readonly TimeSpan MaximumRefreshAheadWindow = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Validates the specified <see cref="AzureSqlFederatedIdentityOptions"/> instance.
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        public ValidateOptionsResult Validate(string? name, AzureSqlFederatedIdentityOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var builder = new ValidateOptionsResultBuilder();

            if (string.IsNullOrWhiteSpace(options.TenantId))
            {
                builder.AddError("TenantId must be provided.");
            }

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                builder.AddError("ClientId must be provided.");
            }

            if (options.Google is null)
            {
                builder.AddError("Google section must be provided.");
            }

            if (options.Google is not null && string.IsNullOrWhiteSpace(options.Google.ServiceAccountEmail))
            {
                builder.AddError("Google:ServiceAccountEmail must be provided.");
            }

            if (options.RefreshAheadWindow <= TimeSpan.Zero)
            {
                builder.AddError("RefreshAheadWindow must be a positive duration.");
            }

            if (options.RefreshAheadWindow > MaximumRefreshAheadWindow)
            {
                builder.AddError("RefreshAheadWindow must not exceed five minutes: the credential returns its cached token until five minutes before expiry, so a longer window cannot refresh earlier.");
            }

            return builder.Build();
        }
    }
}
