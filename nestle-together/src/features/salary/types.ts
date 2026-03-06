// Salary Reports types

export interface SalarySettings {
  enabled: boolean;
  baseAmount: number;
}

export interface SalaryDeduction {
  choreId: string;
  choreName: string;
  missedPeriod: string;
  amount: number;
}

export interface EnableSalaryReportsRequest {
  baseAmount?: number;
}

export interface UpdateBaseSalaryRequest {
  newBaseAmount: number;
}

export interface GenerateSalaryReportResponse {
  success: boolean;
  period?: string;
  baseAmount?: number;
  deductions?: SalaryDeduction[];
  finalAmount?: number;
  error?: string;
}
