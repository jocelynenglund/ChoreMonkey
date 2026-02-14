export interface Household {
  id: string;
  name: string;
  pinCode: string;
  createdAt: Date;
}

export interface Member {
  id: string;
  householdId: string;
  nickname: string;
  avatarColor: string;
  joinedAt: Date;
}

export interface ChoreFrequency {
  type: 'daily' | 'weekly' | 'interval' | 'once';
  days?: string[];      // For weekly: ['monday', 'thursday']
  intervalDays?: number; // For interval: every X days
}

export interface MemberCompletion {
  memberId: string;
  completedToday: boolean;
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
}

export interface Invite {
  id: string;
  householdId: string;
  link: string;
  createdAt: Date;
  expiresAt: Date;
}

export interface ChoreCompletion {
  completedBy: string;
  completedAt: Date;
}

export interface OverdueChore {
  choreId: string;
  displayName: string;
  overdueDays: number;
  lastCompleted?: Date;
}

export interface MemberOverdue {
  memberId: string;
  nickname: string;
  overdueCount: number;
  chores: OverdueChore[];
}
