import { useEffect, useCallback } from 'react';
import { householdConnection, type HouseholdEvent } from '@/lib/signalr';
import { useHouseholdStore } from '@/stores/householdStore';

/**
 * Hook that connects to SignalR for real-time household updates.
 * Call this in the HouseholdDashboard component.
 */
export function useHouseholdRealtime(householdId: string | null) {
  const fetchHouseholdChores = useHouseholdStore((s) => s.fetchHouseholdChores);
  const fetchHouseholdMembers = useHouseholdStore((s) => s.fetchHouseholdMembers);
  const currentMemberId = useHouseholdStore((s) => s.currentMemberId);
  const fetchMyChores = useHouseholdStore((s) => s.fetchMyChores);

  // Handle incoming events
  const handleEvent = useCallback(
    (event: HouseholdEvent) => {
      if (!householdId) return;

      console.log('[Realtime] Event received:', event.type);

      switch (event.type) {
        case 'ChoreCompleted':
        case 'ChoreCreated':
        case 'ChoreAssigned':
        case 'ChoreDeleted':
          // Refresh chore list
          fetchHouseholdChores(householdId);
          // Also refresh my chores if we have a current member
          if (currentMemberId) {
            fetchMyChores(householdId, currentMemberId);
          }
          break;

        case 'MemberJoined':
          // Refresh member list
          fetchHouseholdMembers(householdId);
          break;
      }
    },
    [householdId, currentMemberId, fetchHouseholdChores, fetchHouseholdMembers, fetchMyChores]
  );

  // Connect to SignalR when household changes
  useEffect(() => {
    if (!householdId) {
      householdConnection.disconnect();
      return;
    }

    // Connect and subscribe to events
    householdConnection.connect(householdId).catch((error) => {
      console.error('[Realtime] Failed to connect:', error);
    });

    const unsubscribe = householdConnection.subscribe(handleEvent);

    return () => {
      unsubscribe();
    };
  }, [householdId, handleEvent]);

  // Disconnect on unmount
  useEffect(() => {
    return () => {
      householdConnection.disconnect();
    };
  }, []);

  return {
    isConnected: householdConnection.isConnected,
  };
}
