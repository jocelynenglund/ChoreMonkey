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
