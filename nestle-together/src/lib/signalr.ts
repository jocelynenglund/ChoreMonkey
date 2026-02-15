import * as signalR from '@microsoft/signalr';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://itsybitsylist-api.azurewebsites.net';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface HouseholdEvents {
  onChoreCompleted?: (data: { choreId: string; completedByMemberId: string; completedAt: string }) => void;
  onChoreCreated?: (data: { choreId: string; displayName: string; description: string; isOptional: boolean }) => void;
  onChoreAssigned?: (data: { choreId: string; assignedToMemberIds?: string[]; assignToAll?: boolean }) => void;
  onMemberJoined?: (data: { memberId: string; nickname: string }) => void;
  onMemberRemoved?: (data: { memberId: string; nickname: string }) => void;
  onMemberStatusChanged?: (data: { memberId: string; status: string }) => void;
  onMemberNicknameChanged?: (data: { memberId: string; oldNickname: string; newNickname: string }) => void;
}

class HouseholdConnection {
  private connection: signalR.HubConnection | null = null;
  private currentHouseholdId: string | null = null;
  private stateListeners: Set<(state: ConnectionState) => void> = new Set();
  private eventHandlers: HouseholdEvents = {};
  private reconnectAttempts = 0;
  private maxReconnectAttempts = 3;

  getState(): ConnectionState {
    if (!this.connection) return 'disconnected';
    switch (this.connection.state) {
      case signalR.HubConnectionState.Connected:
        return 'connected';
      case signalR.HubConnectionState.Connecting:
        return 'connecting';
      case signalR.HubConnectionState.Reconnecting:
        return 'reconnecting';
      default:
        return 'disconnected';
    }
  }

  onStateChange(listener: (state: ConnectionState) => void): () => void {
    this.stateListeners.add(listener);
    return () => this.stateListeners.delete(listener);
  }

  private notifyStateChange() {
    const state = this.getState();
    this.stateListeners.forEach(listener => listener(state));
  }

  setEventHandlers(handlers: HouseholdEvents) {
    this.eventHandlers = handlers;
  }

  async connect(householdId: string): Promise<boolean> {
    // Already connected to this household
    if (this.currentHouseholdId === householdId && this.getState() === 'connected') {
      return true;
    }

    // Disconnect from previous household if any
    if (this.connection) {
      await this.disconnect();
    }

    this.currentHouseholdId = householdId;

    try {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE_URL}/hubs/household`)
        .withAutomaticReconnect({
          nextRetryDelayInMilliseconds: (retryContext) => {
            // Quick retries first, then back off
            if (retryContext.previousRetryCount < 3) {
              return 1000; // 1 second
            } else if (retryContext.previousRetryCount < 6) {
              return 5000; // 5 seconds
            } else {
              return null; // Stop reconnecting
            }
          }
        })
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      // Set up event handlers
      this.connection.on('ChoreCompleted', (data) => {
        this.eventHandlers.onChoreCompleted?.(data);
      });

      this.connection.on('ChoreCreated', (data) => {
        this.eventHandlers.onChoreCreated?.(data);
      });

      this.connection.on('ChoreAssigned', (data) => {
        this.eventHandlers.onChoreAssigned?.(data);
      });

      this.connection.on('MemberJoined', (data) => {
        this.eventHandlers.onMemberJoined?.(data);
      });

      this.connection.on('MemberRemoved', (data) => {
        this.eventHandlers.onMemberRemoved?.(data);
      });

      this.connection.on('MemberStatusChanged', (data) => {
        this.eventHandlers.onMemberStatusChanged?.(data);
      });

      this.connection.on('MemberNicknameChanged', (data) => {
        this.eventHandlers.onMemberNicknameChanged?.(data);
      });

      // State change handlers
      this.connection.onreconnecting(() => {
        this.notifyStateChange();
      });

      this.connection.onreconnected(async () => {
        // Rejoin the household group after reconnection
        if (this.currentHouseholdId) {
          try {
            await this.connection?.invoke('JoinHousehold', this.currentHouseholdId);
          } catch (error) {
            console.error('Failed to rejoin household after reconnection:', error);
          }
        }
        this.notifyStateChange();
      });

      this.connection.onclose(() => {
        this.notifyStateChange();
      });

      // Start the connection
      this.notifyStateChange();
      await this.connection.start();
      
      // Join the household group
      await this.connection.invoke('JoinHousehold', householdId);
      
      this.reconnectAttempts = 0;
      this.notifyStateChange();
      return true;
    } catch (error) {
      console.error('SignalR connection failed:', error);
      this.reconnectAttempts++;
      this.notifyStateChange();
      
      // Don't retry automatically if we've hit the max
      if (this.reconnectAttempts >= this.maxReconnectAttempts) {
        console.log('Max reconnection attempts reached. SignalR disabled.');
      }
      
      return false;
    }
  }

  async disconnect() {
    if (this.connection) {
      try {
        if (this.currentHouseholdId && this.connection.state === signalR.HubConnectionState.Connected) {
          await this.connection.invoke('LeaveHousehold', this.currentHouseholdId);
        }
        await this.connection.stop();
      } catch (error) {
        console.error('Error disconnecting:', error);
      }
      this.connection = null;
      this.currentHouseholdId = null;
      this.notifyStateChange();
    }
  }

  // Check if SignalR is supported (WebSockets available)
  isSupported(): boolean {
    return typeof WebSocket !== 'undefined';
  }
}

// Singleton instance
export const householdConnection = new HouseholdConnection();
