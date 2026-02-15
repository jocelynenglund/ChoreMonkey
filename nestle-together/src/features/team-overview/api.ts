import type { MemberOverview, ChoreStatus } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function fetchTeamOverview(
  householdId: string,
  pinCode: number
): Promise<MemberOverview[]> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/team`, {
    headers: {
      'X-Pin-Code': pinCode.toString(),
    },
  });

  if (!response.ok) {
    return [];
  }

  const data = await response.json();
  
  return (data.members || []).map((m: Record<string, unknown>) => ({
    memberId: m.memberId as string,
    nickname: m.nickname as string,
    totalChores: m.totalChores as number,
    completedCount: m.completedCount as number,
    overdueCount: m.overdueCount as number,
    chores: ((m.chores || []) as Record<string, unknown>[]).map((c) => ({
      choreId: c.choreId as string,
      displayName: c.displayName as string,
      status: c.status as 'completed' | 'pending' | 'overdue',
      overduePeriod: c.overduePeriod as string | undefined,
      lastCompletedAt: c.lastCompletedAt ? new Date(c.lastCompletedAt as string) : undefined,
      isOptional: c.isOptional as boolean,
    })),
  }));
}
