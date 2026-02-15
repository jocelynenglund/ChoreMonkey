import { useState } from 'react';
import { User, Pencil } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { MemberAvatar } from './MemberAvatar';
import { useHouseholdStore } from '@/stores/householdStore';

interface ProfileDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  householdId: string;
  memberId: string;
  currentNickname: string;
  avatarColor: string;
}

export function ProfileDialog({ 
  open, 
  onOpenChange, 
  householdId, 
  memberId, 
  currentNickname, 
  avatarColor 
}: ProfileDialogProps) {
  const [nickname, setNickname] = useState(currentNickname);
  const [isLoading, setIsLoading] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  
  const { changeNickname } = useHouseholdStore();

  const handleSave = async () => {
    if (!nickname.trim() || nickname.trim() === currentNickname) {
      onOpenChange(false);
      return;
    }

    setIsLoading(true);
    setMessage(null);
    
    const success = await changeNickname(householdId, memberId, nickname.trim());
    
    if (success) {
      setMessage({ type: 'success', text: 'Nickname updated!' });
      setTimeout(() => onOpenChange(false), 1000);
    } else {
      setMessage({ type: 'error', text: 'Failed to update nickname' });
    }
    
    setIsLoading(false);
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-sm max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <User className="w-5 h-5" />
            Edit Profile
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-6 mt-4">
          {/* Avatar Preview */}
          <div className="flex justify-center">
            <MemberAvatar
              nickname={nickname || currentNickname}
              color={avatarColor}
              size="lg"
            />
          </div>

          {/* Nickname */}
          <div className="space-y-2">
            <Label htmlFor="nickname" className="flex items-center gap-2">
              <Pencil className="w-4 h-4" />
              Nickname
            </Label>
            <Input
              id="nickname"
              value={nickname}
              onChange={(e) => setNickname(e.target.value)}
              placeholder="Enter your nickname"
              className="text-center"
              maxLength={20}
            />
          </div>

          {/* Message */}
          {message && (
            <p className={`text-sm text-center ${message.type === 'success' ? 'text-green-600' : 'text-destructive'}`}>
              {message.text}
            </p>
          )}

          {/* Actions */}
          <div className="flex gap-3">
            <Button
              variant="outline"
              className="flex-1"
              onClick={() => onOpenChange(false)}
            >
              Cancel
            </Button>
            <Button
              className="flex-1"
              onClick={handleSave}
              disabled={isLoading || !nickname.trim()}
            >
              {isLoading ? 'Saving...' : 'Save'}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
