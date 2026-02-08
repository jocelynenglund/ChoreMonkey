import { useState } from 'react';
import { cn } from '@/lib/utils';
import type { Member } from '@/types/household';
import { MemberAvatar } from './MemberAvatar';

interface MemberSelectorProps {
  members: Member[];
  selectedId?: string | null;
  onSelect: (memberId: string) => void;
}

export function MemberSelector({
  members,
  selectedId,
  onSelect,
}: MemberSelectorProps) {
  const [hoveredId, setHoveredId] = useState<string | null>(null);

  return (
    <div className="flex flex-wrap gap-3 justify-center">
      {members.map((member) => (
        <button
          key={member.id}
          onClick={() => onSelect(member.id)}
          onMouseEnter={() => setHoveredId(member.id)}
          onMouseLeave={() => setHoveredId(null)}
          className={cn(
            'flex flex-col items-center gap-2 p-3 rounded-2xl transition-all duration-200',
            selectedId === member.id
              ? 'bg-primary/10 ring-2 ring-primary'
              : 'hover:bg-muted'
          )}
        >
          <MemberAvatar
            nickname={member.nickname}
            color={member.avatarColor}
            size="lg"
            className={cn(
              'transition-transform duration-200',
              (selectedId === member.id || hoveredId === member.id) &&
                'scale-110'
            )}
          />
          <span
            className={cn(
              'text-sm font-medium',
              selectedId === member.id
                ? 'text-primary'
                : 'text-muted-foreground'
            )}
          >
            {member.nickname}
          </span>
        </button>
      ))}
    </div>
  );
}
