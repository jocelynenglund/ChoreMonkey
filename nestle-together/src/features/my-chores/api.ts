import type { MyChoresResponse } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function fetchMyChores(
  householdId: string, 
  memberId: string
): Promise<MyChoresResponse | null> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/my-chores?memberId=${memberId}`
  );
  
  if (!response.ok) {
    return null;
  }

  const data = await response.json();
  return {
    pending: data.pending || [],
    overdue: data.overdue || [],
    completed: (data.completed || []).map((c: Record<string, unknown>) => ({
      ...c,
      completedAt: new Date(c.completedAt as string),
    })),
  };
}

export async function acknowledgeMissed(
  householdId: string,
  choreId: string,
  memberId: string,
  period: string
): Promise<boolean> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/acknowledge-missed`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ memberId, period }),
    }
  );

  return response.ok;
}
