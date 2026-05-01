import { useEffect, useState } from 'react';
import { Sparkles, Bug, Wrench, Zap } from 'lucide-react';

export interface ChangelogEntry {
  hash: string;
  shortHash: string;
  message: string;
  date: string;
  author: string;
  type: 'feature' | 'fix' | 'refactor' | 'update';
  displayMessage: string;
}

export interface Changelog {
  generated: string;
  commits: ChangelogEntry[];
}

const typeConfig = {
  feature: { icon: Sparkles, color: 'text-green-500', bg: 'bg-green-100', label: 'New' },
  fix: { icon: Bug, color: 'text-amber-500', bg: 'bg-amber-100', label: 'Fix' },
  refactor: { icon: Wrench, color: 'text-blue-500', bg: 'bg-blue-100', label: 'Improved' },
  update: { icon: Zap, color: 'text-purple-500', bg: 'bg-purple-100', label: 'Update' },
};

function formatDate(dateStr: string): string {
  const date = new Date(dateStr);
  return date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

interface ChangelogListProps {
  /** When true, the component fetches /changelog.json itself. */
  autoLoad?: boolean;
  /** Inject pre-loaded changelog data. Wins over autoLoad. */
  changelog?: Changelog | null;
}

/**
 * Renders the changelog as a date-grouped list. Used inside WhatsNewDialog
 * (modal) and on the /changelog page.
 */
export function ChangelogList({ autoLoad = true, changelog: injected }: ChangelogListProps) {
  const [fetched, setFetched] = useState<Changelog | null>(null);

  useEffect(() => {
    if (!autoLoad || injected) return;
    fetch('/changelog.json')
      .then((res) => res.json())
      .then((data: Changelog) => setFetched(data))
      .catch((err) => console.error('Failed to load changelog', err));
  }, [autoLoad, injected]);

  const data = injected ?? fetched;

  if (!data) {
    return (
      <p className="text-sm text-muted-foreground text-center py-8">Loading...</p>
    );
  }

  const groupedByDate = data.commits.reduce((acc, commit) => {
    const date = commit.date;
    if (!acc[date]) acc[date] = [];
    acc[date].push(commit);
    return acc;
  }, {} as Record<string, ChangelogEntry[]>);

  return (
    <div className="space-y-6">
      {Object.entries(groupedByDate).map(([date, commits]) => (
        <div key={date}>
          <h3 className="text-xs font-semibold text-muted-foreground mb-2 sticky top-0 bg-background py-1">
            {formatDate(date)}
          </h3>
          <div className="space-y-2">
            {commits.map((commit) => {
              const config = typeConfig[commit.type];
              const Icon = config.icon;
              return (
                <div
                  key={commit.hash}
                  className="flex items-start gap-3 p-2 rounded-lg hover:bg-muted/50 transition-colors"
                >
                  <div className={`p-1.5 rounded-md ${config.bg}`}>
                    <Icon className={`w-3.5 h-3.5 ${config.color}`} />
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm leading-snug">{commit.displayMessage}</p>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
}
