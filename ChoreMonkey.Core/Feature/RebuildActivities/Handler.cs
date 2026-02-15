using ChoreMonkey.Core.Infrastructure.ReadModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.RebuildActivities;

public record RebuildActivitiesCommand(Guid HouseholdId);
public record RebuildActivitiesResponse(int ActivityCount);

internal class Handler(IActivityReadModel activityReadModel)
{
    public async Task<RebuildActivitiesResponse> HandleAsync(RebuildActivitiesCommand request)
    {
        await activityReadModel.RebuildAsync(request.HouseholdId);
        
        // Get the rebuilt activities to return count
        var activities = await activityReadModel.GetActivitiesAsync(request.HouseholdId, days: null, limit: null);
        
        return new RebuildActivitiesResponse(activities.Count);
    }
}

public static class RebuildActivitiesEndpoint
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("/households/{householdId}/activities/rebuild", async (
            Guid householdId,
            Handler handler) =>
        {
            var response = await handler.HandleAsync(new RebuildActivitiesCommand(householdId));
            return Results.Ok(response);
        });
    }
}
