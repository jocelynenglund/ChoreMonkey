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
}

export interface ChoreCompletion {
  completedBy: string;
  completedAt: Date;
}

export interface AddChoreRequest {
  displayName: string;
  description: string;
  frequency?: ChoreFrequency;
  isOptional?: boolean;
  startDate?: string;
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
