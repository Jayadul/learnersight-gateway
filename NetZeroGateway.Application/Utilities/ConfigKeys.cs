namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Centralised constants for every IConfiguration key used in the API layer.
/// No magic strings anywhere else — add a constant here, reference it everywhere.
/// </summary>
public static class ConfigKeys
{
    // ── JWT ────────────────────────────────────────────────────────────────
    public const string JwtKey = "Jwt:Key";
    public const string JwtIssuer = "Jwt:Issuer";
    public const string JwtAudience = "Jwt:Audience";
    public const string JwtAccessExpiry = "Jwt:AccessTokenExpiryMinutes";
    public const string JwtRefreshExpiry = "Jwt:RefreshTokenExpiryDays";

    // ── Encryption ─────────────────────────────────────────────────────────
    public const string EncryptionKey = "Encryption:Key";

    // ── CORS ───────────────────────────────────────────────────────────────
    public const string CorsAllowedOrigins = "Cors:AllowedOrigins";

    // ── HTTP Client / Polly ────────────────────────────────────────────────
    public const string TimeoutSeconds = "HttpClients:TimeoutSeconds";
    public const string WhatsAppTimeoutSeconds = "HttpClients:WhatsAppTimeoutSeconds";
    public const string RetryMaxAttempts = "HttpClients:Retry:MaxAttempts";
    public const string RetryDelaySeconds = "HttpClients:Retry:DelaySeconds";

    // ── Provider base URLs ─────────────────────────────────────────────────
    public const string SmsBaseUrl = "Providers:Sms:BaseUrl";
    public const string WhatsAppBaseUrl = "Providers:WhatsApp:BaseUrl";
    public const string VoiceBaseUrl = "Providers:Voice:BaseUrl";

    // ── Authorization / Swagger (authorization.json) ───────────────────────
    public const string GatewayEndpointPath = "Authorization:Gateway:EndpointPath";
    public const string AdminCorsPolicy = "Authorization:Admin:CorsPolicy";
    public const string JwtTokenTypeClaim = "Authorization:Admin:JwtTokenType";
    public const string JwtAccessTypeValue = "Authorization:Admin:JwtAccessTokenValue";
    public const string JwtRefreshTypeValue = "Authorization:Admin:JwtRefreshTokenValue";
    public const string SwaggerGatewayTitle = "Authorization:Swagger:GatewayTitle";
    public const string SwaggerGatewayDesc = "Authorization:Swagger:GatewayDescription";
    public const string SwaggerAdminTitle = "Authorization:Swagger:AdminTitle";
    public const string SwaggerAdminDesc = "Authorization:Swagger:AdminDescription";
    public const string SwaggerSecSchemeId = "Authorization:Swagger:SecuritySchemeId";

    // ── Provider API paths (authorization.json) ────────────────────────────
    public const string SmsApiPath = "Providers:Sms:SendPath";
    public const string WhatsAppSendPath = "Providers:WhatsApp:SendPath";
    public const string WhatsAppTextType = "Providers:WhatsApp:TextType";
    public const string WhatsAppMediaType = "Providers:WhatsApp:MediaType";
    public const string VoiceCampaignPath = "Providers:Voice:CampaignPath";
    public const string VoiceActionId = "Providers:Voice:ActionId";
    public const string VoiceService = "Providers:Voice:Service";
    public const string VoiceDefaultLot = "Providers:Voice:LotNumber";
}