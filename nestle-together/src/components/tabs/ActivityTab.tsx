import { CompletionTimeline } from '@/components/CompletionTimeline';

interface ActivityTabProps {
  householdId: string;
  refreshKey: number;
}

export function ActivityTab({ householdId, refreshKey }: ActivityTabProps) {
  return <CompletionTimeline key={refreshKey} householdId={householdId} />;
}
