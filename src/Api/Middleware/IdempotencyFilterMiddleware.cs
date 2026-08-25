using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Api.Middleware;

public class IdempotencyFilterMiddleware : IAsyncActionFilter
{
    private readonly ICacheService _cacheService;
    private const string HeaderName = "Idempotency-Key";

    public IdempotencyFilterMiddleware(ICacheService cache)
    {
        _cacheService = cache;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Verifica se o header de idempotência foi enviado
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var idempotencyKey))
        {
            throw new BadRequestException($"O header '{HeaderName}' é obrigatório para requisições idempotentes.");
        }

        string cacheKey = $"idempotency:{idempotencyKey}";

        // 1. Verifica se já existe uma resposta processada para esta chave
        var cachedResponse = await _cacheService.GetAsync<IdempotentResponseModel>(cacheKey);
        if (cachedResponse != null)
        {
            // Retorna exatamente a mesma resposta anterior sem reprocessar
            context.Result = new ObjectResult(cachedResponse.Body)
            {
                StatusCode = cachedResponse.StatusCode
            };

            return;
        }

        // 2. Executa a ação da Controller
        var executedContext = await next();

        // 3. Captura o resultado gerado e salva no cache
        if (executedContext.Result is ObjectResult objectResult)
        {
            var responseModel = new IdempotentResponseModel
            {
                StatusCode = objectResult.StatusCode ?? 200,
                Body = objectResult.Value
            };

            var serializedResponse = JsonSerializer.Serialize(responseModel);

            await _cacheService.SetAsync(cacheKey, serializedResponse, TimeSpan.FromMinutes(6));
        }
    }
}

public class IdempotentResponseModel
{
    public int StatusCode { get; set; }

    // ObjectResult.Value é anulável (ex.: NoContent), então o corpo replayed também é.
    public object? Body { get; set; }
}
