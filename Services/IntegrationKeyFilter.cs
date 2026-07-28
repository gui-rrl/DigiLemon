using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RankingDigi.Services
{
    /// <summary>
    /// Exige o header <c>X-Integration-Key</c> nos endpoints da integração com o DCGO.
    ///
    /// Importante ser honesto sobre o que isso é: uma chave embutida num aplicativo desktop
    /// distribuído NÃO é segredo (sai com strings/dnSpy/proxy). Ela vale como interruptor geral
    /// — trocar a chave desliga a integração inteira sem mexer em código — e para separar o
    /// tráfego do DCGO nos logs. Quem realmente protege o resultado é o código por slot
    /// (inadivinhável, visível só para o dono) somado à dupla confirmação: um código vazado não
    /// muda resultado sozinho, no máximo gera um conflito que aparece para o organizador.
    /// </summary>
    public class IntegrationKeyFilter : IAsyncActionFilter
    {
        public const string HeaderName = "X-Integration-Key";

        private readonly IConfiguration _config;
        private readonly ILogger<IntegrationKeyFilter> _logger;

        public IntegrationKeyFilter(IConfiguration config, ILogger<IntegrationKeyFilter> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var esperada = _config["Integration:ApiKey"];

            // Fail closed: sem chave configurada a integração fica DESLIGADA, nunca aberta.
            if (string.IsNullOrWhiteSpace(esperada))
            {
                context.Result = new ObjectResult(new { error = "Integração desativada no servidor." })
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                };
                return;
            }

            var informada = context.HttpContext.Request.Headers[HeaderName].ToString();

            // Tempo constante: barato e evita a crítica óbvia de timing attack.
            bool ok = !string.IsNullOrEmpty(informada) && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(informada), Encoding.UTF8.GetBytes(esperada));

            if (!ok)
            {
                _logger.LogWarning("Integração DCGO: chave inválida vinda de {Ip}",
                    context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?");
                context.Result = new ObjectResult(new { error = "Chave de integração inválida." })
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                };
                return;
            }

            await next();
        }
    }
}
