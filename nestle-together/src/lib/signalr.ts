import * as signalR from '@microsoft/signalr';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5207';

export type HouseholdEvent = 
  | { type: 'ChoreCompleted'; choreId: string; householdId: string; completedByMemberId: string; completedAt: string }
  | { type: 'ChoreCreated'; choreId: string; householdId: string; displayName: string; description: string }
  | { type: 'ChoreAssigned'; choreId: string; householdId: string; assignedToMemberIds: string[]; assignToAll: boolean }
  | { type: 'ChoreDeleted'; choreId: string; householdId: string; deletedByMemberId: string }
  | { type: 'MemberJoined'; memberId: string; householdId: string; nickname: string };

type EventHandler = (event: HouseholdEvent) => void;

class HouseholdConnection {
  private connection: signalR.HubConnection | null = null;
  private currentHouseholdId: string | null = null;
  private eventHandlers: Set<EventHandler> = new Set();
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 5;

  async connect(householdId: string): Promise<void> {
    // If already connected to this household, do nothing
    if (this.currentHouseholdId === householdId && this.connection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    // Disconnect from previous household if any
    await this.disconnect();

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/household`)
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 4s, 8s, 16s, then stop
          if (retryContext.previousRetryCount >= this.maxReconnectAttempts) {
            return null;
          }
          return Math.min(1000 * Math.pow(2, retryContext.previousRetryCount), 16000);
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Register event handlers
    this.registerEventHandlers();

    // Handle reconnection
    this.connection.onreconnected(async () => {
      console.log('[SignalR] Reconnected, rejoining household...');
      if (this.currentHouseholdId) {
        await this.connection?.invoke('JoinHousehold', this.currentHouseholdId);
      }
    });

    this.connection.onclose((error) => {
      console.log('[SignalR] Connection closed', error);
    });

    try {
      await this.connection.start();
      console.log('[SignalR] Connected');
      
      await this.connection.invoke('JoinHousehold', householdId);
      this.currentHouseholdId = householdId;
      console.log(`[SignalR] Joined household ${householdId}`);
    } catch (error) {
      console.error('[SignalR] Failed to connect:', error);
      throw error;
    }
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      if (this.currentHouseholdId && this.connection.state === signalR.HubConnectionState.Connected) {
        try {
          await this.connection.invoke('LeaveHousehold', this.currentHouseholdId);
        } catch (e) {
          // Ignore errors when leaving
        }
      }
      await this.connection.stop();
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

    const events = ['ChoreCompleted', 'ChoreCreated', 'ChoreAssigned', 'ChoreDeleted', 'MemberJoined'] as const;
    
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
