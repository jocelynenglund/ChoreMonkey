import * as signalR from '@microsoft/signalr';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5207';

export type HouseholdEvent = 
  | { type: 'ChoreCompleted'; choreId: string; householdId: string; completedByMemberId: string; completedAt: string }
  | { type: 'ChoreCreated'; choreId: string; householdId: string; displayName: string; description: string }
  | { type: 'ChoreAssigned'; choreId: string; householdId: string; assignedToMemberIds: string[]; assignToAll: boolean }
  | { type: 'ChoreDeleted'; choreId: string; householdId: string; deletedByMemberId: string }
  | { type: 'MemberJoined'; memberId: string; householdId: string; nickname: string }
  | { type: 'MemberNicknameChanged'; memberId: string; householdId: string; oldNickname: string; newNickname: string }
  | { type: 'MemberStatusChanged'; memberId: string; householdId: string; status: string };

type EventHandler = (event: HouseholdEvent) => void;

class HouseholdConnection {
  private connection: signalR.HubConnection | null = null;
  private currentHouseholdId: string | null = null;
  private eventHandlers: Set<EventHandler> = new Set();
  private maxReconnectAttempts = 5;
  private isConnecting = false;
  private pendingHouseholdId: string | null = null;

  async connect(householdId: string): Promise<void> {
    // If already connected to this household, do nothing
    if (this.currentHouseholdId === householdId && this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    // If already connecting, just update the pending household
    if (this.isConnecting) {
      this.pendingHouseholdId = householdId;
      console.log('[SignalR] Already connecting, queued household:', householdId);
      return;
    }

    this.isConnecting = true;
    this.pendingHouseholdId = null;

    try {
      // Disconnect from previous household if any
      await this.disconnect();

      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_URL}/hubs/household`)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
              return null;
            }
            return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
          }
        })
        .configureLogging(signalR.LogLevel.Warning) // Reduce noise
        .build();

      this.registerEventHandlers();

      this.connection.onreconnected(async () => {
        console.log('[SignalR] Reconnected, rejoining household...');
        if (this.currentHouseholdId) {
          try {
            await this.connection?.invoke('JoinHousehold', this.currentHouseholdId);
          } catch (e) {
            console.error('[SignalR] Failed to rejoin household:', e);
          }
        }
      });

      this.connection.onclose((error) => {
        if (error) {
          console.log('[SignalR] Connection closed with error:', error);
        }
      });

      await this.connection.start();
      console.log('[SignalR] Connected');
      
      await this.connection.invoke('JoinHousehold', householdId);
      this.currentHouseholdId = householdId;
      console.log(`[SignalR] Joined household ${householdId}`);

    } catch (error) {
      // Only log if it's not a "stopped during negotiation" error (which is expected on quick unmount)
      const errorMsg = String(error);
      if (!errorMsg.includes('stopped during negotiation') && !errorMsg.includes('stop() was called')) {
        console.error('[SignalR] Failed to connect:', error);
      }
    } finally {
      this.isConnecting = false;
      
      // If there was a pending connection request, handle it
      if (this.pendingHouseholdId && this.pendingHouseholdId !== householdId) {
        const pending = this.pendingHouseholdId;
        this.pendingHouseholdId = null;
        await this.connect(pending);
      }
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      if (this.currentHouseholdId && this.connection.state === signalR.HubConnectionState.Connected) {
        try {
          await this.connection.invoke('LeaveHousehold', this.currentHouseholdId);
        } catch {
          // Ignore errors when leaving
        }
      }
      try {
        await this.connection.stop();
      } catch {
        // Ignore stop errors
      }
      this.connection = null;
      this.currentHouseholdId = null;
    }
  }

  subscribe(handler: EventHandler): () => void {
    this.eventHandlers.add(handler);
    return () => this.eventHandlers.delete(handler);
  }

  private registerEventHandlers(): void {
    if (!this.connection) return;

    const events = ['ChoreCompleted', 'ChoreCreated', 'ChoreAssigned', 'ChoreDeleted', 'MemberJoined', 'MemberNicknameChanged', 'MemberStatusChanged'] as const;
    
    for (const eventType of events) {
      this.connection.on(eventType, (data: Record<string, unknown>) => {
        const event = { type: eventType, ...data } as HouseholdEvent;
        console.log(`[SignalR] Received ${eventType}:`, data);
        this.eventHandlers.forEach(handler => handler(event));
      });
    }
  }

  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected;
  }
}

// Singleton instance
export const householdConnection = new HouseholdConnection();
