import { useState, useEffect } from 'react';
import { Settings, Lock, Shield, Wallet, Link, Check, Copy } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useHouseholdStore } from '@/stores/householdStore';
import { setHouseholdSlug } from '@/features/household/api';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

interface SettingsDialogProps {
  householdId: string;
  currentSlug?: string;
  onManageSalaries?: () => void;
  onSlugChanged?: (slug: string) => void;
}

export function SettingsDialog({ householdId, currentSlug, onManageSalaries, onSlugChanged }: SettingsDialogProps) {
  const [open, setOpen] = useState(false);
  const [currentPin, setCurrentPin] = useState('');
  const [newAdminPin, setNewAdminPin] = useState('');
  const [confirmAdminPin, setConfirmAdminPin] = useState('');
  const [newMemberPin, setNewMemberPin] = useState('');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  // Slug state
  const [slugInput, setSlugInput] = useState(currentSlug || '');
  const [slugSaving, setSlugSaving] = useState(false);
  const [slugMessage, setSlugMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    if (currentSlug) setSlugInput(currentSlug);
  }, [currentSlug]);

  const { isAdmin, currentPinCode } = useHouseholdStore();

  const handleChangeAdminPin = async () => {
    setMessage(null);
    
    if (newAdminPin.length !== 4) {
      setMessage({ type: 'error', text: 'New PIN must be 4 digits' });
      return;
    }
    if (newAdminPin !== confirmAdminPin) {
      setMessage({ type: 'error', text: 'PINs do not match' });
      return;
    }

    setIsLoading(true);
    try {
      const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/admin-pin`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          currentPinCode: parseInt(currentPin || currentPinCode || '0', 10),
          newPinCode: parseInt(newAdminPin, 10),
        }),
      });

      if (response.ok) {
        setMessage({ type: 'success', text: 'Admin PIN changed! Please log in again.' });
        setNewAdminPin('');
        setConfirmAdminPin('');
        setCurrentPin('');
        // Could auto-logout here
      } else {
        setMessage({ type: 'error', text: 'Failed to change PIN. Check current PIN.' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to change PIN' });
    }
    setIsLoading(false);
  };

  const handleSetMemberPin = async () => {
    setMessage(null);
    
    if (newMemberPin.length !== 4) {
      setMessage({ type: 'error', text: 'Member PIN must be 4 digits' });
      return;
    }

    setIsLoading(true);
    try {
      const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/member-pin`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          adminPinCode: parseInt(currentPin || currentPinCode || '0', 10),
          memberPinCode: parseInt(newMemberPin, 10),
        }),
      });

      if (response.ok) {
        setMessage({ type: 'success', text: 'Member PIN updated!' });
        setNewMemberPin('');
        setCurrentPin('');
      } else {
        setMessage({ type: 'error', text: 'Failed to set member PIN' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to set member PIN' });
    }
    setIsLoading(false);
  };

  if (!isAdmin) {
    return null; // Only admins can access settings
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="ghost" size="icon" className="text-muted-foreground hover:text-foreground">
          <Settings className="w-5 h-5" />
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Shield className="w-5 h-5" />
            Admin Settings
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-6 mt-4">
          {/* Admin Panel */}
          {onManageSalaries && (
            <div className="space-y-3">
              <h3 className="font-semibold flex items-center gap-2">
                <Wallet className="w-4 h-4" />
                Chores & Salaries
              </h3>
              <p className="text-xs text-muted-foreground">
                Manage chores, set allowances, and close monthly periods.
              </p>
              <Button 
                onClick={() => {
                  setOpen(false);
                  onManageSalaries();
                }}
                variant="outline"
                className="w-full"
              >
                Open Admin Panel
              </Button>
            </div>
          )}

          {onManageSalaries && <hr />}

          {/* Household URL */}
          <div className="space-y-3">
            <h3 className="font-semibold flex items-center gap-2">
              <Link className="w-4 h-4" />
              Household URL
            </h3>
            <p className="text-xs text-muted-foreground">
              Set a custom vanity URL for your household. Letters, numbers, and hyphens only.
            </p>

            <div className="space-y-2">
              <Label htmlFor="slugInput">Slug</Label>
              <Input
                id="slugInput"
                type="text"
                placeholder="my-family"
                maxLength={30}
                value={slugInput}
                onChange={(e) => setSlugInput(e.target.value.toLowerCase().replace(/[^a-z0-9-]/g, ''))}
                className="font-mono"
              />
              {slugInput && (
                <p className="text-xs text-muted-foreground">
                  Preview: <span className="font-mono text-foreground">choremonkey.itsybit.se/h/{slugInput}</span>
                </p>
              )}
            </div>

            <Button
              onClick={async () => {
                setSlugMessage(null);
                if (!slugInput || slugInput.length < 3) {
                  setSlugMessage({ type: 'error', text: 'Slug must be at least 3 characters.' });
                  return;
                }
                setSlugSaving(true);
                try {
                  const pin = parseInt(currentPinCode || '0', 10);
                  await setHouseholdSlug(householdId, slugInput, pin);
                  setSlugMessage({ type: 'success', text: 'Household URL saved!' });
                  onSlugChanged?.(slugInput);
                } catch (err: unknown) {
                  const msg = err instanceof Error ? err.message : 'Failed to save slug';
                  setSlugMessage({ type: 'error', text: msg });
                }
                setSlugSaving(false);
              }}
              disabled={slugSaving || slugInput === currentSlug}
              variant="outline"
              className="w-full"
            >
              {slugSaving ? 'Saving...' : 'Save Household URL'}
            </Button>

            {slugMessage && (
              <p className={`text-sm ${slugMessage.type === 'success' ? 'text-green-600' : 'text-destructive'}`}>
                {slugMessage.text}
              </p>
            )}
          </div>

          <hr />

          {/* Change Admin PIN */}
          <div className="space-y-3">
            <h3 className="font-semibold flex items-center gap-2">
              <Lock className="w-4 h-4" />
              Change Admin PIN
            </h3>
            
            <div className="space-y-2">
              <Label htmlFor="currentPin">Current PIN</Label>
              <Input
                id="currentPin"
                type="password"
                inputMode="numeric"
                maxLength={4}
                placeholder="••••"
                value={currentPin}
                onChange={(e) => setCurrentPin(e.target.value.replace(/\D/g, ''))}
                className="text-center tracking-[0.5em] font-mono"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="newAdminPin">New Admin PIN</Label>
              <Input
                id="newAdminPin"
                type="password"
                inputMode="numeric"
                maxLength={4}
                placeholder="••••"
                value={newAdminPin}
                onChange={(e) => setNewAdminPin(e.target.value.replace(/\D/g, ''))}
                className="text-center tracking-[0.5em] font-mono"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="confirmAdminPin">Confirm New PIN</Label>
              <Input
                id="confirmAdminPin"
                type="password"
                inputMode="numeric"
                maxLength={4}
                placeholder="••••"
                value={confirmAdminPin}
                onChange={(e) => setConfirmAdminPin(e.target.value.replace(/\D/g, ''))}
                className="text-center tracking-[0.5em] font-mono"
              />
            </div>

            <Button 
              onClick={handleChangeAdminPin} 
              disabled={isLoading}
              className="w-full"
            >
              {isLoading ? 'Changing...' : 'Change Admin PIN'}
            </Button>
          </div>

          <hr />

          {/* Set Member PIN */}
          <div className="space-y-3">
            <h3 className="font-semibold flex items-center gap-2">
              <Lock className="w-4 h-4" />
              Set Member PIN
            </h3>
            <p className="text-xs text-muted-foreground">
              Members can view and complete chores but cannot delete them.
            </p>

            <div className="space-y-2">
              <Label htmlFor="newMemberPin">Member PIN</Label>
              <Input
                id="newMemberPin"
                type="password"
                inputMode="numeric"
                maxLength={4}
                placeholder="••••"
                value={newMemberPin}
                onChange={(e) => setNewMemberPin(e.target.value.replace(/\D/g, ''))}
                className="text-center tracking-[0.5em] font-mono"
              />
            </div>

            <Button 
              onClick={handleSetMemberPin} 
              disabled={isLoading}
              variant="outline"
              className="w-full"
            >
              {isLoading ? 'Setting...' : 'Set Member PIN'}
            </Button>
          </div>

          {/* Message */}
          {message && (
            <p className={`text-sm ${message.type === 'success' ? 'text-green-600' : 'text-destructive'}`}>
              {message.text}
            </p>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
