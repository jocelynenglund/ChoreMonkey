import { useEffect, useState } from 'react';
import { useHouseholdStore } from '@/stores/householdStore';
import type { MyChoresResponse } from '@/types/household';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { CheckCircle2, AlertTriangle, Clock, Sparkles } from 'lucide-react';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';

interface MyChoreSectionProps {
  householdId: string;
  memberId: string;
  onCompleteChore?: (choreId: string) => void;
}

export function MyChoresSection({ householdId, memberId, onCompleteChore }: MyChoreSectionProps) {
  const [myChores, setMyChores] = useState<MyChoresResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { fetchMyChores } = useHouseholdStore();

  const loadMyChores = async () => {
    setIsLoading(true);
    const data = await fetchMyChores(householdId, memberId);
    setMyChores(data);
    setIsLoading(false);
  };

  useEffect(() => {
    loadMyChores();
  }, [householdId, memberId]);

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-24 bg-muted animate-pulse rounded-lg" />
        <div className="h-24 bg-muted animate-pulse rounded-lg" />
      </div>
    );
  }

  if (!myChores) {
    return (
      <div className="text-center text-muted-foreground py-8">
        Unable to load your chores
      </div>
    );
  }

  const hasOverdue = myChores.overdue.length > 0;
  const hasPending = myChores.pending.length > 0;
  const hasCompleted = myChores.completed.length > 0;
  const allDone = !hasOverdue && !hasPending;

  return (
    <div className="space-y-4">
      {/* All Done Message */}
      {allDone && (
        <Card className="border-green-200 bg-green-50">
          <CardContent className="flex items-center gap-3 py-4">
            <Sparkles className="h-6 w-6 text-green-600" />
            <div>
              <p className="font-medium text-green-800">All caught up! 🎉</p>
              <p className="text-sm text-green-600">No pending or overdue chores</p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Overdue Section */}
      {hasOverdue && (
        <Card className="border-red-200 bg-red-50">
          <CardContent className="py-4">
            <div className="flex items-center gap-2 mb-3">
              <AlertTriangle className="h-5 w-5 text-red-600" />
              <h3 className="font-semibold text-red-800">Overdue</h3>
              <Badge variant="destructive" className="ml-auto">
                {myChores.overdue.length}
              </Badge>
            </div>
            <ul className="space-y-2">
              {myChores.overdue.map((chore) => (
                <li
                  key={chore.choreId}
                  className="flex items-center justify-between p-3 bg-white rounded-md border border-red-200 cursor-pointer hover:bg-red-100 transition-colors"
                  onClick={() => onCompleteChore?.(chore.choreId)}
                >
                  <div>
                    <span className="font-medium">{chore.displayName}</span>
                    {chore.frequencyType && (
                      <span className="text-xs text-muted-foreground ml-2">
                        ({chore.frequencyType})
                      </span>
                    )}
                  </div>
                  <span className="text-sm text-red-600 font-medium">
                    from {chore.overduePeriod}
                  </span>
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      {/* Pending Section */}
      {hasPending && (
        <Card>
          <CardContent className="py-4">
            <div className="flex items-center gap-2 mb-3">
              <Clock className="h-5 w-5 text-amber-600" />
              <h3 className="font-semibold">To Do</h3>
              <Badge variant="secondary" className="ml-auto">
                {myChores.pending.length}
              </Badge>
            </div>
            <ul className="space-y-2">
              {myChores.pending.map((chore) => (
                <li
                  key={chore.choreId}
                  className="flex items-center justify-between p-3 bg-muted/50 rounded-md cursor-pointer hover:bg-muted transition-colors"
                  onClick={() => onCompleteChore?.(chore.choreId)}
                >
                  <div>
                    <span className="font-medium">{chore.displayName}</span>
                    {chore.frequencyType && (
                      <span className="text-xs text-muted-foreground ml-2">
                        ({chore.frequencyType})
                      </span>
                    )}
                  </div>
                  {chore.dueDescription && (
                    <span className="text-sm text-muted-foreground">
                      {chore.dueDescription}
                    </span>
                  )}
                </li>
              ))}
            </ul>
          </CardContent>
        </Card>
      )}

      {/* Completed Section */}
      {hasCompleted && (
        <Accordion type="single" collapsible className="w-full">
          <AccordionItem value="completed" className="border rounded-lg">
            <AccordionTrigger className="px-4 hover:no-underline">
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-5 w-5 text-green-600" />
                <span className="font-semibold">Completed</span>
                <Badge variant="secondary" className="ml-2 bg-green-100 text-green-700">
                  {myChores.completed.length}
                </Badge>
              </div>
            </AccordionTrigger>
            <AccordionContent className="px-4 pb-4">
              <ul className="space-y-2">
                {myChores.completed.map((chore) => (
                  <li
                    key={chore.choreId}
                    className="flex items-center justify-between p-2 text-sm"
                  >
                    <span className="text-muted-foreground line-through">
                      {chore.displayName}
                    </span>
                    <span className="text-xs text-muted-foreground">
                      {chore.completedAt.toLocaleTimeString([], { 
                        hour: '2-digit', 
                        minute: '2-digit' 
                      })}
                    </span>
                  </li>
                ))}
              </ul>
            </AccordionContent>
          </AccordionItem>
        </Accordion>
      )}
    </div>
  );
}
