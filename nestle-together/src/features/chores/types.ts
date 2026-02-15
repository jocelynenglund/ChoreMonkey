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
