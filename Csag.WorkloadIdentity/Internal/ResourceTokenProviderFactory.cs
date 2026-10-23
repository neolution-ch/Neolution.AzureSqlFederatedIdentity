namespace Csag.WorkloadIdentity.Internal
{
    using Csag.WorkloadIdentity.Options;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    /// <summary>
    /// Creates the token holder of a resource, wired to the exchanger of the provider the resource's options select.
    /// </summary>
    internal sealed class ResourceTokenProviderFactory
    {
        /// <summary>
        /// The options naming the provider of each resource.
        /// </summary>
        private readonly WorkloadIdentityOptions options;

        /// <summary>
        /// The registered exchangers, one per identity provider.
        /// </summary>
        private readonly IEnumerable<IWorkloadIdentityTokenExchanger> exchangers;

        /// <summary>
        /// The clock the holders judge token lifetime with.
        /// </summary>
        private readonly TimeProvider timeProvider;

        /// <summary>
        /// Creates the holders' loggers.
        /// </summary>
        private readonly ILoggerFactory loggerFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResourceTokenProviderFactory"/> class.
        /// </summary>
        /// <param name="options">The workload identity options.</param>
        /// <param name="exchangers">The registered exchangers, one per identity provider.</param>
        /// <param name="timeProvider">The clock the holders judge token lifetime with.</param>
        /// <param name="loggerFactory">Creates the holders' loggers.</param>
        public ResourceTokenProviderFactory(
            IOptions<WorkloadIdentityOptions> options,
            IEnumerable<IWorkloadIdentityTokenExchanger> exchangers,
            TimeProvider timeProvider,
            ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(options);

            this.options = options.Value;
            this.exchangers = exchangers;
            this.timeProvider = timeProvider;
            this.loggerFactory = loggerFactory;
        }

        /// <summary>
        /// Creates the token holder of the resource.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <returns>The token holder, owned by the caller.</returns>
        /// <exception cref="InvalidOperationException">The resource is not configured, or no exchanger is registered for its provider.</exception>
        public ResourceTokenProvider Create(TokenScope scope)
        {
            var resource = this.options.GetRequiredResource(scope);
            var exchanger = this.exchangers.FirstOrDefault(candidate => candidate.Provider == resource.Provider)
                ?? throw new InvalidOperationException($"No token exchanger is registered for the {resource.Provider} provider selected by the {scope} resource.");

            return new ResourceTokenProvider(scope, exchanger, this.options.RefreshAheadWindow, this.timeProvider, this.loggerFactory.CreateLogger<ResourceTokenProvider>());
        }
    }
}
