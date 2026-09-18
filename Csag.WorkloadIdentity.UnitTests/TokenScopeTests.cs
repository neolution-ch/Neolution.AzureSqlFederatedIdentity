namespace Csag.WorkloadIdentity.UnitTests
{
    using System.Reflection;
    using Csag.WorkloadIdentity.Internal;
    using Shouldly;

    /// <summary>
    /// Unit tests for the <see cref="TokenScope"/> metadata and the <see cref="TokenScopeExtensions"/> that read it.
    /// </summary>
    public class TokenScopeTests
    {
        /// <summary>
        /// Verifies that each scope resolves to the OAuth 2.0 scope named by its metadata.
        /// </summary>
        /// <param name="scope">The token scope.</param>
        /// <param name="expected">The OAuth 2.0 scope expected for it.</param>
        [Theory]
        [InlineData(TokenScope.AzureSql, "https://database.windows.net/.default")]
        [InlineData(TokenScope.BlobStorage, "https://storage.azure.com/.default")]
        public void Given_Scope_When_GetIdentifier_Then_ReturnsScopeFromMetadata(TokenScope scope, string expected)
        {
            // Act
            var result = scope.GetIdentifier();

            // Assert
            result.ShouldBe(expected);
        }

        /// <summary>
        /// Verifies that every member of the enum carries metadata, so that a new resource cannot be added without its scope.
        /// </summary>
        [Fact]
        public void Given_EveryMember_When_ReadingMetadata_Then_EachCarriesAnIdentifier()
        {
            foreach (var scope in Enum.GetValues<TokenScope>())
            {
                // Act
                var attribute = typeof(TokenScope).GetField(scope.ToString()).ShouldNotBeNull().GetCustomAttribute<TokenScopeMetadataAttribute>();

                // Assert
                attribute.ShouldNotBeNull().Identifier.ShouldStartWith("https://");
                scope.GetIdentifier().ShouldBe(attribute.Identifier);
            }
        }

        /// <summary>
        /// Verifies that a value outside the enum is rejected instead of silently yielding an empty scope.
        /// </summary>
        [Fact]
        public void Given_UndefinedScope_When_GetIdentifier_Then_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            var scope = (TokenScope)42;

            // Act
            var exception = Should.Throw<ArgumentOutOfRangeException>(() => scope.GetIdentifier());

            // Assert
            exception.ParamName.ShouldBe("scope");
        }
    }
}
