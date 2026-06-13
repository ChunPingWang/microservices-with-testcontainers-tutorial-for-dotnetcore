using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PaymentService.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try { return await next(ct); }
        finally { logger.LogInformation("{Req} took {Ms}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds); }
    }
}

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var list = validators.ToList();
        if (list.Count > 0)
        {
            var failures = (await Task.WhenAll(list.Select(v =>
                v.ValidateAsync(new ValidationContext<TRequest>(request), ct))))
                .SelectMany(r => r.Errors).Where(f => f != null).ToList();
            if (failures.Count > 0) throw new ValidationException(failures);
        }
        return await next(ct);
    }
}
