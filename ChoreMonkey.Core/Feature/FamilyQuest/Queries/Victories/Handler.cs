using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace ChoreMonkey.Core.Feature.FamilyQuest.Queries.Victories;

public record GetVictoriesQuery(Guid HouseholdId, int Limit = 10);

public record VictoryDto(
    Guid ChoreId,
    Guid MemberId,
    string MemberNickname,
    string ChoreName,
    DateTime CompletedAt);

public record GetVictoriesResponse(List<VictoryDto> Victories);

internal class Handler(IEventStore store)
{
    public async Task<GetVictoriesResponse> HandleAsync(GetVictoriesQuery request)
    {
        var householdEvents = await store.FetchEventsAsync(HouseholdAggregate.StreamId(request.HouseholdId));
        var choreEvents     = await store.FetchEventsAsync(ChoreAggregate.StreamId(request.HouseholdId));

        var nicknames = new Dictionary<Guid, string>();
        foreach (var e in householdEvents)
        {
            switch (e)
            {
                case MemberJoinedHousehold j: nicknames[j.MemberId] = j.Nickname; break;
                case MemberNicknameChanged n: nicknames[n.MemberId] = n.NewNickname; break;
            }
        }

        var choreNames = choreEvents.OfType<ChoreCreated>()
            .ToDictionary(c => c.ChoreId, c => c.DisplayName);

        var victories = choreEvents.OfType<ChoreCompleted>()
            .OrderByDescending(c => c.CompletedAt)
            .Take(request.Limit)
            .Select(c => new VictoryDto(
                c.ChoreId,
                c.CompletedByMemberId,
                nicknames.GetValueOrDefault(c.CompletedByMemberId, "Unknown"),
                choreNames.GetValueOrDefault(c.ChoreId, "Unknown chore"),
                c.CompletedAt))
            .ToList();

        return new GetVictoriesResponse(victories);
    }
}

internal static class VictoriesEndpoint
{
    public static void Map(this RouteGroupBuilder group)
    {
        group.MapGet("households/{householdId:guid}/family-quest/victories",
            async (Guid householdId, Handler handler, int limit = 10) =>
            {
                var result = await handler.HandleAsync(new GetVictoriesQuery(householdId, limit));
                return Results.Ok(result);
            });
    }
}
