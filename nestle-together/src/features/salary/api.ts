import type {
  MemberSalaryConfig,
  ChoreRates,
  CurrentPeriodResponse,
  ClosePeriodResponse,
  PeriodPayout,
  OfficialSalarySlipResponse,
} from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

// ============ Configure Payday ============

export async function configurePayday(
  householdId: string,
  paydayDayOfMonth: number,
  pinCode: number
): Promise<{ paydayDayOfMonth: number } | null> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/salary/payday`,
    {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ paydayDayOfMonth, pinCode }),
    }
  );

  if (!response.ok) {
    return null;
  }

  return response.json();
}

// ============ Member Salary ============

export async function setMemberSalary(
  householdId: string,
  memberId: string,
  config: Omit<MemberSalaryConfig, 'memberId'>
): Promise<boolean> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/members/${memberId}/salary`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config),
    }
  );
  return response.ok;
}

// ============ Chore Rates ============

export async function setChoreRates(
  householdId: string,
  choreId: string,
  rates: Omit<ChoreRates, 'choreId'>
): Promise<boolean> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/rates`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(rates),
    }
  );
  return response.ok;
}

// ============ Current Period ============

export async function getCurrentPeriod(
  householdId: string,
  memberId?: string
): Promise<CurrentPeriodResponse | null> {
  const url = memberId
    ? `${API_BASE_URL}/api/households/${householdId}/salary/current?memberId=${memberId}`
    : `${API_BASE_URL}/api/households/${householdId}/salary/current`;
    
  const response = await fetch(url);
  
  if (!response.ok) {
    return null;
  }
  
  return response.json();
}

// ============ Close Period ============

export async function closePeriod(
  householdId: string,
  periodEnd: Date
): Promise<ClosePeriodResponse | null> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/salary/close-period`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ periodEnd: periodEnd.toISOString() }),
    }
  );
  
  if (!response.ok) {
    return null;
  }
  
  return response.json();
}

// ============ Payout History ============

export async function getPayoutHistory(
  householdId: string
): Promise<PeriodPayout[]> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/salary/history`
  );

  if (!response.ok) {
    return [];
  }

  return response.json();
}

// ============ Official Salary Slip ============

export async function getOfficialSalarySlip(
  householdId: string,
  periodId: string,
  memberId: string
): Promise<OfficialSalarySlipResponse | null> {
  const response = await fetch(
    `${API_BASE_URL}/api/households/${householdId}/salary/periods/${periodId}/slip/${memberId}`
  );

  if (!response.ok) {
    return null;
  }

  return response.json();
}
