import type { Activity } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function fetchActivityTimeline(
  householdId: string, 
  limit = 20
): Promise<Activity[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/activity?limit=${limit}`
  );
  
  if (!response.ok) {
    return [];
  }

  const data = await response.json();
  return (data.activities || []).map((a: Record<string, unknown>) => ({
    id: a.id || crypto.randomUUID(),
    type: a.type,
    text: a.text,
    timestamp: new Date(a.timestamp as string),
    memberId: a.memberId,
    choreId: a.choreId,
  }));
}
