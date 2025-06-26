namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    using System;
    using System.Collections.Generic;
    using Neolution.AzureSqlFederatedIdentity.Abstractions;
    using Neolution.AzureSqlFederatedIdentity.Options;

    /// <summary>
    /// Factory for resolving the correct Workload Identity Token Exchanger based on provider.
    /// </summary>
    public class WorkloadIdentityTokenExchangerFactory
    {
        /// <summary>
        /// The service provider for dependency resolution.
        /// </summary>
        private readonly IServiceProvider serviceProvider;

        /// <summary>
        /// A dictionary mapping providers to factory delegates.
        /// </summary>
        private readonly IReadOnlyDictionary<WorkloadIdentityProvider, Func<IServiceProvider, IWorkloadIdentityTokenExchanger>> exchangerFactories;

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkloadIdentityTokenExchangerFactory"/> class.
        /// </summary>
        /// <param name="serviceProvider">The service provider for dependency resolution.</param>
        /// <param name="exchangerFactories">A dictionary mapping providers to factory delegates.</param>
        internal WorkloadIdentityTokenExchangerFactory(
            IServiceProvider serviceProvider,
            IReadOnlyDictionary<WorkloadIdentityProvider, Func<IServiceProvider, IWorkloadIdentityTokenExchanger>> exchangerFactories)
        {
            this.serviceProvider = serviceProvider;
            this.exchangerFactories = exchangerFactories;
        }

        /// <summary>
        /// Resolves the correct concrete token exchanger based on the provider.
        /// </summary>
        /// <param name="provider">The provider enum.</param>
        /// <returns>The concrete token exchanger instance.</returns>
        public IWorkloadIdentityTokenExchanger Create(WorkloadIdentityProvider provider)
        {
            if (!this.exchangerFactories.TryGetValue(provider, out var factory))
            {
                throw new ArgumentException("Unknown or unregistered provider", nameof(provider));
            }

            return factory(this.serviceProvider);
        }
    }
}
