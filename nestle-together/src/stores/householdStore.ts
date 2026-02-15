// Re-export from the new feature-based store for backwards compatibility
// TODO: Gradually migrate components to import directly from @/features/store

export { useAppStore, useHouseholdStore } from '@/features/store';

// Re-export types for backwards compatibility
export type { Household, AccessResponse } from '@/features/household/types';
export type { Member, Invite } from '@/features/members/types';
export type { Chore, ChoreFrequency, MemberCompletion, ChoreCompletion } from '@/features/chores/types';
export type { MyChoresResponse, MyPendingChore, MyOverdueChore, MyCompletedChore } from '@/features/my-chores/types';
export type { MemberOverdue, OverdueChore } from '@/features/overdue/types';
