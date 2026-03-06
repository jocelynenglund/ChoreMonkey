import type {
  SalarySettings,
  EnableSalaryReportsRequest,
  UpdateBaseSalaryRequest,
  GenerateSalaryReportResponse,
} from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

// ============ Salary Settings ============

export async function fetchSalarySettings(householdId: string): Promise<SalarySettings> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/salary/settings`);
  
  if (!response.ok) {
    return { enabled: false, baseAmount: 800 };
  }

  return response.json();
}

export async function enableSalaryReports(
  householdId: string,
  request: EnableSalaryReportsRequest = {}
): Promise<boolean> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/salary/enable`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    return false;
  }

  const data = await response.json();
  return data.success;
}

export async function updateBaseSalary(
  householdId: string,
  request: UpdateBaseSalaryRequest
): Promise<boolean> {
  const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/salary/base`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    return false;
  }

  const data = await response.json();
  return data.success;
}

// ============ Salary Reports ============

export async function getSalaryReport(
  householdId: string,
  memberId: string,
  period: string
): Promise<GenerateSalaryReportResponse> {
  const params = new URLSearchParams({ memberId, period });
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/salary/report?${params}`
  );

  return response.json();
}
