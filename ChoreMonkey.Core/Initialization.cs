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
using ChoreMonkey.Core.Feature.OverdueChores;
using ChoreMonkey.Core.Feature.CompletionTimeline;
using ChoreMonkey.Core.Feature.DeleteChore;
using ChoreMonkey.Core.Feature.SetAdminPin;
using ChoreMonkey.Core.Feature.SetMemberPin;
using ChoreMonkey.Core.Feature.MyChores;
using ChoreMonkey.Core.Feature.AcknowledgeMissed;
using ChoreMonkey.Core.Infrastructure;
using ChoreMonkey.Core.Infrastructure.SignalR;

namespace ChoreMonkey.Core;

public static class Initialization
{
    public static IServiceCollection AddChoreMonkeyCore(this IServiceCollection services)
    {
        // Use environment variable for data path, default to ./data for local dev
        var dataPath = Environment.GetEnvironmentVariable("EVENTSTORE_PATH") 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "data");
        
        // Add MediatR for event publishing
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<HouseholdHub>());
        
        // Add FileEventStore first (will be decorated)
        services.AddFileEventStore(dataPath);
        
        // Decorate with PublishingEventStore to broadcast events via MediatR
        services.Decorate<IEventStore, PublishingEventStore>();
        
        // Add SignalR
        services.AddSignalR();
        
        return services.InstallFeatures();
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
        services.AddScoped<Feature.OverdueChores.Handler>();
        services.AddScoped<Feature.CompletionTimeline.Handler>();
        services.AddScoped<Feature.MyChores.Handler>();
        services.AddScoped<Feature.AcknowledgeMissed.Handler>();
        services.AddScoped<Feature.DeleteChore.Handler>();
        services.AddScoped<Feature.SetAdminPin.Handler>();
        services.AddScoped<Feature.SetMemberPin.Handler>();
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
        OverdueChoresEndpoint.Map(householdEndpoints);
        CompletionTimelineEndpoint.Map(householdEndpoints);
        MyChoresEndpoint.Map(householdEndpoints);
        AcknowledgeMissedEndpoint.Map(householdEndpoints);
        DeleteChoreEndpoint.Map(householdEndpoints);
        SetAdminPinEndpoint.Map(householdEndpoints);
        SetMemberPinEndpoint.Map(householdEndpoints);

        return app;
    }

    public static IEndpointRouteBuilder MapChoreMonkeyHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<HouseholdHub>("/hubs/household");
        return app;
    }
}
