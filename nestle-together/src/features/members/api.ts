import type { Member, JoinHouseholdRequest, JoinHouseholdResponse, GenerateInviteResponse } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

const AVATAR_COLORS = [
  'hsl(150 50% 50%)',
  'hsl(15 80% 60%)',
  'hsl(200 70% 55%)',
  'hsl(45 90% 55%)',
  'hsl(280 60% 60%)',
  'hsl(340 70% 60%)',
];

export async function fetchMembers(householdId: string): Promise<Member[]> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/members`);
  
  if (!response.ok) {
    return [];
  }

  const data = await response.json();
  // API returns { members: [...] } or direct array
  const memberArray = Array.isArray(data) ? data : (data.members ?? []);
  
  return memberArray.map((m: Record<string, unknown>, index: number) => ({
    id: (m.memberId ?? m.id) as string,
    householdId,
    nickname: m.nickname as string,
    avatarColor: AVATAR_COLORS[index % AVATAR_COLORS.length],
    joinedAt: m.joinedAt ? new Date(m.joinedAt as string) : new Date(),
    status: (m.status as string) || undefined,
  }));
}

export async function joinHousehold(
  householdId: string, 
  request: JoinHouseholdRequest
): Promise<JoinHouseholdResponse> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to join household');
  }

  return response.json();
}

export async function generateInvite(householdId: string): Promise<GenerateInviteResponse> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/invite`, {
    method: 'POST',
  });

  if (!response.ok) {
    throw new Error('Failed to generate invite');
  }

  return response.json();
}

export async function changeNickname(
  householdId: string, 
  memberId: string, 
  newNickname: string
): Promise<boolean> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/members/${memberId}/nickname`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ newNickname }),
    }
  );

  return response.ok;
}

export async function changeStatus(
  householdId: string, 
  memberId: string, 
  status: string
): Promise<boolean> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/members/${memberId}/status`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ status }),
    }
  );

  return response.ok;
}

export async function removeMember(
  householdId: string, 
  memberId: string, 
  pinCode: number
): Promise<boolean> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/members/${memberId}/remove`,
    {
      method: 'POST',
      headers: { 'X-Pin-Code': pinCode.toString() },
    }
  );

  return response.ok;
}
