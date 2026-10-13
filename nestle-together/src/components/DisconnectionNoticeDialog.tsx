import { useEffect, useRef, useState } from 'react';
import { WifiOff } from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { fetchDisconnectionNotice, type DisconnectionNotice } from '@/features/chores';

interface DisconnectionNoticeDialogProps {
  householdId: string;
  memberId: string | null;
  refreshKey: number;
  onCompleteChore: (choreId: string) => void;
}

const formatDate = (date: Date) =>
  date.toLocaleDateString(undefined, { day: 'numeric', month: 'long', year: 'numeric' });

const formatMonth = (date: Date) => date.toLocaleDateString(undefined, { month: 'long' });

/**
 * Shown on load when the member hasn't done any of their obligatory chores in
 * the past two months: their internet is "disconnected" until they do one.
 * Re-checks on refresh so finishing a chore announces the restoration.
 */
export function DisconnectionNoticeDialog({
  householdId,
  memberId,
  refreshKey,
  onCompleteChore,
}: DisconnectionNoticeDialogProps) {
  const [notice, setNotice] = useState<DisconnectionNotice | null>(null);
  const [open, setOpen] = useState(false);
  const checkedOnLoad = useRef(false);
  const wasDisconnected = useRef(false);

  useEffect(() => {
    if (!memberId) return;
    let cancelled = false;

    fetchDisconnectionNotice(householdId, memberId).then((result) => {
      if (cancelled || !result) return;

      if (wasDisconnected.current && !result.isDisconnected) {
        toast.success('Internet restored 📶', {
          description: 'Thanks for pitching in. Keep those chores coming!',
        });
        setOpen(false);
      }
      wasDisconnected.current = result.isDisconnected;
      setNotice(result);

      // Only pop the notice automatically on app load, not on every refresh.
      if (!checkedOnLoad.current) {
        checkedOnLoad.current = true;
        if (result.isDisconnected) setOpen(true);
      }
    });

    return () => {
      cancelled = true;
    };
  }, [householdId, memberId, refreshKey]);

  if (!notice?.isDisconnected) return null;

  const handleDoChore = (choreId: string) => {
    setOpen(false);
    onCompleteChore(choreId);
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <div className="w-12 h-12 rounded-full bg-destructive/10 flex items-center justify-center mx-auto mb-2">
            <WifiOff className="w-6 h-6 text-destructive" />
          </div>
          <p className="text-center text-xs font-semibold uppercase tracking-widest text-destructive">
            Final notice
          </p>
          <DialogTitle className="text-center text-xl">Notice of Disconnection</DialogTitle>
          <DialogDescription className="text-center">
            Your internet service has been{' '}
            <span className="font-medium text-foreground">disconnected in the month of {formatMonth(new Date())}</span>.
            Our records show that none of your obligatory chores have been done since{' '}
            {formatDate(notice.windowStart)}.
          </DialogDescription>
        </DialogHeader>

        <div className="rounded-lg border border-border bg-muted/40 p-3">
          <p className="text-xs font-medium text-muted-foreground mb-2">
            Complete an obligatory chore to restore service:
          </p>
          <ul className="space-y-2">
            {notice.missedChores.map((chore) => (
              <li key={chore.choreId} className="flex items-center justify-between gap-3">
                <div className="min-w-0">
                  <p className="font-medium truncate">{chore.displayName}</p>
                  <p className="text-xs text-muted-foreground">
                    {chore.lastCompleted
                      ? `Last done ${formatDate(chore.lastCompleted)}`
                      : 'Never done'}
                  </p>
                </div>
                <Button size="sm" onClick={() => handleDoChore(chore.choreId)}>
                  Do it now
                </Button>
              </li>
            ))}
          </ul>
        </div>

        <DialogFooter>
          <Button variant="ghost" className="w-full" onClick={() => setOpen(false)}>
            I'll do it later
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
