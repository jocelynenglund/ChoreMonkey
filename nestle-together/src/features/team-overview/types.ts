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
