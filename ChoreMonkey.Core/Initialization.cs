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
using ChoreMonkey.Core.Feature.ChangeMemberNickname;
using ChoreMonkey.Core.Feature.ChangeMemberStatus;
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
        
        // Add MediatR for event broadcasting
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<HouseholdHub>());
        
        // Add SignalR
        services.AddSignalR();
        
        // Add FileEventStore with PublishingEventStore decorator
        services.AddFileEventStore(dataPath);
        
        // Manually decorate IEventStore with PublishingEventStore
        var innerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(FileEventStore.IEventStore));
        if (innerDescriptor != null)
        {
            services.Remove(innerDescriptor);
            services.AddScoped<FileEventStore.IEventStore>(sp =>
            {
                var inner = innerDescriptor.ImplementationType != null 
                    ? (FileEventStore.IEventStore)ActivatorUtilities.CreateInstance(sp, innerDescriptor.ImplementationType)
                    : innerDescriptor.ImplementationFactory != null
                        ? (FileEventStore.IEventStore)innerDescriptor.ImplementationFactory(sp)
                        : throw new InvalidOperationException("Cannot create IEventStore");
                var publisher = sp.GetRequiredService<MediatR.IPublisher>();
                return new PublishingEventStore(inner, publisher);
            });
        }
        
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
        services.AddScoped<Feature.ChangeMemberNickname.Handler>();
        services.AddScoped<Feature.ChangeMemberStatus.Handler>();
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
        ChangeMemberNicknameEndpoint.Map(householdEndpoints);
        ChangeMemberStatusEndpoint.Map(householdEndpoints);

        return app;
    }

    public static IEndpointRouteBuilder MapChoreMonkeyHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<HouseholdHub>("/hubs/household");
        return app;
    }
}
