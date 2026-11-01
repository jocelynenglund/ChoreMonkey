using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using MediatR;

namespace ChoreMonkey.Core.Feature.Members.Queries.MemberLookup;

public record MemberLookupQuery(Guid HouseholdId) : IRequest<MemberLookupResponse>;

public record MemberDto(
    Guid Id,
    string Nickname,
    string? Status);

public record MemberLookupResponse(Dictionary<Guid, MemberDto> Members);

internal class Handler(IEventStore store) : IRequestHandler<MemberLookupQuery, MemberLookupResponse>
{
    public async Task<MemberLookupResponse> Handle(MemberLookupQuery request, CancellationToken ct)
    {
        var history = (await store.FetchEventsAsync(
            HouseholdAggregate.StreamId(request.HouseholdId))).Cast<object>().ToList();

        return MemberLookupView.Project(history);
    }
}
