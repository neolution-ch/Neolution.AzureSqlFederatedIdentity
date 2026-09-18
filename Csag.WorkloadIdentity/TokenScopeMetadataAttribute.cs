namespace Csag.WorkloadIdentity
{
    /// <summary>
    /// Names the OAuth 2.0 scope that is requested from Microsoft Entra ID for a <see cref="TokenScope"/> member.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TokenScopeMetadataAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TokenScopeMetadataAttribute"/> class.
        /// </summary>
        /// <param name="identifier">The OAuth 2.0 scope, for example <c>https://database.windows.net/.default</c>.</param>
        public TokenScopeMetadataAttribute(string identifier)
        {
            ArgumentNullException.ThrowIfNull(identifier);

            this.Identifier = identifier;
        }

        /// <summary>
        /// Gets the OAuth 2.0 scope requested for the resource.
        /// </summary>
        public string Identifier { get; }
    }
}
