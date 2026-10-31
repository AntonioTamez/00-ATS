namespace Helpdesk.Api.Common;

/// <summary>
/// The only place that lists every slice. Adding a feature = a new folder + one line here.
/// Slices never reference each other.
/// </summary>
public static class FeatureRegistration
{
    public static IServiceCollection AddFeatures(this IServiceCollection services)
    {
        services.AddScoped<Features.OpenTicket.Handler>();
        services.AddScoped<Features.AssignTicket.Handler>();
        services.AddScoped<Features.CloseTicket.Handler>();
        services.AddScoped<Features.GetTicket.Handler>();
        services.AddScoped<Features.ListTickets.Handler>();
        return services;
    }

    public static IEndpointRouteBuilder MapFeatures(this IEndpointRouteBuilder app)
    {
        Features.OpenTicket.Endpoint.Map(app);
        Features.AssignTicket.Endpoint.Map(app);
        Features.CloseTicket.Endpoint.Map(app);
        Features.GetTicket.Endpoint.Map(app);
        Features.ListTickets.Endpoint.Map(app);
        return app;
    }
}
