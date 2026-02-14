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

export interface Chore {
  id: string;
  householdId: string;
  displayName: string;
  description: string;
  assignedTo?: string;
  completed: boolean;
  createdAt: Date;
  frequency?: ChoreFrequency;
  lastCompletedAt?: Date;
  lastCompletedBy?: string;
}

export interface Invite {
  id: string;
  householdId: string;
  link: string;
  createdAt: Date;
  expiresAt: Date;
}
