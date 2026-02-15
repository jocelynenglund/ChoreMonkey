using FileEventStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

// Household module
using ChoreMonkey.Core.Feature.Household.Commands.CreateHousehold;
using ChoreMonkey.Core.Feature.Household.Commands.SetAdminPin;
using ChoreMonkey.Core.Feature.Household.Commands.SetMemberPin;
using ChoreMonkey.Core.Feature.Household.Queries.HouseholdName;
using ChoreMonkey.Core.Feature.Household.Queries.AccessHousehold;

// Members module
using ChoreMonkey.Core.Feature.Members.Commands.JoinHousehold;
using ChoreMonkey.Core.Feature.Members.Commands.RemoveMember;
using ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberNickname;
using ChoreMonkey.Core.Feature.Members.Commands.ChangeMemberStatus;
using ChoreMonkey.Core.Feature.Members.Queries.ListMembers;
using ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;

// Invites module
using ChoreMonkey.Core.Feature.Invites.Commands.GenerateInvite;
using ChoreMonkey.Core.Feature.Invites.Queries.InviteLink;

// Chores module
using ChoreMonkey.Core.Feature.Chores.Commands.AddChore;
using ChoreMonkey.Core.Feature.Chores.Commands.AssignChore;
using ChoreMonkey.Core.Feature.Chores.Commands.CompleteChore;
using ChoreMonkey.Core.Feature.Chores.Commands.DeleteChore;
using ChoreMonkey.Core.Feature.Chores.Commands.AcknowledgeMissed;
using ChoreMonkey.Core.Feature.Chores.Queries.ChoreList;
using ChoreMonkey.Core.Feature.Chores.Queries.ChoreHistory;
using ChoreMonkey.Core.Feature.Chores.Queries.MyChores;
using ChoreMonkey.Core.Feature.Chores.Queries.OverdueChores;

// Activity module
using ChoreMonkey.Core.Feature.Activity.Queries.CompletionTimeline;
using ChoreMonkey.Core.Feature.Activity.Queries.TeamOverview;

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
        // Household module
        services.AddScoped<Feature.Household.Commands.CreateHousehold.Handler>();
        services.AddScoped<Feature.Household.Commands.SetAdminPin.Handler>();
        services.AddScoped<Feature.Household.Commands.SetMemberPin.Handler>();
        services.AddScoped<Feature.Household.Queries.HouseholdName.Handler>();
        services.AddScoped<Feature.Household.Queries.AccessHousehold.Handler>();

        // Members module
        services.AddScoped<Feature.Members.Commands.JoinHousehold.Handler>();
        services.AddScoped<Feature.Members.Commands.RemoveMember.Handler>();
        services.AddScoped<Feature.Members.Commands.ChangeMemberNickname.Handler>();
        services.AddScoped<Feature.Members.Commands.ChangeMemberStatus.Handler>();
        services.AddScoped<Feature.Members.Queries.ListMembers.Handler>();
        services.AddScoped<Feature.Members.Queries.MemberLookup.Handler>();

        // Invites module
        services.AddScoped<Feature.Invites.Commands.GenerateInvite.Handler>();
        services.AddScoped<Feature.Invites.Queries.InviteLink.Handler>();

        // Chores module
        services.AddScoped<Feature.Chores.Commands.AddChore.Handler>();
        services.AddScoped<Feature.Chores.Commands.AssignChore.Handler>();
        services.AddScoped<Feature.Chores.Commands.CompleteChore.Handler>();
        services.AddScoped<Feature.Chores.Commands.DeleteChore.Handler>();
        services.AddScoped<Feature.Chores.Commands.AcknowledgeMissed.Handler>();
        services.AddScoped<Feature.Chores.Queries.ChoreList.Handler>();
        services.AddScoped<Feature.Chores.Queries.ChoreHistory.Handler>();
        services.AddScoped<Feature.Chores.Queries.MyChores.Handler>();
        services.AddScoped<Feature.Chores.Queries.OverdueChores.Handler>();

        // Activity module
        services.AddScoped<Feature.Activity.Queries.CompletionTimeline.Handler>();
        services.AddScoped<Feature.Activity.Queries.TeamOverview.Handler>();

        return services;
    }

    public static IEndpointRouteBuilder MapChoreMonkeyEndpoints(this IEndpointRouteBuilder app)
    {
        var householdEndpoints = app.MapGroup("/api")
            .WithTags("household");

        // Household module
        CreateHouseholdEndpoint.Map(householdEndpoints);
        SetAdminPinEndpoint.Map(householdEndpoints);
        SetMemberPinEndpoint.Map(householdEndpoints);
        HouseholdNameEndpoint.Map(householdEndpoints);
        AccessHouseholdEndpoint.Map(householdEndpoints);

        // Members module
        JoinHouseholdEndpoint.Map(householdEndpoints);
        RemoveMemberEndpoint.Map(householdEndpoints);
        ChangeMemberNicknameEndpoint.Map(householdEndpoints);
        ChangeMemberStatusEndpoint.Map(householdEndpoints);
        ListMembersEndpoint.Map(householdEndpoints);

        // Invites module
        GenerateInviteEndpoint.Map(householdEndpoints);
        InviteLinkEndpoint.Map(householdEndpoints);

        // Chores module
        AddChoreEndpoint.Map(householdEndpoints);
        AssignChoreEndpoint.Map(householdEndpoints);
        CompleteChoreEndpoint.Map(householdEndpoints);
        DeleteChoreEndpoint.Map(householdEndpoints);
        AcknowledgeMissedEndpoint.Map(householdEndpoints);
        ChoreListEndpoint.Map(householdEndpoints);
        ChoreHistoryEndpoint.Map(householdEndpoints);
        MyChoresEndpoint.Map(householdEndpoints);
        OverdueChoresEndpoint.Map(householdEndpoints);

        // Activity module
        CompletionTimelineEndpoint.Map(householdEndpoints);
        TeamOverviewEndpoint.Map(householdEndpoints);

        return app;
    }

    public static IEndpointRouteBuilder MapChoreMonkeyHub(this IEndpointRouteBuilder app)
    {
        app.MapHub<HouseholdHub>("/hubs/household");
        return app;
    }
}
