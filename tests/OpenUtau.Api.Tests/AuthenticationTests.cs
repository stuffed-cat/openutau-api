using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
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
        public async Task RequestWithoutApiKeyIsRejected()
        {
            using var client = _factory.CreateClient();

            var response = await client.GetAsync("/api/preferences");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RequestWithApiKeyIsAllowed()
        {
            using var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add(ApiKeyAuthenticationOptions.DefaultHeaderName, ApiKeyAuthenticationOptions.DefaultApiKey);

            var response = await client.GetAsync("/api/preferences");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}