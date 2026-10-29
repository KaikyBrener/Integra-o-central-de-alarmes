using System.Security.Cryptography;
using System.Text;
using CentralHub.Api.Services;

namespace CentralHub.Api.Middleware;

/// <summary>
/// Exige o header X-Api-Key em todas as requisições, exceto o Swagger.
/// A chave esperada vem da configuração "ApiKey" (variável de ambiente ou User Secrets)
/// e nunca é escrita em log. Se a chave não estiver configurada, nenhuma requisição passa.
/// </summary>
public class ApiKeyMiddleware
{
    public const string HeaderName = "X-Api-Key";

    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/swagger"))
        {
            await _next(context);
            return;
        }

        var esperada = _configuration["ApiKey"];
        var recebida = context.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(esperada) || !ChavesIguais(esperada, recebida))
        {
            // Tratada pelo ExceptionHandlingMiddleware, que devolve o JSON padrão de erro.
            throw new BusinessException($"Chave de API ausente ou inválida. Envie o header {HeaderName}.", statusCode: 401);
        }

        await _next(context);
    }

    // Compara os hashes em tempo constante, para não vazar o tamanho/conteúdo da chave por timing.
    private static bool ChavesIguais(string esperada, string recebida)
    {
        var hashEsperada = SHA256.HashData(Encoding.UTF8.GetBytes(esperada));
        var hashRecebida = SHA256.HashData(Encoding.UTF8.GetBytes(recebida));
        return CryptographicOperations.FixedTimeEquals(hashEsperada, hashRecebida);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKey(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ApiKeyMiddleware>();
    }
}
