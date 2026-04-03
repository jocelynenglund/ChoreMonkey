import { useState, useEffect, useCallback } from 'react';
import { useHouseholdStore } from '@/stores/householdStore';
import type { Household, Chore } from '@/types/household';

interface UseHouseholdDataResult {
  household: Household | null;
  chores: Chore[];
  isDataLoading: boolean;
  isRefreshing: boolean;
  refreshKey: number;
  refreshData: () => Promise<void>;
  setHousehold: React.Dispatch<React.SetStateAction<Household | null>>;
  setChores: React.Dispatch<React.SetStateAction<Chore[]>>;
  bumpRefreshKey: () => void;
}

export function useHouseholdData(householdId: string | undefined): UseHouseholdDataResult {
  const { getHousehold, getHouseholdChores, fetchHouseholdMembers } = useHouseholdStore();

  const [household, setHousehold] = useState<Household | null>(null);
  const [chores, setChores] = useState<Chore[]>([]);
  const [isDataLoading, setIsDataLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);

  const bumpRefreshKey = useCallback(() => setRefreshKey((k) => k + 1), []);

  const refreshData = useCallback(async () => {
    if (!householdId) return;
    setIsRefreshing(true);
    try {
      const [fetchedHousehold, fetchedChores] = await Promise.all([
        getHousehold(householdId),
        getHouseholdChores(householdId),
        fetchHouseholdMembers(householdId),
      ]);
      setHousehold(fetchedHousehold);
      setChores(fetchedChores);
      setRefreshKey((k) => k + 1);
    } finally {
      setIsRefreshing(false);
    }
  }, [householdId, getHousehold, getHouseholdChores, fetchHouseholdMembers]);

  useEffect(() => {
    const fetchData = async () => {
      if (!householdId) return;
      setIsDataLoading(true);
      const [fetchedHousehold, fetchedChores] = await Promise.all([
        getHousehold(householdId),
        getHouseholdChores(householdId),
        fetchHouseholdMembers(householdId),
      ]);
      setHousehold(fetchedHousehold);
      setChores(fetchedChores);
      setIsDataLoading(false);
    };
    fetchData();
  }, [householdId, getHousehold, getHouseholdChores, fetchHouseholdMembers]);

  return {
    household,
    chores,
    isDataLoading,
    isRefreshing,
    refreshKey,
    refreshData,
    setHousehold,
    setChores,
    bumpRefreshKey,
  };
}
