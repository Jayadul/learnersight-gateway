# NetZeroGateway.Infrastructure

Implements Application interfaces. Owns persistence, HTTP clients, and channel strategies.

## EF Core

- `Persistence/NetZeroGatewayDbContext.cs` — DbContext with snake_case naming (EFCore.NamingConventions)
- `Persistence/DataSeeder.cs` — seeds admin users, API keys, channel configs, fallback policies from appsettings
- `Persistence/Migrations/` — code-first migrations (auto-applied on startup)
- PostgreSQL via Npgsql

## HTTP clients

Typed HttpClient interfaces with Polly retry policies (configured in DI):
- `IReveSmsClient` — REVE SMS provider
- `IWaTrendClient` — WaTrend WhatsApp provider
- `INetZeroVoiceClient` — NetZero Voice provider
- Response models in `HttpClients/Models/`

## Services

- `Services/BackupExportService.cs` — xlsx export (ClosedXML) for the admin backup download
- `Services/LocalMediaStorageService.cs` — Media Library files on local disk (`Media:StoragePath`), path-traversal safe

## Channel sender strategy

`Strategies/ChannelSenderFactory.cs` resolves the correct sender by ChannelType:
- `SmsSender` → IReveSmsClient
- `WhatsAppTextSender` → IWaTrendClient
- `WhatsAppMediaSender` → IWaTrendClient
- `VoiceSender` → INetZeroVoiceClient

## Repositories

EF Core implementations of Application-layer repository interfaces in `Repositories/`.
