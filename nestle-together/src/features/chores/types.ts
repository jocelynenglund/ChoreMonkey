// Core chore types
export interface ChoreFrequency {
  type: 'daily' | 'weekly' | 'interval' | 'once';
  days?: string[];
  intervalDays?: number;
}

export interface MemberCompletion {
  memberId: string;
  completedToday: boolean;
  completedThisWeek: boolean;
  lastCompletedAt?: Date;
}

export interface Chore {
  id: string;
  householdId: string;
  displayName: string;
  description: string;
  assignedTo?: string[];
  assignedToAll?: boolean;
  completed: boolean;
  createdAt: Date;
  frequency?: ChoreFrequency;
  lastCompletedAt?: Date;
  lastCompletedBy?: string;
  memberCompletions?: MemberCompletion[];
  isOptional?: boolean;
  isRequired?: boolean;
  missedDeduction?: number;
  deductionRate?: number;
  bonusRate?: number;
}

export interface ChoreCompletion {
  completedBy: string;
  completedAt: Date;
}

export interface AddChoreRequest {
  choreId?: string;  // Client-generated for idempotency
  displayName: string;
  description: string;
  frequency?: ChoreFrequency;
  isOptional?: boolean;
  startDate?: string;
  isRequired?: boolean;
  missedDeduction?: number;
}

export interface AssignChoreRequest {
  memberIds?: string[];
  assignToAll?: boolean;
  assignedByMemberId?: string;
}

// My Chores types (personal view)
export interface MyPendingChore {
  choreId: string;
  displayName: string;
  frequencyType?: string;
  dueDescription?: string;
}

export interface MyOverdueChore {
  choreId: string;
  displayName: string;
  frequencyType?: string;
  overduePeriod: string;
  periodKey: string;
}

export interface MyCompletedChore {
  choreId: string;
  displayName: string;
  completedAt: Date;
  completedByName?: string;  // set when someone else did a shared chore
}

export interface MyChoresResponse {
  pending: MyPendingChore[];
  overdue: MyOverdueChore[];
  completed: MyCompletedChore[];
}

// Overdue types (admin view)
export interface OverdueChore {
  choreId: string;
  displayName: string;
  overduePeriod: string;
  lastCompleted?: Date;
}

export interface MemberOverdue {
  memberId: string;
  nickname: string;
  overdueCount: number;
  chores: OverdueChore[];
}
