import { useState, useEffect } from 'react';
import { Sparkles, Server, Monitor } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { ScrollArea } from '@/components/ui/scroll-area';
import { ChangelogList, type Changelog } from '@/components/ChangelogList';

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
    version: typeof __BUILD_VERSION__ !== 'undefined' ? __BUILD_VERSION__ : 'dev',
    gitSha: typeof __GIT_SHA__ !== 'undefined' ? __GIT_SHA__ : 'local',
    buildTime: typeof __BUILD_TIME__ !== 'undefined' ? __BUILD_TIME__ : 'unknown',
  };

  useEffect(() => {
    if (open && !changelog) {
      fetch('/changelog.json')
        .then(res => res.json())
        .then((data: Changelog) => setChangelog(data))
        .catch(err => console.error('Failed to load changelog', err));
    }
    if (open && !apiVersion) {
      fetch(`${API_BASE_URL}/api/version`)
        .then(res => res.json())
        .then((data: ApiVersion) => setApiVersion(data))
        .catch(err => console.error('Failed to load API version', err));
    }
  }, [open, changelog, apiVersion]);

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
            <div className="flex items-center gap-1.5" title={`SHA: ${frontendVersion.gitSha}\nBuilt: ${frontendVersion.buildTime}`}>
              <Monitor className="w-3.5 h-3.5" />
              <span>Web:</span>
              <code className="bg-muted px-1.5 py-0.5 rounded font-mono">
                {frontendVersion.version}
              </code>
            </div>
            <div className="flex items-center gap-1.5" title={apiVersion ? `SHA: ${apiVersion.gitSha}\nBuilt: ${apiVersion.buildTime}` : ''}>
              <Server className="w-3.5 h-3.5" />
              <span>API:</span>
              <code className="bg-muted px-1.5 py-0.5 rounded font-mono">
                {apiVersion?.version || '...'}
              </code>
            </div>
          </div>
        </DialogHeader>
        <ScrollArea className="h-[60vh] pr-4">
          <ChangelogList autoLoad={false} changelog={changelog} limit={20} />
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}
