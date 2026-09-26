using Microsoft.Extensions.DependencyInjection;

namespace OpsPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Phase 1: no application-layer services yet (no MediatR/AutoMapper).
        // This extension point is filled in as Phase 2 introduces ERP use cases.
        return services;
    }
}
