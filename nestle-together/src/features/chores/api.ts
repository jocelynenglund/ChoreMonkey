import type { Chore, ChoreCompletion, AddChoreRequest, AssignChoreRequest } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function fetchChores(householdId: string): Promise<Chore[]> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/chores`);
  
  if (!response.ok) {
    return [];
  }

  const data = await response.json();
  console.log('[DEBUG] Raw chores API response:', data);
  const choreArray = Array.isArray(data) ? data : (data.chores ?? []);
  console.log('[DEBUG] Chore array:', choreArray);
  
  return choreArray.map((c: Record<string, unknown>) => {
    const frequency = c.frequency as import('./types').ChoreFrequency | undefined;
    const lastCompletedAt = c.lastCompletedAt ? new Date(c.lastCompletedAt as string) : undefined;
    // One-time chores are "completed" once they have any completion
    const isOneTime = !frequency || frequency.type === 'once';
    const completed = isOneTime && lastCompletedAt != null;
    
    return {
      id: (c.choreId ?? c.id) as string,
      householdId,
      displayName: c.displayName as string,
      description: (c.description || '') as string,
      assignedTo: c.assignedTo as string[] | undefined,
      assignedToAll: c.assignedToAll as boolean | undefined,
      completed,
      createdAt: c.createdAt ? new Date(c.createdAt as string) : new Date(),
      frequency,
      lastCompletedAt,
      lastCompletedBy: c.lastCompletedBy as string | undefined,
      memberCompletions: c.memberCompletions as import('./types').MemberCompletion[] | undefined,
      isOptional: c.isOptional as boolean | undefined,
    };
  });
}

export async function addChore(householdId: string, request: AddChoreRequest): Promise<void> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/chores`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to add chore');
  }
}

export async function completeChore(
  householdId: string, 
  choreId: string, 
  memberId: string,
  completedAt?: Date
): Promise<{ completedAt: string; completedBy: string }> {
  const body: Record<string, unknown> = { memberId };
  if (completedAt) {
    body.completedAt = completedAt.toISOString();
  }

  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/complete`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to complete chore');
  }

  return response.json();
}

export async function assignChore(
  householdId: string, 
  choreId: string, 
  request: AssignChoreRequest
): Promise<void> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/assign`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    }
  );

  if (!response.ok) {
    throw new Error('Failed to assign chore');
  }
}

export async function deleteChore(
  householdId: string, 
  choreId: string, 
  pinCode: number
): Promise<boolean> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/delete`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ pinCode }),
    }
  );

  return response.ok;
}

export async function fetchChoreHistory(
  householdId: string, 
  choreId: string
): Promise<ChoreCompletion[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/history`
  );
  
  if (!response.ok) {
    return [];
  }

  const data = await response.json();
  return (data.completions || []).map((c: Record<string, unknown>) => ({
    completedBy: c.completedByNickname || c.completedBy,
    completedAt: new Date(c.completedAt as string),
  }));
}
