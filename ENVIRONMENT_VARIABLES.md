# Environment Variables Reference

This project reads configuration values from appsettings files and overrides them with environment variables using ASP.NET Core's `AddEnvironmentVariables()`.

Set the following keys when configuring the service (e.g. in systemd `Environment=` entries or an environment file):

| Purpose | Environment Variable Key |
| --- | --- |
| Database connection string | `ConnectionStrings__CoreDatabase` |
| Qdrant HTTP endpoint | `Qdrant__UrlHttp` |
| Qdrant gRPC endpoint | `Qdrant__UrlGrpc` |
| Serilog Seq URL | `Serilog__WriteTo__2__Args__serverUrl` |
| AI description API key | `Ai__DescriptionApiKey` |
| AI embedding API key | `Ai__EmbeddingApiKey` |
| Twitch client ID | `Twitch__ClientId` |
| Twitch client secret | `Twitch__ClientSecret` |

> The double underscore (`__`) maps to nested configuration sections (e.g. `Qdrant:UrlHttp`). Adjust the values to match your deployment.
