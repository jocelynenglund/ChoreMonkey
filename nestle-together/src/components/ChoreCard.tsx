import { Check, Trash2, User, Calendar, Repeat, Clock } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { Chore, Member, ChoreFrequency } from '@/types/household';
import { MemberAvatar } from './MemberAvatar';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

interface ChoreCardProps {
  chore: Chore;
  members: Member[];
  currentMemberId?: string;
  onToggleComplete: () => void;
  onComplete?: () => void;
  onAssign: (memberId: string | undefined) => void;
  onDelete: () => void;
}

function formatFrequency(frequency?: ChoreFrequency): string {
  if (!frequency) return '';
  switch (frequency.type) {
    case 'daily':
      return 'Daily';
    case 'weekly':
      if (frequency.days && frequency.days.length > 0) {
        const dayNames = frequency.days.map(d => d.charAt(0).toUpperCase() + d.slice(1, 3));
        return dayNames.join(', ');
      }
      return 'Weekly';
    case 'interval':
      return `Every ${frequency.intervalDays} days`;
    case 'once':
    default:
      return 'One-time';
  }
}

function formatLastCompleted(date?: Date): string {
  if (!date) return 'Never';
  const now = new Date();
  const diff = now.getTime() - new Date(date).getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));
  if (days === 0) return 'Today';
  if (days === 1) return 'Yesterday';
  return `${days} days ago`;
}

export function ChoreCard({
  chore,
  members,
  currentMemberId,
  onToggleComplete,
  onComplete,
  onAssign,
  onDelete,
}: ChoreCardProps) {
  const assignedMember = members.find((m) => m.id === chore.assignedTo);
  const lastCompletedByMember = members.find((m) => m.id === chore.lastCompletedBy);
  const isRecurring = chore.frequency && chore.frequency.type !== 'once';

  return (
    <div
      className={cn(
        'card-elevated p-4 flex items-center gap-4 transition-all duration-200 animate-scale-in',
        chore.completed && !isRecurring && 'opacity-60'
      )}
    >
      {/* Complete button - different behavior for recurring vs one-time */}
      {isRecurring ? (
        <Button
          size="sm"
          variant="outline"
          onClick={onComplete}
          className="flex-shrink-0 gap-1"
        >
          <Check className="w-4 h-4" />
          Done
        </Button>
      ) : (
        <button
          onClick={onToggleComplete}
          className={cn(
            'w-7 h-7 rounded-full border-2 flex items-center justify-center transition-all duration-200 flex-shrink-0',
            chore.completed
              ? 'bg-success border-success text-success-foreground'
              : 'border-muted-foreground/30 hover:border-primary hover:bg-primary/10'
          )}
        >
          {chore.completed && <Check className="w-4 h-4" />}
        </button>
      )}

      <div className="flex-1 min-w-0">
        <h3
          className={cn(
            'font-semibold text-foreground transition-all',
            chore.completed && !isRecurring && 'line-through text-muted-foreground'
          )}
        >
          {chore.displayName}
        </h3>
        <div className="flex items-center gap-3 text-sm text-muted-foreground">
          {chore.description && (
            <span className="truncate">{chore.description}</span>
          )}
        </div>
        <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
          {chore.frequency && (
            <span className="flex items-center gap-1">
              <Repeat className="w-3 h-3" />
              {formatFrequency(chore.frequency)}
            </span>
          )}
          {isRecurring && (
            <span className="flex items-center gap-1">
              <Clock className="w-3 h-3" />
              {formatLastCompleted(chore.lastCompletedAt)}
              {lastCompletedByMember && ` by ${lastCompletedByMember.nickname}`}
            </span>
          )}
        </div>
      </div>

      <div className="flex items-center gap-2">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button className="p-1 rounded-full hover:bg-muted transition-colors">
              {assignedMember ? (
                <MemberAvatar
                  nickname={assignedMember.nickname}
                  color={assignedMember.avatarColor}
                  size="sm"
                />
              ) : (
                <div className="w-8 h-8 rounded-full border-2 border-dashed border-muted-foreground/30 flex items-center justify-center">
                  <User className="w-4 h-4 text-muted-foreground/50" />
                </div>
              )}
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => onAssign(undefined)}>
              <User className="w-4 h-4 mr-2" />
              Unassigned
            </DropdownMenuItem>
            {members.map((member) => (
              <DropdownMenuItem
                key={member.id}
                onClick={() => onAssign(member.id)}
              >
                <MemberAvatar
                  nickname={member.nickname}
                  color={member.avatarColor}
                  size="sm"
                  className="mr-2"
                />
                {member.nickname}
              </DropdownMenuItem>
            ))}
          </DropdownMenuContent>
        </DropdownMenu>

        <button
          onClick={onDelete}
          className="p-2 rounded-full hover:bg-destructive/10 text-muted-foreground hover:text-destructive transition-colors"
        >
          <Trash2 className="w-4 h-4" />
        </button>
      </div>
    </div>
  );
}
