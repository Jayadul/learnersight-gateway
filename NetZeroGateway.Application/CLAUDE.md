# NetZeroGateway.Application

Business logic layer. Depends only on Domain — never references Infrastructure.

## Conventions

- Interfaces for all services (e.g. `IAuthService` / `AuthService`)
- Repository interfaces defined here in `Interfaces/` — implemented in Infrastructure
- DTOs in `DTOs/` — request/response types for controllers, never domain entities
- Strategy interface `IChannelSenderFactory` defined here, implemented in Infrastructure

## Key services

- `SendTextOrchestratorService` — core flow: parse request → create MessageRequest → dispatch
- `ChannelDispatchService` — resolves channel sender, executes dispatch
- `FallbackHandlerService` — checks FallbackPolicy rules, retries on alternate channel
- `TemplateParserService` — message template variable substitution
- `AuthService` / `JwtService` — admin login, token generation
- `ManualSendService` — admin-initiated message sends

## Encryption

`IEncryptionService` encrypts/decrypts channel config secrets (AES, key from config).
