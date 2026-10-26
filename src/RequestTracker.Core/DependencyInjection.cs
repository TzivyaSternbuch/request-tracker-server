using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using RequestTracker.Core.Models;
using RequestTracker.Core.Services;
using RequestTracker.Core.Validation;

namespace RequestTracker.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddHybridCache();

        services.AddScoped<IValidator<RequestQueryParameters>, RequestQueryParametersValidator>();
        services.AddScoped<IRequestService, RequestService>();

        return services;
    }
}
