using System.Net;
using System.Text.Json;
using CentralHub.Api.Services;

namespace CentralHub.Api.Middleware;

/// <summary>
/// Captura qualquer exceção não tratada no pipeline da API, registra o erro
/// via ILogger e retorna uma resposta JSON padronizada ao cliente, evitando
/// vazar detalhes internos (stack trace, mensagens de infraestrutura, etc.).
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (BusinessException ex)
        {
            // Erro de regra de negócio: esperado, não é logado como erro, apenas informativo.
            _logger.LogInformation("Regra de negócio violada em {Method} {Path}: {Mensagem}", context.Request.Method, context.Request.Path, ex.Message);
            await EscreverRespostaAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado ao processar {Method} {Path}", context.Request.Method, context.Request.Path);
            await EscreverRespostaAsync(context, (int)HttpStatusCode.InternalServerError, "Ocorreu um erro interno ao processar a requisição.");
        }
    }

    private static async Task EscreverRespostaAsync(HttpContext context, int statusCode, string mensagem)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var resposta = new
        {
            status = statusCode,
            mensagem
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(resposta));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
