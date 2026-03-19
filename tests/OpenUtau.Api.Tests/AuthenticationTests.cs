using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using OpenUtau.Api.Security;

namespace OpenUtau.Api.Tests
{
    public class AuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AuthenticationTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RequestWithoutAuthConfigIsAllowed()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/preferences");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void ApiKeyDefaultsAreDocumented()
        {
            Assert.Equal("X-Api-Key", ApiKeyAuthenticationOptions.DefaultHeaderName);
            Assert.False(string.IsNullOrWhiteSpace(ApiKeyAuthenticationOptions.DefaultApiKey));
        }
    }
}