import { useEffect, useState } from 'react';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import { Badge } from '@/components/ui/badge';
import { CheckCircle2, AlertTriangle } from 'lucide-react';
import { useHouseholdStore } from '@/stores/householdStore';
import type { MemberOverdue } from '@/types/household';
import { MemberAvatar } from './MemberAvatar';

interface OverdueAccordionProps {
  householdId: string;
}

export function OverdueAccordion({ householdId }: OverdueAccordionProps) {
  const [memberOverdue, setMemberOverdue] = useState<MemberOverdue[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { fetchOverdueChores, members, isAdmin } = useHouseholdStore();
  
  // Only admins can see overdue chores
  if (!isAdmin) {
    return null;
  }

  useEffect(() => {
    const loadOverdue = async () => {
      setIsLoading(true);
      const data = await fetchOverdueChores(householdId);
      setMemberOverdue(data);
      setIsLoading(false);
    };
    loadOverdue();
  }, [householdId, fetchOverdueChores]);

  if (isLoading) {
    return (
      <div className="rounded-lg border bg-card p-4">
        <p className="text-muted-foreground text-sm">Loading overdue chores...</p>
      </div>
    );
  }

  const hasAnyOverdue = memberOverdue.some((m) => m.overdueCount > 0);

  if (!hasAnyOverdue) {
    return (
      <div className="rounded-lg border bg-card p-4 flex items-center gap-2">
        <CheckCircle2 className="h-5 w-5 text-green-500" />
        <p className="text-sm font-medium text-green-700">Everyone is caught up! 🎉</p>
      </div>
    );
  }

  const getMemberColor = (memberId: string) => {
    const member = members.find((m) => m.id === memberId);
    return member?.avatarColor || 'hsl(150 50% 50%)';
  };

  return (
    <div className="rounded-lg border bg-card">
      <div className="p-4 border-b">
        <h3 className="font-semibold flex items-center gap-2">
          <AlertTriangle className="h-5 w-5 text-amber-500" />
          Overdue Chores
        </h3>
      </div>
      <Accordion type="multiple" className="w-full">
        {memberOverdue.map((member) => (
          <AccordionItem key={member.memberId} value={member.memberId}>
            <AccordionTrigger className="px-4 hover:no-underline">
              <div className="flex items-center gap-3 w-full">
                <MemberAvatar
                  nickname={member.nickname}
                  color={getMemberColor(member.memberId)}
                  size="sm"
                />
                <span className="font-medium">{member.nickname}</span>
                {member.overdueCount > 0 ? (
                  <Badge variant="destructive" className="ml-auto mr-2">
                    {member.overdueCount} overdue
                  </Badge>
                ) : (
                  <Badge variant="secondary" className="ml-auto mr-2 bg-green-100 text-green-700">
                    ✓ Caught up
                  </Badge>
                )}
              </div>
            </AccordionTrigger>
            <AccordionContent className="px-4 pb-4">
              {member.chores.length === 0 ? (
                <p className="text-sm text-muted-foreground py-2">
                  All chores completed! Great job! 🌟
                </p>
              ) : (
                <ul className="space-y-2">
                  {member.chores.map((chore) => (
                    <li
                      key={chore.choreId}
                      className="flex items-center justify-between py-2 px-3 bg-red-50 rounded-md border border-red-100"
                    >
                      <div className="flex items-center gap-2">
                        <AlertTriangle className="h-4 w-4 text-red-500" />
                        <span className="font-medium">{chore.displayName}</span>
                      </div>
                      <span className="text-sm text-red-600">
                        from {chore.overduePeriod}
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
