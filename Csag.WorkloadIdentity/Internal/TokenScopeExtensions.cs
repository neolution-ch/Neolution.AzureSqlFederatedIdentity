namespace Csag.WorkloadIdentity.Internal
{
    using System.Collections.Concurrent;
    using System.Reflection;

    /// <summary>
    /// Reads the metadata attached to <see cref="TokenScope"/> members.
    /// </summary>
    internal static class TokenScopeExtensions
    {
        /// <summary>
        /// The scope identifier of every member read so far, so that reflection runs once per member.
        /// </summary>
        private static readonly ConcurrentDictionary<TokenScope, string> Identifiers = new();

        /// <summary>
        /// Gets the OAuth 2.0 scope requested from Microsoft Entra ID for the resource.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <returns>The OAuth 2.0 scope.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not a member carrying <see cref="TokenScopeMetadataAttribute"/>.</exception>
        public static string GetIdentifier(this TokenScope scope)
        {
            return Identifiers.GetOrAdd(scope, ReadIdentifier);
        }

        /// <summary>
        /// Reads the scope identifier from the member's attribute.
        /// </summary>
        /// <param name="scope">The resource.</param>
        /// <returns>The OAuth 2.0 scope.</returns>
        private static string ReadIdentifier(TokenScope scope)
        {
            var attribute = typeof(TokenScope).GetField(scope.ToString())?.GetCustomAttribute<TokenScopeMetadataAttribute>();
            return attribute?.Identifier
                ?? throw new ArgumentOutOfRangeException(nameof(scope), scope, $"The value is not a {nameof(TokenScope)} member carrying {nameof(TokenScopeMetadataAttribute)}.");
        }
    }
}
