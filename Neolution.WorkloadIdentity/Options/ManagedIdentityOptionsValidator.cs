namespace Neolution.WorkloadIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validator for <see cref="ManagedIdentityOptions"/>.
    /// </summary>
    public class ManagedIdentityOptionsValidator : IValidateOptions<ManagedIdentityOptions>
    {
        /// <summary>
        /// Static entry point for validation, used by <see cref="ProviderOptionsValidator"/>.
        /// </summary>
        /// <param name="name">The named options instance.</param>
        /// <param name="options">The <see cref="ManagedIdentityOptions"/> to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or failure.</returns>
        public static ValidateOptionsResult ValidateStatic(string? name, ManagedIdentityOptions options)
        {
            var prefix = name is null ? string.Empty : $"Options '{name}': ";

            if (options is null)
            {
                return ValidateOptionsResult.Fail(prefix + $"{nameof(ManagedIdentityOptions)} cannot be null.");
            }

            if (options.UseSystemAssignedIdentity)
            {
                return ValidateOptionsResult.Success;
            }

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                return ValidateOptionsResult.Fail(prefix +
                    $"{nameof(ManagedIdentityOptions)}.{nameof(ManagedIdentityOptions.ClientId)} must be provided when {nameof(ManagedIdentityOptions.UseSystemAssignedIdentity)} is false.");
            }

            return ValidateOptionsResult.Success;
        }

        /// <summary>
        /// Validates the <see cref="ManagedIdentityOptions"/> instance.
        /// </summary>
        /// <param name="name">The name of the options instance being validated.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>
        /// A <see cref="ValidateOptionsResult"/> indicating the result of the validation.
        /// </returns>
        public ValidateOptionsResult Validate(string? name, ManagedIdentityOptions options)
        {
            return ValidateStatic(name, options);
        }
    }
}
