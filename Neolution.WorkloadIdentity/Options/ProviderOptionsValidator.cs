namespace Neolution.WorkloadIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Provides validation logic for any options implementing <see cref="IProviderOptions"/>,
    /// dispatching to the appropriate provider-specific validator.
    /// </summary>
    public static class ProviderOptionsValidator
    {
        /// <summary>
        /// Validates any <see cref="IProviderOptions"/>, dispatching to provider-specific static validators.
        /// </summary>
        /// <typeparam name="TOptions">The concrete options type for error messages.</typeparam>
        /// <param name="name">The named options instance (if any).</param>
        /// <param name="options">The provider-backed options to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        public static ValidateOptionsResult Validate<TOptions>(string? name, IProviderOptions options)
        {
            if (options is null)
            {
                return ValidateOptionsResult.Fail($"{nameof(TOptions)} cannot be null.");
            }

            var prefix = name is null ? string.Empty : $"Options '{name}': ";

            switch (options.Provider)
            {
                case WorkloadIdentityProvider.ManagedIdentity:
                    return ManagedIdentityOptionsValidator.ValidateStatic(name, options.ManagedIdentity);

                case WorkloadIdentityProvider.Google:
                    return GoogleOptionsValidator.ValidateStatic(name, options.Google);

                default:
                    return ValidateOptionsResult.Fail(prefix + $"Unknown {nameof(IProviderOptions.Provider)} '{options.Provider}'.");
            }
        }
    }
}
