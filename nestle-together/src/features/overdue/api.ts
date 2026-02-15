import type { MemberOverdue } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function fetchOverdueChores(
  householdId: string, 
  pinCode: number
): Promise<MemberOverdue[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/overdue`,
    {
      headers: { 'X-Pin-Code': pinCode.toString() },
    }
  );
  
  if (!response.ok) {
    return [];
  }

  const data = await response.json();
  return data.memberOverdue || [];
}
