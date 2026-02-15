import { useEffect, useState, useCallback } from 'react';
import { householdConnection, ConnectionState, HouseholdEvents } from '@/lib/signalr';

interface UseHouseholdRealtimeOptions {
  householdId: string | null;
  onRefreshNeeded?: () => void;
  enabled?: boolean;
}

export function useHouseholdRealtime({ 
  householdId, 
  onRefreshNeeded, 
  enabled = true 
}: UseHouseholdRealtimeOptions) {
  const [connectionState, setConnectionState] = useState<ConnectionState>('disconnected');

  const refresh = useCallback(() => {
    onRefreshNeeded?.();
  }, [onRefreshNeeded]);

  useEffect(() => {
    if (!enabled || !householdId) {
      return;
    }

    // Set up event handlers that trigger refresh
    const handlers: HouseholdEvents = {
      onChoreCompleted: () => refresh(),
      onChoreCreated: () => refresh(),
      onChoreAssigned: () => refresh(),
      onChoreDeleted: () => refresh(),
      onMemberJoined: () => refresh(),
      onMemberRemoved: () => refresh(),
      onMemberStatusChanged: () => refresh(),
      onMemberNicknameChanged: () => refresh(),
    };

    householdConnection.setEventHandlers(handlers);

    // Subscribe to state changes
    const unsubscribe = householdConnection.onStateChange(setConnectionState);

    // Attempt to connect
    householdConnection.connect(householdId);

    // Cleanup on unmount
    return () => {
      unsubscribe();
      householdConnection.disconnect();
    };
  }, [householdId, refresh, enabled]);

  const reconnect = useCallback(() => {
    if (householdId) {
      householdConnection.connect(householdId);
    }
  }, [householdId]);

  return {
    connectionState,
    isConnected: connectionState === 'connected',
    isConnecting: connectionState === 'connecting' || connectionState === 'reconnecting',
    reconnect,
  };
}
