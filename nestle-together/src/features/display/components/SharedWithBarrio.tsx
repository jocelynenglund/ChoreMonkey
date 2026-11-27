import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { fetchDisplayLately, fetchDisplayLinks, fetchDisplayToday, makeDisplayLink, revokeDisplayLink } from '../api';
import { DisplayLatelyRows } from './DisplayLatelyRows';
import { DisplayLinkControls } from './DisplayLinkControls';
import { DisplayTodayList } from './DisplayTodayList';

interface SharedWithBarrioProps {
  householdId: string;
  /** Changes when the household changes (realtime), so the lists load again. */
  refreshKey?: number;
  isAdmin: boolean;
  adminPin: string | null;
}

/** "Shared with barrio": what a connected screen sees for this household, and (for admins) its link. */
export function SharedWithBarrio({ householdId, refreshKey = 0, isAdmin, adminPin }: SharedWithBarrioProps) {
  const queryClient = useQueryClient();
  const linksKey = ['display-links', householdId];

  const today = useQuery({ queryKey: ['display-today', householdId, refreshKey], queryFn: () => fetchDisplayToday(householdId) });
  const lately = useQuery({ queryKey: ['display-lately', householdId, refreshKey], queryFn: () => fetchDisplayLately(householdId) });
  const canManage = isAdmin && !!adminPin;
  const links = useQuery({ queryKey: linksKey, queryFn: () => fetchDisplayLinks(householdId, adminPin!), enabled: canManage });

  const make = useMutation({
    mutationFn: () => makeDisplayLink(householdId, adminPin!),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: linksKey }),
  });
  const revoke = useMutation({
    mutationFn: (linkId: string) => revokeDisplayLink(householdId, adminPin!, linkId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: linksKey }),
  });

  const error = (make.error ?? revoke.error ?? links.error) as Error | null;

  return (
    <Card data-testid="shared-with-barrio">
      <CardHeader className="pb-2">
        <CardTitle className="text-lg">Shared with barrio</CardTitle>
        <p className="text-sm text-muted-foreground">What a connected screen sees. Never any pay or money.</p>
      </CardHeader>
      <CardContent className="space-y-5">
        {canManage && (
          <DisplayLinkControls
            links={links.data ?? []}
            busy={make.isPending || revoke.isPending || links.isLoading}
            error={error?.message ?? null}
            onMake={() => make.mutate()}
            onRevoke={(linkId) => revoke.mutate(linkId)}
          />
        )}

        <section>
          <h4 className="text-sm font-medium mb-2">Today</h4>
          {today.data ? (
            <DisplayTodayList today={today.data} />
          ) : (
            <p className="text-sm text-muted-foreground">{today.isError ? "Couldn't load today's chores." : 'Loading...'}</p>
          )}
        </section>

        <section>
          <h4 className="text-sm font-medium mb-2">The last 40 days</h4>
          {lately.data ? (
            <DisplayLatelyRows lately={lately.data} />
          ) : (
            <p className="text-sm text-muted-foreground">{lately.isError ? "Couldn't load the last 40 days." : 'Loading...'}</p>
          )}
        </section>
      </CardContent>
    </Card>
  );
}
