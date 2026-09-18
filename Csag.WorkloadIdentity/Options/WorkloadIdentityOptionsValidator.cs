namespace Csag.WorkloadIdentity.Options
{
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Validates <see cref="WorkloadIdentityOptions"/>: at least one resource must be configured, every configured
    /// resource must carry the complete settings of the provider it selects, and the refresh-ahead window must be
    /// positive. Every failure names the configuration path of the offending value, relative to the options section.
    /// </summary>
    internal sealed class WorkloadIdentityOptionsValidator : IValidateOptions<WorkloadIdentityOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, WorkloadIdentityOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var failures = new List<string>();
            if (options.AzureSql is null && options.BlobStorage is null)
            {
                failures.Add($"At least one resource section ({nameof(options.AzureSql)} or {nameof(options.BlobStorage)}) must be configured.");
            }

            ValidateResource(nameof(options.AzureSql), options.AzureSql, failures);
            ValidateResource(nameof(options.BlobStorage), options.BlobStorage, failures);

            if (options.RefreshAheadWindow <= TimeSpan.Zero)
            {
                failures.Add($"{nameof(options.RefreshAheadWindow)} must be a positive duration.");
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }

        /// <summary>
        /// Validates a resource section, if present, against the provider it selects.
        /// </summary>
        /// <param name="path">The configuration path of the section.</param>
        /// <param name="resource">The section, or <see langword="null"/> when the resource is not configured.</param>
        /// <param name="failures">Receives a message for every invalid value.</param>
        private static void ValidateResource(string path, WorkloadIdentityResourceOptions? resource, List<string> failures)
        {
            if (resource is null)
            {
                return;
            }

            switch (resource.Provider)
            {
                case WorkloadIdentityProvider.ManagedIdentity:
                    ValidateManagedIdentity(path, resource.ManagedIdentity, failures);
                    break;
                case WorkloadIdentityProvider.Google:
                    ValidateGoogle(path, resource.Google, failures);
                    break;
                default:
                    failures.Add($"{path}:{nameof(resource.Provider)} must be {nameof(WorkloadIdentityProvider.ManagedIdentity)} or {nameof(WorkloadIdentityProvider.Google)}.");
                    break;
            }
        }

        /// <summary>
        /// Validates the managed identity section of a resource.
        /// </summary>
        /// <param name="path">The configuration path of the resource section.</param>
        /// <param name="managedIdentity">The managed identity section, or <see langword="null"/> when it is absent.</param>
        /// <param name="failures">Receives a message for every invalid value.</param>
        private static void ValidateManagedIdentity(string path, ManagedIdentityOptions? managedIdentity, List<string> failures)
        {
            var sectionPath = $"{path}:{nameof(WorkloadIdentityResourceOptions.ManagedIdentity)}";
            if (managedIdentity is null)
            {
                failures.Add($"{sectionPath} section must be provided when {path}:{nameof(WorkloadIdentityResourceOptions.Provider)} is {nameof(WorkloadIdentityProvider.ManagedIdentity)}.");
                return;
            }

            if (!managedIdentity.UseSystemAssignedIdentity && string.IsNullOrWhiteSpace(managedIdentity.ClientId))
            {
                failures.Add($"{sectionPath}:{nameof(managedIdentity.ClientId)} must be provided unless {sectionPath}:{nameof(managedIdentity.UseSystemAssignedIdentity)} is true.");
            }
        }

        /// <summary>
        /// Validates the Google section of a resource.
        /// </summary>
        /// <param name="path">The configuration path of the resource section.</param>
        /// <param name="google">The Google section, or <see langword="null"/> when it is absent.</param>
        /// <param name="failures">Receives a message for every invalid value.</param>
        private static void ValidateGoogle(string path, GoogleOptions? google, List<string> failures)
        {
            var sectionPath = $"{path}:{nameof(WorkloadIdentityResourceOptions.Google)}";
            if (google is null)
            {
                failures.Add($"{sectionPath} section must be provided when {path}:{nameof(WorkloadIdentityResourceOptions.Provider)} is {nameof(WorkloadIdentityProvider.Google)}.");
                return;
            }

            RequireValue(sectionPath, nameof(google.TenantId), google.TenantId, failures);
            RequireValue(sectionPath, nameof(google.ClientId), google.ClientId, failures);
            RequireValue(sectionPath, nameof(google.ServiceAccountEmail), google.ServiceAccountEmail, failures);
        }

        /// <summary>
        /// Records a failure when a required value is blank.
        /// </summary>
        /// <param name="sectionPath">The configuration path of the section holding the value.</param>
        /// <param name="key">The key of the value within the section.</param>
        /// <param name="value">The value.</param>
        /// <param name="failures">Receives the message when the value is blank.</param>
        private static void RequireValue(string sectionPath, string key, string? value, List<string> failures)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failures.Add($"{sectionPath}:{key} must be provided.");
            }
        }
    }
}
