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
