namespace Neolution.WorkloadIdentity.Internal
{
    /// <summary>
    /// Metadata for a token scope: the corresponding OAuth2 scope identifier URI and provider.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TokenScopeMetadataAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenScopeMetadataAttribute"/> class.
        /// </summary>
        /// <param name="identifier">The OAuth2 scope identifier URI.</param>
        /// <param name="provider">The identity provider.</param>
        public TokenScopeMetadataAttribute(string identifier, IdentityProvider provider)
        {
            this.Identifier = identifier;
            this.Provider = provider;
        }

        /// <summary>
        /// Gets the OAuth2 scope identifier URI.
        /// </summary>
        public string Identifier { get; }

        /// <summary>
        /// Gets the identity provider.
        /// </summary>
        public IdentityProvider Provider { get; }
    }
}
