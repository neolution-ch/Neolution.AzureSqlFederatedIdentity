namespace Neolution.WorkloadIdentity.Options
{
    /// <summary>
    /// Represents workload-identity options that select a provider and include nested provider-specific settings.
    /// </summary>
    public interface IProviderOptions
    {
        /// <summary>
        /// Gets the selected workload identity provider.
        /// </summary>
        WorkloadIdentityProvider Provider { get; }

        /// <summary>
        /// Gets the Managed Identity options.
        /// </summary>
        ManagedIdentityOptions ManagedIdentity { get; }

        /// <summary>
        /// Gets the Google federated identity options.
        /// </summary>
        GoogleOptions Google { get; }
    }
}
