import { useEffect, useCallback, useState } from 'react';
import { householdConnection, type HouseholdEvent } from '@/lib/signalr';
import { useHouseholdStore } from '@/stores/householdStore';

/**
 * Hook that connects to SignalR for real-time household updates.
 * SignalR is optional - app works without it, just no live updates.
 */
export function useHouseholdRealtime(householdId: string | null) {
  const [isConnected, setIsConnected] = useState(false);
  const [connectionFailed, setConnectionFailed] = useState(false);
  
  const fetchHouseholdChores = useHouseholdStore((s) => s.fetchHouseholdChores);
  const fetchHouseholdMembers = useHouseholdStore((s) => s.fetchHouseholdMembers);
  const currentMemberId = useHouseholdStore((s) => s.currentMemberId);
  const fetchMyChores = useHouseholdStore((s) => s.fetchMyChores);

  // Manual refresh function
  const refresh = useCallback(() => {
    if (!householdId) return;
    fetchHouseholdChores(householdId);
    fetchHouseholdMembers(householdId);
    if (currentMemberId) {
      fetchMyChores(householdId, currentMemberId);
    }
  }, [householdId, currentMemberId, fetchHouseholdChores, fetchHouseholdMembers, fetchMyChores]);

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
          fetchHouseholdChores(householdId);
          if (currentMemberId) {
            fetchMyChores(householdId, currentMemberId);
          }
          break;

        case 'MemberJoined':
        case 'MemberNicknameChanged':
        case 'MemberStatusChanged':
          fetchHouseholdMembers(householdId);
          break;
      }
    },
    [householdId, currentMemberId, fetchHouseholdChores, fetchHouseholdMembers, fetchMyChores]
  );

  // Connect to SignalR when household changes (non-blocking)
  useEffect(() => {
    if (!householdId) {
      householdConnection.disconnect();
      setIsConnected(false);
      return;
    }

    let cancelled = false;

    // Try to connect, but don't block the app if it fails
    householdConnection.connect(householdId)
      .then(() => {
        if (!cancelled) {
          setIsConnected(true);
          setConnectionFailed(false);
        }
      })
      .catch(() => {
        if (!cancelled) {
          setConnectionFailed(true);
          setIsConnected(false);
          // App continues to work without real-time updates
        }
      });

    const unsubscribe = householdConnection.subscribe(handleEvent);

    return () => {
      cancelled = true;
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
    isConnected,
    connectionFailed,
    refresh,
  };
}
