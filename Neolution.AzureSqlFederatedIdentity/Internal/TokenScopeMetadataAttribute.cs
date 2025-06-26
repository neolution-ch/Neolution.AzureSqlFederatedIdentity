namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    /// <summary>
    /// Metadata for a token scope: the options name and the corresponding OAuth2 scope identifier URI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class TokenScopeMetadataAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenScopeMetadataAttribute"/> class.
        /// </summary>
        /// <param name="optionsName">The name of the options associated with the token scope.</param>
        /// <param name="identifier">The OAuth2 scope identifier URI.</param>
        public TokenScopeMetadataAttribute(string optionsName, string identifier)
        {
            this.OptionsName = optionsName;
            this.Identifier = identifier;
        }

        /// <summary>
        /// Gets the name of the options associated with the token scope.
        /// </summary>
        public string OptionsName { get; }

        /// <summary>
        /// Gets the OAuth2 scope identifier URI.
        /// </summary>
        public string Identifier { get; }
    }
}
