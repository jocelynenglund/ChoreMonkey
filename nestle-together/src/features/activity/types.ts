// Activity Timeline types
export interface Activity {
  id: string;
  type: string;
  text: string;
  timestamp: Date;
  memberId?: string;
  choreId?: string;
}

// Team Overview types (admin view)
export interface ChoreStatus {
  choreId: string;
  displayName: string;
  status: 'completed' | 'pending' | 'overdue';
  overduePeriod?: string;
  lastCompletedAt?: Date;
  isOptional: boolean;
}

export interface MemberOverview {
  memberId: string;
  nickname: string;
  totalChores: number;
  completedCount: number;
  overdueCount: number;
  chores: ChoreStatus[];
}

export interface TeamOverviewResponse {
  members: MemberOverview[];
}
