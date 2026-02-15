import type { GenerateInviteResponse, InviteInfo } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function generateInvite(householdId: string): Promise<GenerateInviteResponse> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/invite`, {
    method: 'POST',
  });

  if (!response.ok) {
    throw new Error('Failed to generate invite');
  }

  return response.json();
}

export async function getInviteInfo(
  householdId: string, 
  inviteCode: string
): Promise<InviteInfo | null> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/invite/${inviteCode}`
  );

  if (!response.ok) {
    return null;
  }

  return response.json();
}
