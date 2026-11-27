import { CheckCircle2, Circle } from 'lucide-react';
import type { DisplayToday } from '../types';

interface DisplayTodayListProps {
  today: DisplayToday;
}

/** Today's chores as a connected screen shows them: open ones to do, done ones with who did them. */
export function DisplayTodayList({ today }: DisplayTodayListProps) {
  if (today.chores.length === 0) {
    return <p className="text-sm text-muted-foreground">Nothing on the list today.</p>;
  }

  return (
    <ul className="space-y-1" aria-label="Today">
      {today.chores.map((chore) => (
        <li key={chore.choreId} className="flex items-center gap-2 text-sm">
          {chore.state === 'done' ? (
            <CheckCircle2 className="h-4 w-4 text-green-500 shrink-0" aria-hidden />
          ) : (
            <Circle className="h-4 w-4 text-muted-foreground shrink-0" aria-hidden />
          )}
          <span className={chore.state === 'done' ? 'text-muted-foreground line-through' : ''}>{chore.title}</span>
          {chore.state === 'done' && chore.by && <span className="text-xs text-muted-foreground">by {chore.by}</span>}
        </li>
      ))}
    </ul>
  );
}
