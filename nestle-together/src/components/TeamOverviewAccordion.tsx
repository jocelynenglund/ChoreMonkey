import { useEffect, useState } from 'react';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import { Badge } from '@/components/ui/badge';
import { CheckCircle2, AlertTriangle, Clock, Users } from 'lucide-react';
import { useAppStore } from '@/features/store';
import type { MemberOverview } from '@/features/team-overview/types';
import { MemberAvatar } from './MemberAvatar';

interface TeamOverviewAccordionProps {
  householdId: string;
}

export function TeamOverviewAccordion({ householdId }: TeamOverviewAccordionProps) {
  const [teamData, setTeamData] = useState<MemberOverview[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { fetchTeamOverview, members, isAdmin } = useAppStore();
  
  // Only admins can see team overview
  if (!isAdmin) {
    return null;
  }

  useEffect(() => {
    const loadTeam = async () => {
      setIsLoading(true);
      const data = await fetchTeamOverview(householdId);
      setTeamData(data);
      setIsLoading(false);
    };
    loadTeam();
  }, [householdId, fetchTeamOverview]);

  if (isLoading) {
    return (
      <div className="rounded-lg border bg-card p-4">
        <p className="text-muted-foreground text-sm">Loading team overview...</p>
      </div>
    );
  }

  const getMemberColor = (memberId: string) => {
    const member = (members || []).find((m) => m.id === memberId);
    return member?.avatarColor || 'hsl(150 50% 50%)';
  };

  const totalOverdue = teamData.reduce((sum, m) => sum + m.overdueCount, 0);

  return (
    <div className="rounded-lg border bg-card">
      <div className="p-4 border-b">
        <h3 className="font-semibold flex items-center gap-2">
          <Users className="h-5 w-5 text-blue-500" />
          Team Overview
          {totalOverdue > 0 && (
            <Badge variant="destructive" className="ml-2">
              {totalOverdue} overdue
            </Badge>
          )}
        </h3>
      </div>
      <Accordion type="multiple" className="w-full">
        {teamData.map((member) => (
          <AccordionItem key={member.memberId} value={member.memberId}>
            <AccordionTrigger className="px-4 hover:no-underline">
              <div className="flex items-center gap-3 w-full">
                <MemberAvatar
                  nickname={member.nickname}
                  color={getMemberColor(member.memberId)}
                  size="sm"
                />
                <span className="font-medium">{member.nickname}</span>
                <div className="ml-auto mr-2 flex gap-2">
                  {member.overdueCount > 0 && (
                    <Badge variant="destructive">
                      {member.overdueCount} overdue
                    </Badge>
                  )}
                  <Badge variant="secondary" className="bg-green-100 text-green-700">
                    {member.completedCount}/{member.totalChores} done
                  </Badge>
                </div>
              </div>
            </AccordionTrigger>
            <AccordionContent className="px-4 pb-4">
              {member.chores.length === 0 ? (
                <p className="text-sm text-muted-foreground py-2">
                  No chores assigned yet
                </p>
              ) : (
                <ul className="space-y-2">
                  {member.chores.map((chore) => (
                    <li
                      key={chore.choreId}
                      className={`flex items-center justify-between py-2 px-3 rounded-md border ${
                        chore.status === 'overdue'
                          ? 'bg-red-50 border-red-100'
                          : chore.status === 'completed'
                          ? 'bg-green-50 border-green-100'
                          : 'bg-amber-50 border-amber-100'
                      }`}
                    >
                      <div className="flex items-center gap-2">
                        {chore.status === 'overdue' && (
                          <AlertTriangle className="h-4 w-4 text-red-500" />
                        )}
                        {chore.status === 'completed' && (
                          <CheckCircle2 className="h-4 w-4 text-green-500" />
                        )}
                        {chore.status === 'pending' && (
                          <Clock className="h-4 w-4 text-amber-500" />
                        )}
                        <span className="font-medium">{chore.displayName}</span>
                        {chore.isOptional && (
                          <Badge variant="outline" className="text-xs">bonus</Badge>
                        )}
                      </div>
                      <span className={`text-sm ${
                        chore.status === 'overdue'
                          ? 'text-red-600'
                          : chore.status === 'completed'
                          ? 'text-green-600'
                          : 'text-amber-600'
                      }`}>
                        {chore.status === 'overdue' && chore.overduePeriod
                          ? `from ${chore.overduePeriod}`
                          : chore.status === 'completed'
                          ? '✓ done'
                          : 'pending'}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </AccordionContent>
          </AccordionItem>
        ))}
      </Accordion>
    </div>
  );
}
