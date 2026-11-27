import { useState } from 'react';
import { Check, Copy, ExternalLink, Link as LinkIcon, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import type { DisplayLink } from '../types';

interface DisplayLinkControlsProps {
  links: DisplayLink[];
  busy?: boolean;
  error?: string | null;
  onMake: () => void;
  onRevoke: (linkId: string) => void;
}

/** The admin's side: make a link when there is none, then copy it again any time, open its page, or revoke it. */
export function DisplayLinkControls({ links, busy, error, onMake, onRevoke }: DisplayLinkControlsProps) {
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const copy = async (link: DisplayLink) => {
    if (!link.url) return;
    await navigator.clipboard.writeText(link.url);
    setCopiedId(link.linkId);
    setTimeout(() => setCopiedId((id) => (id === link.linkId ? null : id)), 2000);
  };

  return (
    <div className="space-y-2">
      {links.length === 0 && (
        <Button size="sm" variant="outline" disabled={busy} onClick={onMake}>
          <LinkIcon className="h-4 w-4 mr-2" />
          Make a link
        </Button>
      )}

      {links.map((link) => (
        <div key={link.linkId} className="flex flex-wrap items-center gap-2" data-testid="display-link">
          <span className="text-sm text-muted-foreground mr-auto">{link.label}</span>
          <Button size="sm" variant="outline" disabled={!link.url} onClick={() => copy(link)}>
            {copiedId === link.linkId ? <Check className="h-4 w-4 mr-2" /> : <Copy className="h-4 w-4 mr-2" />}
            {copiedId === link.linkId ? 'Copied' : 'Copy link'}
          </Button>
          {link.url && (
            <Button size="sm" variant="outline" asChild>
              <a href={link.url} target="_blank" rel="noopener noreferrer">
                <ExternalLink className="h-4 w-4 mr-2" />
                Open page
              </a>
            </Button>
          )}
          <Button size="sm" variant="ghost" disabled={busy} onClick={() => onRevoke(link.linkId)}>
            <Trash2 className="h-4 w-4 mr-2" />
            Revoke
          </Button>
        </div>
      ))}

      {error && <p className="text-sm text-destructive" role="alert">{error}</p>}
    </div>
  );
}
