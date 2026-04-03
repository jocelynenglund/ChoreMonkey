import { useState } from 'react';
import { Lock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useHouseholdStore } from '@/stores/householdStore';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

interface PinSettingsTabProps {
  householdId: string;
}

export function PinSettingsTab({ householdId }: PinSettingsTabProps) {
  const { currentPinCode } = useHouseholdStore();

  const [currentPin, setCurrentPin] = useState('');
  const [newAdminPin, setNewAdminPin] = useState('');
  const [confirmAdminPin, setConfirmAdminPin] = useState('');
  const [newMemberPin, setNewMemberPin] = useState('');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const handleChangeAdminPin = async () => {
    setMessage(null);
    if (newAdminPin.length !== 4) return setMessage({ type: 'error', text: 'New PIN must be 4 digits' });
    if (newAdminPin !== confirmAdminPin) return setMessage({ type: 'error', text: 'PINs do not match' });

    setIsLoading(true);
    try {
      const res = await fetch(`${API_BASE_URL}/api/households/${householdId}/admin-pin`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          currentPinCode: parseInt(currentPin || currentPinCode || '0', 10),
          newPinCode: parseInt(newAdminPin, 10),
        }),
      });
      if (res.ok) {
        setMessage({ type: 'success', text: 'Admin PIN changed! You\'ll need to log in again.' });
        setNewAdminPin(''); setConfirmAdminPin(''); setCurrentPin('');
      } else {
        setMessage({ type: 'error', text: 'Failed to change PIN. Check current PIN.' });
      }
    } catch {
      setMessage({ type: 'error', text: 'Failed to change PIN' });
    }
    setIsLoading(false);
  };

  const handleSetMemberPin = async () => {
    setMessage(null);
    if (newMemberPin.length !== 4) return setMessage({ type: 'error', text: 'Member PIN must be 4 digits' });

    setIsLoading(true);
    try {
      const res = await fetch(`${API_BASE_URL}/api/households/${householdId}/member-pin`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          adminPinCode: parseInt(currentPin || currentPinCode || '0', 10),
          memberPinCode: parseInt(newMemberPin, 10),
        }),
      });
      if (res.ok) {
        setMessage({ type: 'success', text: 'Member PIN updated!' });
        setNewMemberPin(''); setCurrentPin('');
      } else {
        setMessage({ type: 'error', text: 'Failed to set member PIN' });
      }
    } catch {
      setMessage({ type: 'error', text: 'Failed to set member PIN' });
    }
    setIsLoading(false);
  };

  return (
    <div className="space-y-8">
      {message && (
        <p className={`text-sm ${message.type === 'success' ? 'text-green-600' : 'text-destructive'}`}>
          {message.text}
        </p>
      )}

      {/* Current PIN (shared field) */}
      <div className="space-y-2">
        <Label htmlFor="currentPin" className="flex items-center gap-2">
          <Lock className="w-4 h-4" /> Current Admin PIN
        </Label>
        <Input
          id="currentPin"
          type="password"
          inputMode="numeric"
          maxLength={4}
          placeholder="••••"
          value={currentPin}
          onChange={(e) => setCurrentPin(e.target.value.replace(/\D/g, ''))}
          className="text-center tracking-[0.5em] font-mono max-w-[160px]"
        />
      </div>

      <hr />

      {/* Change Admin PIN */}
      <div className="space-y-3">
        <h3 className="font-semibold text-sm">Change Admin PIN</h3>
        <div className="space-y-2">
          <Label htmlFor="newAdminPin">New PIN</Label>
          <Input id="newAdminPin" type="password" inputMode="numeric" maxLength={4} placeholder="••••"
            value={newAdminPin} onChange={(e) => setNewAdminPin(e.target.value.replace(/\D/g, ''))}
            className="text-center tracking-[0.5em] font-mono max-w-[160px]"
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="confirmAdminPin">Confirm New PIN</Label>
          <Input id="confirmAdminPin" type="password" inputMode="numeric" maxLength={4} placeholder="••••"
            value={confirmAdminPin} onChange={(e) => setConfirmAdminPin(e.target.value.replace(/\D/g, ''))}
            className="text-center tracking-[0.5em] font-mono max-w-[160px]"
          />
        </div>
        <Button onClick={handleChangeAdminPin} disabled={isLoading} className="w-full sm:w-auto">
          {isLoading ? 'Changing...' : 'Change Admin PIN'}
        </Button>
      </div>

      <hr />

      {/* Set Member PIN */}
      <div className="space-y-3">
        <h3 className="font-semibold text-sm">Set Member PIN</h3>
        <p className="text-xs text-muted-foreground">
          Members can view and complete chores but cannot access admin features.
        </p>
        <div className="space-y-2">
          <Label htmlFor="newMemberPin">Member PIN</Label>
          <Input id="newMemberPin" type="password" inputMode="numeric" maxLength={4} placeholder="••••"
            value={newMemberPin} onChange={(e) => setNewMemberPin(e.target.value.replace(/\D/g, ''))}
            className="text-center tracking-[0.5em] font-mono max-w-[160px]"
          />
        </div>
        <Button onClick={handleSetMemberPin} disabled={isLoading} variant="outline" className="w-full sm:w-auto">
          {isLoading ? 'Setting...' : 'Set Member PIN'}
        </Button>
      </div>
    </div>
  );
}
