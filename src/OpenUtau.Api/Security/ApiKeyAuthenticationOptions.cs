using Microsoft.AspNetCore.Authentication;

namespace OpenUtau.Api.Security
{
    public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string Scheme = "ApiKey";
        public const string DefaultHeaderName = "X-Api-Key";
        public const string DefaultApiKey = "OpenUtau-Development-Key";

        public string HeaderName { get; set; } = DefaultHeaderName;
        public string ApiKey { get; set; } = DefaultApiKey;
    }
}