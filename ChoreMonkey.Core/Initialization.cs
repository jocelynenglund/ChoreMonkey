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
using ChoreMonkey.Core.Feature.JoinHousehold;
using ChoreMonkey.Core.Feature.ListMembers;
using ChoreMonkey.Core.Feature.AssignChore;
using ChoreMonkey.Core.Feature.CompleteChore;
using ChoreMonkey.Core.Feature.ChoreHistory;

namespace ChoreMonkey.Core;

public static class Initialization
{
    public static IServiceCollection AddChoreMonkeyCore(this IServiceCollection services)
    {
        // Use environment variable for data path, default to ./data for local dev
        var dataPath = Environment.GetEnvironmentVariable("EVENTSTORE_PATH") 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        return services.AddFileEventStore(dataPath)
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
        services.AddScoped<Feature.JoinHousehold.Handler>();
        services.AddScoped<Feature.ListMembers.Handler>();
        services.AddScoped<Feature.AssignChore.Handler>();
        services.AddScoped<Feature.CompleteChore.Handler>();
        services.AddScoped<Feature.ChoreHistory.Handler>();
        return services;
    }
    public static IEndpointRouteBuilder MapChoreMonkeyEndpoints(this IEndpointRouteBuilder app)
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
        JoinHouseholdEndpoint.Map(householdEndpoints);
        ListMembersEndpoint.Map(householdEndpoints);
        AssignChoreEndpoint.Map(householdEndpoints);
        CompleteChoreEndpoint.Map(householdEndpoints);
        ChoreHistoryEndpoint.Map(householdEndpoints);

        return app;
    }

}
