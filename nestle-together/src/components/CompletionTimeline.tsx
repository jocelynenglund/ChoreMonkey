import { useEffect, useState } from 'react';
import { 
  Clock, 
  CheckCircle2, 
  Users, 
  UserPlus, 
  PenLine, 
  MessageSquare,
  Plus 
} from 'lucide-react';
import { useHouseholdStore } from '@/stores/householdStore';

interface ActivityEntry {
  type: string;       // "completion", "assignment", "nickname_change", "status_change", "member_joined", "chore_created"
  description: string;
  timestamp: string;
  choreId?: string;
  memberId?: string;
}

interface CompletionTimelineProps {
  householdId: string;
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

function getActivityIcon(type: string) {
  switch (type) {
    case 'completion':
      return <CheckCircle2 className="h-4 w-4 text-green-500" />;
    case 'assignment':
      return <Users className="h-4 w-4 text-blue-500" />;
    case 'nickname_change':
      return <PenLine className="h-4 w-4 text-purple-500" />;
    case 'status_change':
      return <MessageSquare className="h-4 w-4 text-orange-500" />;
    case 'member_joined':
      return <UserPlus className="h-4 w-4 text-teal-500" />;
    case 'chore_created':
      return <Plus className="h-4 w-4 text-primary" />;
    default:
      return <Clock className="h-4 w-4 text-muted-foreground" />;
  }
}

export function CompletionTimeline({ householdId }: CompletionTimelineProps) {
  const [activities, setActivities] = useState<ActivityEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    const fetchActivities = async () => {
      setIsLoading(true);
      try {
        const apiUrl = import.meta.env.VITE_API_URL || 'https://localhost:7422';
        const response = await fetch(`${apiUrl}/api/households/${householdId}/completions?days=7&limit=20`);
        if (response.ok) {
          const data = await response.json();
          setActivities(data.activities ?? []);
        }
      } catch (error) {
        console.error('Failed to fetch activities', error);
      }
      setIsLoading(false);
    };

    fetchActivities();
  }, [householdId]);

  if (isLoading) {
    return (
      <div className="rounded-lg border bg-card p-4">
        <p className="text-muted-foreground text-sm">Loading activity...</p>
      </div>
    );
  }

  if (!activities || activities.length === 0) {
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
        {(activities ?? []).map((activity, idx) => (
          <div key={idx} className="p-3 flex items-center gap-3">
            <div className="flex-shrink-0">
              {getActivityIcon(activity.type)}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm">{activity.description}</p>
            </div>
            <div className="text-xs text-muted-foreground flex-shrink-0">
              {formatTimeAgo(activity.timestamp)}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
