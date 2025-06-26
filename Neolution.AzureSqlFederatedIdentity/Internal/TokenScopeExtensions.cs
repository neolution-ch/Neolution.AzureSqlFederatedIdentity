namespace Neolution.AzureSqlFederatedIdentity.Internal
{
    using System.Linq;

    /// <summary>
    /// Extension methods for reading OAuth2 scope and options name metadata from the TokenScope enum.
    /// </summary>
    public static class TokenScopeExtensions
    {
        /// <summary>
        /// Gets the options name associated with the specified <see cref="TokenScope"/>.
        /// </summary>
        /// <param name="scope">The token scope.</param>
        /// <returns>The options name.</returns>
        public static string GetOptionsName(this TokenScope scope)
        {
            return scope.GetMetadataAttribute().OptionsName;
        }

        /// <summary>
        /// Gets the identifier associated with the specified <see cref="TokenScope"/>.
        /// </summary>
        /// <param name="scope">The token scope.</param>
        /// <returns>The identifier.</returns>
        public static string GetIdentifier(this TokenScope scope)
        {
            return scope.GetMetadataAttribute().Identifier;
        }

        /// <summary>
        /// Retrieves the <see cref="TokenScopeMetadataAttribute"/> associated with the specified <see cref="TokenScope"/>.
        /// </summary>
        /// <param name="scope">The token scope.</param>
        /// <returns>The metadata attribute.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the field for the scope is not found or if the metadata attribute is not present.
        /// </exception>
        private static TokenScopeMetadataAttribute GetMetadataAttribute(this TokenScope scope)
        {
            var field = scope.GetType().GetField(scope.ToString());
            if (field == null)
            {
                throw new InvalidOperationException($"Field for scope '{scope}' not found.");
            }

            if (field.GetCustomAttributes(typeof(TokenScopeMetadataAttribute), false).SingleOrDefault() is not TokenScopeMetadataAttribute attr)
            {
                throw new InvalidOperationException($"TokenScopeMetadataAttribute not found for scope '{scope}'.");
            }

            return attr;
        }
    }
}
