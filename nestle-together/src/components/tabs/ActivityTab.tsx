import { CompletionTimeline } from '@/components/CompletionTimeline';
import { SharedWithBarrio } from '@/features/display';
import { useHouseholdStore } from '@/stores/householdStore';

interface ActivityTabProps {
  householdId: string;
  refreshKey: number;
}

export function ActivityTab({ householdId, refreshKey }: ActivityTabProps) {
  const { isAdmin, currentPinCode } = useHouseholdStore();

  return (
    <div className="space-y-6">
      <SharedWithBarrio householdId={householdId} refreshKey={refreshKey} isAdmin={isAdmin} adminPin={currentPinCode} />
      <CompletionTimeline key={refreshKey} householdId={householdId} />
    </div>
  );
}
