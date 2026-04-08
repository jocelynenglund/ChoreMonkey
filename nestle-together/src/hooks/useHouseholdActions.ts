import { useCallback } from 'react';
import { useHouseholdStore } from '@/stores/householdStore';
import { setChoreRates } from '@/features/salary/api';
import type { Household, Chore, ChoreFrequency } from '@/types/household';

interface UseHouseholdActionsProps {
  household: Household | null;
  chores: Chore[];
  setChores: React.Dispatch<React.SetStateAction<Chore[]>>;
  bumpRefreshKey: () => void;
}

export function useHouseholdActions({ household, chores, setChores, bumpRefreshKey }: UseHouseholdActionsProps) {
  const { addChore, completeChore, assignChore, deleteChore, generateInvite, getHouseholdChores, currentMemberId } = useHouseholdStore();

  const handleAddChore = useCallback(async (
    displayName: string, description: string, frequency?: ChoreFrequency,
    isOptional?: boolean, startDate?: Date, isRequired?: boolean, missedDeduction?: number
  ) => {
    if (!household) return null;
    const newChore = await addChore(household.id, displayName, description, frequency, isOptional, startDate, isRequired, missedDeduction);
    if (newChore) {
      setChores((prev) => [...prev, newChore]);
      bumpRefreshKey();
      return { id: newChore.id };
    }
    return null;
  }, [household, addChore, setChores, bumpRefreshKey]);

  const handleSetChoreRates = useCallback(async (choreId: string, deductionRate: number, bonusRate: number) => {
    if (!household) return;
    await setChoreRates(household.id, choreId, { deductionRate, bonusRate });
  }, [household]);

  const handleCompleteChore = useCallback(async (choreId: string, completedAt?: Date) => {
    if (!household || !currentMemberId) return;
    await completeChore(household.id, choreId, currentMemberId, completedAt);
    const updatedChores = await getHouseholdChores(household.id);
    setChores(updatedChores);
    bumpRefreshKey();
  }, [household, currentMemberId, completeChore, getHouseholdChores, setChores, bumpRefreshKey]);

  const handleAssignChore = useCallback(async (choreId: string, memberIds?: string[], assignToAll?: boolean) => {
    if (!household) return;
    await assignChore(household.id, choreId, memberIds, assignToAll);
    setChores((prev) =>
      prev.map((c) => (c.id === choreId ? { ...c, assignedTo: memberIds, assignedToAll: assignToAll } : c))
    );
    bumpRefreshKey();
  }, [household, assignChore, setChores, bumpRefreshKey]);

  const handleDeleteChore = useCallback(async (choreId: string) => {
    if (!household) return;
    const success = await deleteChore(household.id, choreId);
    if (success) {
      setChores((prev) => prev.filter((c) => c.id !== choreId));
      bumpRefreshKey();
    }
  }, [household, deleteChore, setChores, bumpRefreshKey]);

  const handleGenerateInvite = useCallback(() => {
    if (!household) return Promise.resolve(null);
    return generateInvite(household.id);
  }, [household, generateInvite]);

  return { handleAddChore, handleSetChoreRates, handleCompleteChore, handleAssignChore, handleDeleteChore, handleGenerateInvite };
}
