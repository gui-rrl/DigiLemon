using System.Threading.RateLimiting;

namespace RankingDigi.Services
{
    /// <summary>
    /// Teto de requisições da integração com o DCGO.
    ///
    /// A partição é o CÓDIGO, não o IP: o código é a credencial de verdade (é ele que precisa
    /// de teto contra tentativa e erro), e limitar por IP não funcionaria aqui — atrás do ngrok
    /// todo mundo chega pelo mesmo endereço de origem.
    /// </summary>
    public static class IntegrationRateLimit
    {
        public const string PolicyName = "integration-code";

        public static void AddIntegrationRateLimiter(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.AddPolicy(PolicyName, http => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: http.Request.RouteValues["code"]?.ToString()?.ToUpperInvariant() ?? "sem-codigo",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromHours(1),
                    }));
            });
        }
    }
}
