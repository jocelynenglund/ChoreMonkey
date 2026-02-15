import type { Household, AccessResponse, CreateHouseholdRequest, CreateHouseholdResponse } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export async function createHousehold(request: CreateHouseholdRequest): Promise<CreateHouseholdResponse> {
  const response = await fetch(`${API_BASE_URL}/api/households`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    throw new Error('Failed to create household');
  }

  return response.json();
}

export async function accessHousehold(householdId: string, pinCode: number): Promise<AccessResponse> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/access`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ pinCode }),
  });

  if (!response.ok) {
    return { success: false, householdId, isAdmin: false };
  }

  return response.json();
}

export async function getHousehold(householdId: string): Promise<Household | null> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}`);
  
  if (!response.ok) {
    return null;
  }

  const data = await response.json();
  return {
    id: data.householdId || data.id,
    name: data.householdName || data.name || 'My Household',
    pinCode: '',
    createdAt: data.createdAt ? new Date(data.createdAt) : new Date(),
  };
}
