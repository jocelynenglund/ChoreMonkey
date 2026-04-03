// Per-member Salary System types

export interface MemberSalaryConfig {
  memberId: string;
  baseSalary: number;
  deductionMultiplier: number;
  bonusMultiplier: number;
}

export interface ChoreRates {
  choreId: string;
  deductionRate: number;
  bonusRate: number;
}

export interface MissedChore {
  choreId: string;
  choreName: string;
  period: string;
  deduction: number;
}

export interface BonusChore {
  choreId: string;
  choreName: string;
  completedAt: string;
  bonus: number;
}

export interface MemberPeriodSummary {
  memberId: string;
  name: string;
  baseSalary: number;
  deductions: number;
  bonuses: number;
  projected: number;
  missedChores: MissedChore[];
  bonusChores: BonusChore[];
}

export interface CurrentPeriodResponse {
  periodStart: string;
  periodEnd: string;
  members: MemberPeriodSummary[];
}

export interface PayoutSummary {
  memberId: string;
  name: string;
  baseSalary: number;
  deductions: number;
  bonuses: number;
  netPay: number;
}

export interface PeriodPayout {
  periodId: string;
  periodStart: string;
  periodEnd: string;
  payouts: PayoutSummary[];
}

export interface ClosePeriodResponse {
  periodId: string;
  periodStart: string;
  periodEnd: string;
  payouts: PayoutSummary[];
}

export interface SlipDeduction {
  choreName: string;
  baseRate: number;
  multiplier: number;
  amount: number;
}

export interface SlipBonus {
  choreName: string;
  baseRate: number;
  multiplier: number;
  amount: number;
}

export interface AvailablePeriod {
  periodStart: string;
  periodEnd: string;
  isClosed: boolean;
  periodId: string | null;
}

export interface AvailablePeriodsResponse {
  periods: AvailablePeriod[];
}

export interface OfficialSalarySlipResponse {
  periodId: string;
  periodStart: string;
  periodEnd: string;
  memberName: string;
  baseSalary: number;
  deductions: SlipDeduction[];
  bonuses: SlipBonus[];
  grossDeductions: number;
  grossBonuses: number;
  netPay: number;
}
