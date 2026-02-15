import { useState } from 'react';
import { UserMinus, AlertTriangle } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import type { Member } from '@/types/household';

interface RemoveMemberDialogProps {
  member: Member;
  onRemove: () => Promise<void>;
}

export function RemoveMemberDialog({ member, onRemove }: RemoveMemberDialogProps) {
  const [isRemoving, setIsRemoving] = useState(false);
  const [open, setOpen] = useState(false);

  const handleRemove = async () => {
    setIsRemoving(true);
    try {
      await onRemove();
      setOpen(false);
    } finally {
      setIsRemoving(false);
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="h-6 w-6 text-muted-foreground hover:text-destructive hover:bg-destructive/10"
          title="Remove member"
        >
          <UserMinus className="h-3.5 w-3.5" />
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <AlertTriangle className="h-5 w-5 text-destructive" />
            Remove {member.nickname}?
          </AlertDialogTitle>
          <AlertDialogDescription>
            This will remove <strong>{member.nickname}</strong> from the household. 
            They will no longer be able to access chores or see household activity.
            <br /><br />
            This action cannot be undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isRemoving}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleRemove}
            disabled={isRemoving}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          >
            {isRemoving ? 'Removing...' : 'Remove Member'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
