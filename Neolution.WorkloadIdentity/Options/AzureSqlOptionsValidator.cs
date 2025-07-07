namespace Neolution.WorkloadIdentity.Options
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
            return ProviderOptionsValidator.Validate<AzureSqlOptions>(name, options);
        }
    }
}
