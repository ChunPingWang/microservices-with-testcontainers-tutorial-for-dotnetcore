using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ProductService.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var name = typeof(TRequest).Name;
        logger.LogInformation("Handling {Request}", name);
        try
        {
            var response = await next(ct);
            logger.LogInformation("Handled {Request} in {Elapsed}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed {Request} after {Elapsed}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}

public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var validatorList = validators.ToList();
        if (validatorList.Count == 0)
            return await next(ct);

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validatorList.Select(v => v.ValidateAsync(context, ct)));
        var failures = results.SelectMany(r => r.Errors).Where(f => f != null).ToList();
        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next(ct);
    }
}
