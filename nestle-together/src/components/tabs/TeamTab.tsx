import { MemberAvatar } from '@/components/MemberAvatar';
import { InviteDialog } from '@/components/InviteDialog';
import { RemoveMemberDialog } from '@/components/RemoveMemberDialog';
import { TeamOverviewAccordion } from '@/components/TeamOverviewAccordion';
import type { Member } from '@/features/members/types';

// Marquee for member status
function StatusMarquee({ text }: { text: string }) {
  const duration = Math.max(6, text.length * 0.12);
  return (
    <div className="mt-3 py-2 bg-muted/50 rounded-md overflow-hidden">
      <div
        className="inline-flex whitespace-nowrap animate-marquee"
        style={{ animationDuration: `${duration}s` }}
      >
        <span className="text-sm text-muted-foreground px-4">💬 {text}</span>
        <span className="text-sm text-muted-foreground px-4">💬 {text}</span>
      </div>
    </div>
  );
}

interface TeamTabProps {
  householdId: string;
  members: Member[];
  currentMemberId: string | null | undefined;
  isAdmin: boolean;
  refreshKey: number;
  hoveredMemberStatus: string | null;
  onHoverStatus: (status: string | null) => void;
  onGenerateInvite: () => ReturnType<typeof Promise.resolve>;
  onRemoveMember: (memberId: string, pinCode: string) => Promise<boolean>;
  onAssignmentChange: () => void;
}

export function TeamTab({
  householdId, members, currentMemberId, isAdmin, refreshKey,
  hoveredMemberStatus, onHoverStatus, onGenerateInvite, onRemoveMember, onAssignmentChange,
}: TeamTabProps) {
  return (
    <div>
      {/* Members Strip */}
      <div className="card-elevated p-4 mb-6">
        <div className="flex items-center justify-between mb-3">
          <h2 className="font-semibold text-sm text-muted-foreground">Family Members</h2>
          <InviteDialog onGenerate={onGenerateInvite} />
        </div>
        <div className="flex gap-3 overflow-x-auto pb-2 pt-1 px-1 -mx-1">
          {members.map((member) => (
            <div
              key={member.id}
              className="relative group flex-shrink-0"
              style={{ width: '72px', minWidth: '72px' }}
            >
              <button
                className="flex flex-col items-center gap-1 cursor-pointer w-full"
                title={member.status || member.nickname}
                onClick={() =>
                  member.status &&
                  onHoverStatus(hoveredMemberStatus === member.status ? null : member.status)
                }
              >
                <div className="relative">
                  {member.status && (
                    <span className="absolute inset-0 rounded-full ring-2 ring-primary/50 ring-offset-2 animate-pulse" />
                  )}
                  <MemberAvatar nickname={member.nickname} color={member.avatarColor} size="md" />
                </div>
                <span
                  className="text-xs text-muted-foreground text-center"
                  style={{ display: 'block', width: '72px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}
                >
                  {member.nickname}
                </span>
              </button>
              {isAdmin && member.id !== currentMemberId && (
                <div className="absolute -top-1 -right-1 opacity-0 group-hover:opacity-100 transition-opacity">
                  <RemoveMemberDialog
                    member={member}
                    onRemove={(pinCode) => onRemoveMember(member.id, pinCode)}
                  />
                </div>
              )}
            </div>
          ))}
        </div>
        {hoveredMemberStatus && <StatusMarquee text={hoveredMemberStatus} />}
      </div>

      {/* Team Overview */}
      <TeamOverviewAccordion
        householdId={householdId}
        onAssignmentChange={onAssignmentChange}
        refreshKey={refreshKey}
      />
    </div>
  );
}
