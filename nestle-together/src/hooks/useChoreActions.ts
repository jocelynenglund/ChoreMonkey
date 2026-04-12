import { useCallback } from 'react';
import { useHouseholdStore } from '@/stores/householdStore';
import { setChoreRates } from '@/features/salary/api';
import type { Chore, ChoreFrequency } from '@/types/household';

interface UseChoreActionsProps {
  householdId: string;
  currentMemberId: string | undefined;
  setChores: React.Dispatch<React.SetStateAction<Chore[]>>;
  bumpRefreshKey: () => void;
  getHouseholdChores: (id: string) => Promise<Chore[]>;
}

export function useChoreActions({
  householdId,
  currentMemberId,
  setChores,
  bumpRefreshKey,
  getHouseholdChores,
}: UseChoreActionsProps) {
  const { addChore, completeChore, assignChore, deleteChore, generateInvite } = useHouseholdStore();

  const handleAddChore = useCallback(
    async (
      displayName: string,
      description: string,
      frequency?: ChoreFrequency,
      isOptional?: boolean,
      startDate?: Date,
      isRequired?: boolean,
      missedDeduction?: number,
    ) => {
      const newChore = await addChore(
        householdId,
        displayName,
        description,
        frequency,
        isOptional,
        startDate,
        isRequired,
        missedDeduction,
      );
      if (newChore) {
        setChores((prev) => [...prev, newChore]);
        bumpRefreshKey();
        return { id: newChore.id };
      }
      return null;
    },
    [householdId, addChore, setChores, bumpRefreshKey],
  );

  const handleSetChoreRates = useCallback(
    async (choreId: string, deductionRate: number, bonusRate: number) => {
      await setChoreRates(householdId, choreId, { deductionRate, bonusRate });
    },
    [householdId],
  );

  const handleCompleteChore = useCallback(
    async (choreId: string, completedAt?: Date) => {
      if (!currentMemberId) return;
      await completeChore(householdId, choreId, currentMemberId, completedAt);
      const updatedChores = await getHouseholdChores(householdId);
      setChores(updatedChores);
      bumpRefreshKey();
    },
    [householdId, currentMemberId, completeChore, getHouseholdChores, setChores, bumpRefreshKey],
  );

  const handleAssignChore = useCallback(
    async (choreId: string, memberIds?: string[], assignToAll?: boolean) => {
      await assignChore(householdId, choreId, memberIds, assignToAll);
      setChores((prev) =>
        prev.map((c) => (c.id === choreId ? { ...c, assignedTo: memberIds, assignedToAll: assignToAll } : c)),
      );
      bumpRefreshKey();
    },
    [householdId, assignChore, setChores, bumpRefreshKey],
  );

  const handleDeleteChore = useCallback(
    async (choreId: string) => {
      const success = await deleteChore(householdId, choreId);
      if (success) {
        setChores((prev) => prev.filter((c) => c.id !== choreId));
        bumpRefreshKey();
      }
    },
    [householdId, deleteChore, setChores, bumpRefreshKey],
  );

  const handleGenerateInvite = useCallback(
    () => generateInvite(householdId),
    [householdId, generateInvite],
  );

  return {
    handleAddChore,
    handleSetChoreRates,
    handleCompleteChore,
    handleAssignChore,
    handleDeleteChore,
    handleGenerateInvite,
  };
}
