import type { DisplayLately } from '../types';

interface DisplayLatelyRowsProps {
  lately: DisplayLately;
}

// Four shades: nothing, one, two, three or more.
const SHADES = ['bg-muted', 'bg-green-200', 'bg-green-400', 'bg-green-600'];

/** The last 40 days, one row per person: a square a day, shaded by how many chores they did, and a total. */
export function DisplayLatelyRows({ lately }: DisplayLatelyRowsProps) {
  if (lately.people.length === 0) {
    return <p className="text-sm text-muted-foreground">Nobody has joined yet.</p>;
  }

  return (
    <div className="space-y-2">
      {lately.people.map((person) => (
        <div key={person.name} className="flex items-center gap-3" data-testid="lately-row">
          <span className="w-20 shrink-0 truncate text-sm">{person.name}</span>
          <div className="flex flex-wrap gap-0.5 flex-1">
            {person.days.map((d) => (
              <span
                key={d.day}
                title={`${person.name}, ${d.day}: ${d.count}`}
                className={`h-3.5 w-2 rounded-sm ${SHADES[Math.min(d.count, 3)]}`}
                data-count={d.count}
              />
            ))}
          </div>
          <span className="w-8 text-right text-sm tabular-nums">{person.total}</span>
        </div>
      ))}
    </div>
  );
}
