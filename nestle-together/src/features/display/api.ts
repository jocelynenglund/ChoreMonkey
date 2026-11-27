import type { DisplayLately, DisplayLink, DisplayToday } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

const base = (householdId: string) => `${API_BASE_URL}/api/households/${householdId}`;

async function failure(response: Response, fallback: string): Promise<Error> {
  const data = await response.json().catch(() => ({}));
  return new Error((data as { error?: string }).error || fallback);
}

// ============ What the household's own screen shows (any member) ============

export async function fetchDisplayToday(householdId: string): Promise<DisplayToday> {
  const response = await fetch(`${base(householdId)}/display/today`);
  if (!response.ok) throw await failure(response, "Couldn't load today's chores");
  return response.json();
}

export async function fetchDisplayLately(householdId: string): Promise<DisplayLately> {
  const response = await fetch(`${base(householdId)}/display/lately`);
  if (!response.ok) throw await failure(response, "Couldn't load the last 40 days");
  return response.json();
}

// ============ Display links (admin) ============

export async function fetchDisplayLinks(householdId: string, adminPin: string): Promise<DisplayLink[]> {
  const response = await fetch(`${base(householdId)}/display-links`, {
    headers: { 'X-Pin-Code': adminPin },
  });
  if (!response.ok) throw await failure(response, "Couldn't load the links");
  const data = await response.json();
  return data.links ?? [];
}

export async function makeDisplayLink(householdId: string, adminPin: string, label = 'barrio'): Promise<DisplayLink> {
  const response = await fetch(`${base(householdId)}/display-links`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Pin-Code': adminPin },
    body: JSON.stringify({ label }),
  });
  if (!response.ok) throw await failure(response, "Couldn't make a link");
  return response.json();
}

export async function revokeDisplayLink(householdId: string, adminPin: string, linkId: string): Promise<void> {
  const response = await fetch(`${base(householdId)}/display-links/${linkId}`, {
    method: 'DELETE',
    headers: { 'X-Pin-Code': adminPin },
  });
  if (!response.ok) throw await failure(response, "Couldn't revoke the link");
}
