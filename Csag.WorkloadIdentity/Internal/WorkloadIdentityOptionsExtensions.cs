namespace Csag.WorkloadIdentity.Internal
{
    using Csag.WorkloadIdentity.Options;

    /// <summary>
    /// Looks up the section of <see cref="WorkloadIdentityOptions"/> that configures a resource.
    /// </summary>
    internal static class WorkloadIdentityOptionsExtensions
    {
        /// <summary>
        /// Gets the section that configures the resource, or <see langword="null"/> when the resource is not configured.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="scope">The resource.</param>
        /// <returns>The section, or <see langword="null"/>.</returns>
        public static WorkloadIdentityResourceOptions? GetResource(this WorkloadIdentityOptions options, TokenScope scope)
        {
            ArgumentNullException.ThrowIfNull(options);

            return scope switch
            {
                TokenScope.AzureSql => options.AzureSql,
                TokenScope.BlobStorage => options.BlobStorage,
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown token scope."),
            };
        }

        /// <summary>
        /// Gets the section that configures the resource.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="scope">The resource.</param>
        /// <returns>The section.</returns>
        /// <exception cref="InvalidOperationException">The resource is not configured.</exception>
        public static WorkloadIdentityResourceOptions GetRequiredResource(this WorkloadIdentityOptions options, TokenScope scope)
        {
            return options.GetResource(scope)
                ?? throw new InvalidOperationException($"The {scope} resource is not configured. Add the '{WorkloadIdentityOptions.ConfigurationSectionName}:{scope}' configuration section, or set {nameof(WorkloadIdentityOptions)}.{scope} when configuring the options in code.");
        }
    }
}
