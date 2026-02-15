import type { Chore, ChoreCompletion, AddChoreRequest, AssignChoreRequest } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function fetchChores(householdId: string): Promise<Chore[]> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/chores`);
  
  if (!response.ok) {
    return [];
  }

  const data = await response.json();
  return (data.chores || []).map((c: Record<string, unknown>) => ({
    id: c.choreId || c.id,
    householdId,
    displayName: c.displayName,
    description: c.description || '',
    assignedTo: c.assignedToMemberIds as string[] | undefined,
    assignedToAll: c.assignToAll as boolean | undefined,
    completed: false,
    createdAt: new Date(),
    frequency: c.frequency,
    lastCompletedAt: c.lastCompletedAt ? new Date(c.lastCompletedAt as string) : undefined,
    lastCompletedBy: c.lastCompletedBy as string | undefined,
    isOptional: c.isOptional as boolean | undefined,
  }));
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
