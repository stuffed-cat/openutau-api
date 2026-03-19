# Deployment Guide

This API is designed to run as a headless service around OpenUtau Core.

## 1. Runtime requirements

- .NET 8 runtime
- Access to the OpenUtau data directory used by the process account
- File system permissions for projects, voicebanks, dictionaries, and session storage

## 2. Configuration

Configuration can be supplied through `appsettings.json`, environment variables, or the hosting platform.

### Authentication

Authentication is optional.

#### Open mode

Set:

- `Auth:Enabled=false`

In this mode, the API accepts requests without credentials.

#### Protected mode

Set:

- `Auth:Enabled=true`
- `Auth:HeaderName=X-Api-Key` or a custom header name
- `Auth:ApiKey=<strong-secret>`

Requests must include the configured header.

Example:

- `X-Api-Key: OpenUtau-Development-Key`

### Environment variable mapping

Use double underscores for nested keys:

- `Auth__Enabled=true`
- `Auth__HeaderName=X-Api-Key`
- `Auth__ApiKey=replace-with-secret`

### Upload size

Large multipart uploads, including voicebank packages, can be capped by Kestrel and form parsing limits.

Set the maximum request body size in bytes:

- `Upload__MaxRequestBodySizeBytes=524288000`

Set it to `0` to disable the limit.

## 3. Local run

From the repository root:

```bash
dotnet run --project src/OpenUtau.Api/OpenUtau.Api.csproj
```

For open mode:

```bash
Auth__Enabled=false dotnet run --project src/OpenUtau.Api/OpenUtau.Api.csproj
```

For protected mode:

```bash
Auth__Enabled=true Auth__ApiKey=replace-with-secret dotnet run --project src/OpenUtau.Api/OpenUtau.Api.csproj
```

## 4. Reverse proxy

When exposing the API publicly, place it behind a reverse proxy such as Nginx, Caddy, or IIS and terminate TLS at the proxy.

Recommended headers:

- `X-Forwarded-Proto`
- `X-Forwarded-For`

## 5. Docker

If you package the API in a container, pass auth settings as environment variables:

```bash
docker run -e Auth__Enabled=true -e Auth__ApiKey=replace-with-secret openutau-api
docker run -e Auth__Enabled=true -e Auth__ApiKey=replace-with-secret -e Upload__MaxRequestBodySizeBytes=524288000 openutau-api
```

Mount persistent volumes for:

- projects
- sessions
- dictionaries
- voicebanks

## 6. Operational notes

- If authentication is disabled, restrict access at the network layer.
- If authentication is enabled, treat the API key as a secret and rotate it periodically.
- WebSocket and SSE endpoints share the same access policy as the HTTP API.