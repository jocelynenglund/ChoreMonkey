import { useEffect, useState, useCallback } from 'react';
import { Clock, CheckCircle2, UserPlus } from 'lucide-react';
import { MemberAvatar } from './MemberAvatar';
import { useHouseholdStore } from '@/stores/householdStore';
import { householdConnection } from '@/lib/signalr';

interface ActivityEntry {
  type: 'completion' | 'member_joined';
  timestamp: string;
  choreId?: string;
  choreName?: string;
  memberId: string;
  memberNickname: string;
}

interface CompletionTimelineProps {
  householdId: string;
  refreshKey?: number;
}

function formatTimeAgo(dateString: string): string {
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / (1000 * 60));
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7) return `${diffDays} days ago`;
  return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
}

export function CompletionTimeline({ householdId, refreshKey = 0 }: CompletionTimelineProps) {
  const [activities, setActivities] = useState<ActivityEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const { members } = useHouseholdStore();

  const fetchActivities = useCallback(async () => {
    try {
      const apiUrl = import.meta.env.VITE_API_URL || 'https://localhost:7422';
      const response = await fetch(`${apiUrl}/api/households/${householdId}/completions?days=7&limit=20`);
      if (response.ok) {
        const data = await response.json();
        // Use activities if available, fall back to completions for backwards compat
        if (data.activities) {
          setActivities(data.activities);
        } else if (data.completions) {
          // Convert old format to new format
          setActivities(data.completions.map((c: Record<string, unknown>) => ({
            type: 'completion' as const,
            timestamp: c.completedAt as string,
            choreId: c.choreId as string,
            choreName: c.choreName as string,
            memberId: c.completedBy as string,
            memberNickname: c.completedByNickname as string,
          })));
        }
      }
    } catch (error) {
      console.error('Failed to fetch activities', error);
    }
    setIsLoading(false);
  }, [householdId]);

  // Initial fetch
  useEffect(() => {
    setIsLoading(true);
    fetchActivities();
  }, [fetchActivities, refreshKey]);

  // Subscribe to real-time updates
  useEffect(() => {
    const unsubscribe = householdConnection.subscribe((event) => {
      if (event.type === 'ChoreCompleted' || event.type === 'MemberJoined') {
        fetchActivities();
      }
    });
    return unsubscribe;
  }, [fetchActivities]);

  const getMemberColor = (memberId: string) => {
    const member = members.find((m) => m.id === memberId);
    return member?.avatarColor || 'hsl(150 50% 50%)';
  };

  if (isLoading) {
    return (
      <div className="rounded-lg border bg-card p-4">
        <p className="text-muted-foreground text-sm">Loading activity...</p>
      </div>
    );
  }

  if (activities.length === 0) {
    return (
      <div className="rounded-lg border bg-card p-4 flex items-center gap-2">
        <Clock className="h-5 w-5 text-muted-foreground" />
        <p className="text-sm text-muted-foreground">No recent activity</p>
      </div>
    );
  }

  return (
    <div className="rounded-lg border bg-card">
      <div className="p-4 border-b">
        <h3 className="font-semibold flex items-center gap-2">
          <Clock className="h-5 w-5 text-primary" />
          Recent Activity
        </h3>
      </div>
      <div className="divide-y max-h-80 overflow-y-auto">
        {activities.map((activity, idx) => (
          <div key={idx} className="p-3 flex items-center gap-3">
            <MemberAvatar
              nickname={activity.memberNickname}
              color={getMemberColor(activity.memberId)}
              size="sm"
            />
            <div className="flex-1 min-w-0">
              {activity.type === 'completion' ? (
                <p className="text-sm">
                  <span className="font-medium">{activity.memberNickname}</span>
                  <span className="text-muted-foreground"> completed </span>
                  <span className="font-medium">{activity.choreName}</span>
                </p>
              ) : (
                <p className="text-sm">
                  <span className="font-medium">{activity.memberNickname}</span>
                  <span className="text-muted-foreground"> joined the household</span>
                </p>
              )}
            </div>
            <div className="flex items-center gap-1 text-xs text-muted-foreground flex-shrink-0">
              {activity.type === 'completion' ? (
                <CheckCircle2 className="h-3 w-3 text-green-500" />
              ) : (
                <UserPlus className="h-3 w-3 text-blue-500" />
              )}
              {formatTimeAgo(activity.timestamp)}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
