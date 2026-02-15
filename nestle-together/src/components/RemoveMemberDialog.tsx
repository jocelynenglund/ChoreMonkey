import { useState } from 'react';
import { UserMinus, AlertTriangle } from 'lucide-react';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/alert-dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import type { Member } from '@/types/household';

interface RemoveMemberDialogProps {
  member: Member;
  onRemove: (pinCode: string) => Promise<boolean>;
}

export function RemoveMemberDialog({ member, onRemove }: RemoveMemberDialogProps) {
  const [isRemoving, setIsRemoving] = useState(false);
  const [open, setOpen] = useState(false);
  const [pinCode, setPinCode] = useState('');
  const [error, setError] = useState<string | null>(null);

  const handleRemove = async () => {
    if (!pinCode.trim()) {
      setError('Please enter your admin PIN');
      return;
    }

    setIsRemoving(true);
    setError(null);
    
    try {
      const success = await onRemove(pinCode.trim());
      if (success) {
        setOpen(false);
        setPinCode('');
      } else {
        setError('Invalid admin PIN. Please try again.');
      }
    } catch {
      setError('Failed to remove member. Please try again.');
    } finally {
      setIsRemoving(false);
    }
  };

  const handleOpenChange = (isOpen: boolean) => {
    setOpen(isOpen);
    if (!isOpen) {
      setPinCode('');
      setError(null);
    }
  };

  return (
    <AlertDialog open={open} onOpenChange={handleOpenChange}>
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
        
        <div className="py-4">
          <Label htmlFor="admin-pin" className="text-sm font-medium">
            Enter Admin PIN to confirm
          </Label>
          <Input
            id="admin-pin"
            type="password"
            inputMode="numeric"
            pattern="[0-9]*"
            placeholder="Admin PIN"
            value={pinCode}
            onChange={(e) => {
              setPinCode(e.target.value);
              setError(null);
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                handleRemove();
              }
            }}
            className="mt-2"
            autoFocus
          />
          {error && (
            <p className="text-sm text-destructive mt-2">{error}</p>
          )}
        </div>

        <AlertDialogFooter>
          <AlertDialogCancel disabled={isRemoving}>Cancel</AlertDialogCancel>
          <Button
            onClick={handleRemove}
            disabled={isRemoving || !pinCode.trim()}
            variant="destructive"
          >
            {isRemoving ? 'Removing...' : 'Remove Member'}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
