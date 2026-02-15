import { useState, useEffect } from 'react';
import { Sparkles, Bug, Wrench, Zap, Server, Monitor } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';

interface ChangelogEntry {
  hash: string;
  shortHash: string;
  message: string;
  date: string;
  author: string;
  type: 'feature' | 'fix' | 'refactor' | 'update';
  displayMessage: string;
}

interface Changelog {
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

interface WhatsNewDialogProps {
  variant?: 'icon' | 'button' | 'controlled';
  open?: boolean;
  onOpenChange?: (open: boolean) => void;
}

interface ApiVersion {
  version: string;
  buildTime: string;
  gitSha: string;
}

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export function WhatsNewDialog({ variant = 'icon', open: controlledOpen, onOpenChange }: WhatsNewDialogProps) {
  const [changelog, setChangelog] = useState<Changelog | null>(null);
  const [apiVersion, setApiVersion] = useState<ApiVersion | null>(null);
  const [internalOpen, setInternalOpen] = useState(false);
  
  const isControlled = variant === 'controlled';
  const open = isControlled ? controlledOpen ?? false : internalOpen;
  const setOpen = isControlled ? (onOpenChange ?? (() => {})) : setInternalOpen;

  // Frontend version from build-time injection
  const frontendVersion = {
    gitSha: typeof __GIT_SHA__ !== 'undefined' ? __GIT_SHA__ : 'local',
    buildTime: typeof __BUILD_TIME__ !== 'undefined' ? __BUILD_TIME__ : 'unknown',
  };

  useEffect(() => {
    if (open && !changelog) {
      fetch('/changelog.json')
        .then(res => res.json())
        .then(data => setChangelog(data))
        .catch(err => console.error('Failed to load changelog', err));
    }
    if (open && !apiVersion) {
      fetch(`${API_BASE_URL}/api/version`)
        .then(res => res.json())
        .then(data => setApiVersion(data))
        .catch(err => console.error('Failed to load API version', err));
    }
  }, [open, changelog, apiVersion]);

  // Group commits by date
  const groupedByDate = changelog?.commits.reduce((acc, commit) => {
    const date = commit.date;
    if (!acc[date]) acc[date] = [];
    acc[date].push(commit);
    return acc;
  }, {} as Record<string, ChangelogEntry[]>) ?? {};

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      {variant !== 'controlled' && (
        <DialogTrigger asChild>
          {variant === 'icon' ? (
            <Button variant="ghost" size="icon" className="text-muted-foreground hover:text-foreground">
              <Sparkles className="w-5 h-5" />
            </Button>
          ) : (
            <Button variant="ghost" size="sm" className="w-full justify-start gap-2">
              <Sparkles className="w-4 h-4" />
              What's New
            </Button>
          )}
        </DialogTrigger>
      )}
      <DialogContent className="sm:max-w-md max-h-[80vh]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Sparkles className="w-5 h-5 text-primary" />
            What's New
          </DialogTitle>
          {/* Version info */}
          <div className="flex flex-wrap gap-3 pt-2 text-xs text-muted-foreground">
            <div className="flex items-center gap-1.5" title={`Built: ${frontendVersion.buildTime}`}>
              <Monitor className="w-3.5 h-3.5" />
              <span>Web:</span>
              <code className="bg-muted px-1.5 py-0.5 rounded font-mono">
                {frontendVersion.gitSha.slice(0, 7)}
              </code>
            </div>
            <div className="flex items-center gap-1.5" title={apiVersion?.buildTime ? `Built: ${apiVersion.buildTime}` : ''}>
              <Server className="w-3.5 h-3.5" />
              <span>API:</span>
              <code className="bg-muted px-1.5 py-0.5 rounded font-mono">
                {apiVersion?.gitSha?.slice(0, 7) || '...'}
              </code>
            </div>
          </div>
        </DialogHeader>
        <ScrollArea className="h-[60vh] pr-4">
          {!changelog ? (
            <p className="text-sm text-muted-foreground text-center py-8">
              Loading...
            </p>
          ) : (
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
                            <p className="text-sm leading-snug">
                              {commit.displayMessage}
                            </p>
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          )}
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}
