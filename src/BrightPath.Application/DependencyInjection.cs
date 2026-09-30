using BrightPath.Application.Reports;
using BrightPath.Application.Schedule;
using BrightPath.Application.Sessions;
using BrightPath.Application.Tutors;
using Microsoft.Extensions.DependencyInjection;

namespace BrightPath.Application;

public static class DependencyInjection
{
    /// <summary>One handler per use case. They need the ports from Infrastructure, a <c>BookingPolicy</c> and a <c>TimeProvider</c>.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GetScheduleHandler>();
        services.AddScoped<GetTutorDayHandler>();
        services.AddScoped<GetViolationsHandler>();
        services.AddScoped<GetSessionHandler>();
        services.AddScoped<CreateSessionHandler>();
        services.AddScoped<CancelAttendeeHandler>();
        services.AddScoped<MoveSessionHandler>();
        return services;
    }
}
