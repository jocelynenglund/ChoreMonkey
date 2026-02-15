using ChoreMonkey.Core.Domain;
using ChoreMonkey.Events;
using FileEventStore;
using MediatR;

namespace ChoreMonkey.Core.Feature.MemberLookup;

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
        var events = await store.FetchEventsAsync(
            HouseholdAggregate.StreamId(request.HouseholdId));

        var members = new Dictionary<Guid, MemberDto>();

        foreach (var e in events)
        {
            switch (e)
            {
                case MemberJoinedHousehold joined:
                    members[joined.MemberId] = new(joined.MemberId, joined.Nickname, null);
                    break;
                case MemberNicknameChanged changed:
                    if (members.TryGetValue(changed.MemberId, out var m1))
                        members[changed.MemberId] = m1 with { Nickname = changed.NewNickname };
                    break;
                case MemberStatusChanged status:
                    if (members.TryGetValue(status.MemberId, out var m2))
                        members[status.MemberId] = m2 with { Status = status.Status };
                    break;
                case MemberRemoved removed:
                    members.Remove(removed.MemberId);
                    break;
            }
        }

        return new MemberLookupResponse(members);
    }
}
