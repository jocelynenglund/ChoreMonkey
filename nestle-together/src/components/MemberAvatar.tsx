import { cn } from '@/lib/utils';

interface MemberAvatarProps {
  nickname: string;
  color: string;
  size?: 'sm' | 'md' | 'lg';
  className?: string;
}

const sizeClasses = {
  sm: 'w-8 h-8 text-xs',
  md: 'w-10 h-10 text-sm',
  lg: 'w-14 h-14 text-lg',
};

export function MemberAvatar({
  nickname,
  color,
  size = 'md',
  className,
}: MemberAvatarProps) {
  const initials = nickname
    .split(' ')
    .map((n) => n[0])
    .join('')
    .toUpperCase()
    .slice(0, 2);

  return (
    <div
      className={cn(
        'rounded-full flex items-center justify-center font-bold text-white shadow-sm',
        sizeClasses[size],
        className
      )}
      style={{ backgroundColor: color }}
    >
      {initials}
    </div>
  );
}
