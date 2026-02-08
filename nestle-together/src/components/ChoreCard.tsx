import { Check, Trash2, User } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { Chore, Member } from '@/types/household';
import { MemberAvatar } from './MemberAvatar';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';

interface ChoreCardProps {
  chore: Chore;
  members: Member[];
  onToggleComplete: () => void;
  onAssign: (memberId: string | undefined) => void;
  onDelete: () => void;
}

export function ChoreCard({
  chore,
  members,
  onToggleComplete,
  onAssign,
  onDelete,
}: ChoreCardProps) {
  const assignedMember = members.find((m) => m.id === chore.assignedTo);

  return (
    <div
      className={cn(
        'card-elevated p-4 flex items-center gap-4 transition-all duration-200 animate-scale-in',
        chore.completed && 'opacity-60'
      )}
    >
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

      <div className="flex-1 min-w-0">
        <h3
          className={cn(
            'font-semibold text-foreground transition-all',
            chore.completed && 'line-through text-muted-foreground'
          )}
        >
          {chore.displayName}
        </h3>
        {chore.description && (
          <p className="text-sm text-muted-foreground truncate">
            {chore.description}
          </p>
        )}
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
