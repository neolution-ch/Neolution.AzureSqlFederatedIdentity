namespace Neolution.AzureSqlFederatedIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validator for <see cref="ManagedIdentityOptions"/>.
    /// </summary>
    public class ManagedIdentityOptionsValidator : IValidateOptions<ManagedIdentityOptions>
    {
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
            ArgumentNullException.ThrowIfNull(options);

            if (options.UseSystemAssigned)
            {
                return ValidateOptionsResult.Success;
            }

            if (string.IsNullOrWhiteSpace(options.ClientId))
            {
                return ValidateOptionsResult.Fail("ClientId must be provided when UseSystemAssigned is false (user-assigned identity).");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
