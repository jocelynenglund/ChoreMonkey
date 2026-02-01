using FileEventStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using ChoreMonkey.Core.Feature.CreateHousehold;
using ChoreMonkey.Core.Feature.HouseholdName;
using ChoreMonkey.Core.Feature.AddChore;
using ChoreMonkey.Core.Feature.ChoreList;
using ChoreMonkey.Core.Feature.GenerateInvite;
using ChoreMonkey.Core.Feature.InviteLink;
using ChoreMonkey.Core.Feature.AccessHousehold;
namespace ChoreMonkey.Core;

public static class Initialization
{
    public static IServiceCollection AddChoreMonkeyCore(this IServiceCollection services)
    {
        return services.AddFileEventStore(Path.Combine(Directory.GetCurrentDirectory(), "data"))
            .InstallFeatures();

    }
    public static IServiceCollection InstallFeatures(this IServiceCollection services)
    {
        services.AddScoped<Feature.CreateHousehold.Handler>();
        services.AddScoped<Feature.HouseholdName.Handler>();
        services.AddScoped<Feature.AddChore.Handler>();
        services.AddScoped<Feature.ChoreList.Handler>();
        services.AddScoped<Feature.GenerateInvite.Handler>();
        services.AddScoped<Feature.InviteLink.Handler>();
        services.AddScoped<Feature.AccessHousehold.Handler>();
        return services;
    }
    public static  IEndpointRouteBuilder MapChoreMonkeyEndpoints(this IEndpointRouteBuilder app)
    {
        var householdEndpoints = app.MapGroup("/api")
            .WithTags("household");

        CreateHouseholdEndpoint.Map(householdEndpoints);
        HouseholdNameEndpoint.Map(householdEndpoints);
        AddChoreEndpoint.Map(householdEndpoints);
        ChoreListEndpoint.Map(householdEndpoints);
        GenerateInviteEndpoint.Map(householdEndpoints);
        InviteLinkEndpoint.Map(householdEndpoints);
        AccessHouseholdEndpoint.Map(householdEndpoints);

        return app;
    }

}