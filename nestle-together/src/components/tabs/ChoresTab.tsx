import { ClipboardList } from 'lucide-react';
import { ChoreCard } from '@/components/ChoreCard';
import { AddChoreDialog } from '@/components/AddChoreDialog';
import { MyChoresSection } from '@/components/MyChoresSection';
import type { Chore, ChoreFrequency } from '@/types/household';
import type { Member } from '@/features/members/types';

interface ChoresTabProps {
  householdId: string;
  currentMemberId: string | null | undefined;
  members: Member[];
  chores: Chore[];
  refreshKey: number;
  isAdmin: boolean;
  onCompleteChore: (chore: Chore | string) => void;
  onAssignChore: (choreId: string, memberIds?: string[], assignToAll?: boolean) => void;
  onDeleteChore: (choreId: string) => void;
  onAddChore: (displayName: string, description: string, frequency?: ChoreFrequency, isOptional?: boolean, startDate?: Date, isRequired?: boolean, missedDeduction?: number) => Promise<{ id: string } | null>;
  onSetChoreRates: (choreId: string, deductionRate: number, bonusRate: number) => Promise<void>;
}

export function ChoresTab({
  householdId, currentMemberId, members, chores, refreshKey, isAdmin,
  onCompleteChore, onAssignChore, onDeleteChore, onAddChore, onSetChoreRates,
}: ChoresTabProps) {
  const isAssignedToMe = (chore: Chore) =>
    chore.assignedToAll || (chore.assignedTo?.includes(currentMemberId || '') ?? false);

  const otherChores = chores.filter((c) => !c.isOptional && !isAssignedToMe(c) && !c.completed);
  const bonusChores = chores.filter((c) => c.isOptional && !c.completed);

  return (
    <div>
      {/* My Chores header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <ClipboardList className="w-5 h-5 text-primary" />
          <h2 className="font-bold text-lg">My Chores</h2>
        </div>
        <AddChoreDialog onAdd={onAddChore} onSetRates={onSetChoreRates} />
      </div>

      {currentMemberId && (
        <MyChoresSection
          key={refreshKey}
          householdId={householdId}
          memberId={currentMemberId}
          onCompleteChore={onCompleteChore}
        />
      )}

      {otherChores.length > 0 && (
        <div className="mt-8">
          <div className="flex items-center gap-2 mb-3">
            <h3 className="font-semibold text-muted-foreground">📋 Other Chores</h3>
            <span className="px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-xs font-medium">
              {otherChores.length}
            </span>
          </div>
          <div className="space-y-3">
            {otherChores.map((chore) => (
              <ChoreCard
                key={chore.id}
                chore={chore}
                members={members}
                currentMemberId={currentMemberId || undefined}
                isAdmin={isAdmin}
                onComplete={() => onCompleteChore(chore)}
                onAssign={(memberIds, assignToAll) => onAssignChore(chore.id, memberIds, assignToAll)}
                onDelete={() => onDeleteChore(chore.id)}
              />
            ))}
          </div>
        </div>
      )}

      {bonusChores.length > 0 && (
        <div className="mt-8">
          <div className="flex items-center gap-2 mb-3">
            <h3 className="font-semibold text-amber-600">🌟 Bonus Chores</h3>
            <span className="px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 text-xs font-medium">
              {bonusChores.length}
            </span>
          </div>
          <div className="space-y-3">
            {bonusChores.map((chore) => (
              <ChoreCard
                key={chore.id}
                chore={chore}
                members={members}
                currentMemberId={currentMemberId || undefined}
                isAdmin={isAdmin}
                onComplete={() => onCompleteChore(chore)}
                onAssign={(memberIds, assignToAll) => onAssignChore(chore.id, memberIds, assignToAll)}
                onDelete={() => onDeleteChore(chore.id)}
              />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
