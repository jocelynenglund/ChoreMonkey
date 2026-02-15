import { RefreshCw, Wifi, WifiOff } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';
import type { ConnectionState } from '@/lib/signalr';

interface ConnectionStatusProps {
  connectionState: ConnectionState;
  onRefresh: () => void;
  onReconnect: () => void;
  isRefreshing?: boolean;
}

export function ConnectionStatus({ 
  connectionState, 
  onRefresh, 
  onReconnect,
  isRefreshing = false
}: ConnectionStatusProps) {
  const isConnected = connectionState === 'connected';
  const isConnecting = connectionState === 'connecting' || connectionState === 'reconnecting';

  if (isConnected) {
    // Show green dot for live updates
    return (
      <div 
        className="flex items-center gap-1.5 text-xs text-green-600"
        title="Live updates active"
      >
        <span className="relative flex h-2 w-2">
          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-75"></span>
          <span className="relative inline-flex rounded-full h-2 w-2 bg-green-500"></span>
        </span>
        <Wifi className="h-3 w-3" />
      </div>
    );
  }

  if (isConnecting) {
    // Show connecting state
    return (
      <div 
        className="flex items-center gap-1.5 text-xs text-amber-600"
        title="Connecting..."
      >
        <span className="relative flex h-2 w-2">
          <span className="animate-pulse inline-flex rounded-full h-2 w-2 bg-amber-500"></span>
        </span>
        <Wifi className="h-3 w-3 animate-pulse" />
      </div>
    );
  }

  // Disconnected - show refresh button
  return (
    <div className="flex items-center gap-2">
      <div 
        className="flex items-center gap-1 text-xs text-muted-foreground cursor-pointer hover:text-foreground transition-colors"
        onClick={onReconnect}
        title="Click to try reconnecting"
      >
        <WifiOff className="h-3 w-3" />
      </div>
      <Button
        variant="ghost"
        size="sm"
        className="h-7 px-2"
        onClick={onRefresh}
        disabled={isRefreshing}
        title="Refresh data"
      >
        <RefreshCw className={cn(
          "h-3.5 w-3.5",
          isRefreshing && "animate-spin"
        )} />
      </Button>
    </div>
  );
}
