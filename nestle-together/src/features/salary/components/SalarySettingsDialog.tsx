import { useState, useEffect } from 'react';
import { DollarSign, Settings2 } from 'lucide-react';
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
import { Switch } from '@/components/ui/switch';
import { useAppStore } from '@/features/store';
import * as salaryApi from '../api';
import type { SalarySettings } from '../types';

interface SalarySettingsDialogProps {
  householdId: string;
}

export function SalarySettingsDialog({ householdId }: SalarySettingsDialogProps) {
  const [open, setOpen] = useState(false);
  const [settings, setSettings] = useState<SalarySettings>({ enabled: false, baseAmount: 800 });
  const [newBaseAmount, setNewBaseAmount] = useState('800');
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const { isAdmin } = useAppStore();

  useEffect(() => {
    if (open) {
      loadSettings();
    }
  }, [open, householdId]);

  const loadSettings = async () => {
    const data = await salaryApi.fetchSalarySettings(householdId);
    setSettings(data);
    setNewBaseAmount(data.baseAmount.toString());
  };

  const handleToggleEnabled = async () => {
    setMessage(null);
    setIsLoading(true);

    try {
      if (!settings.enabled) {
        // Enable salary reports
        const success = await salaryApi.enableSalaryReports(householdId, {
          baseAmount: parseFloat(newBaseAmount) || 800,
        });
        if (success) {
          setSettings({ ...settings, enabled: true, baseAmount: parseFloat(newBaseAmount) || 800 });
          setMessage({ type: 'success', text: 'Salary reports enabled!' });
        } else {
          setMessage({ type: 'error', text: 'Failed to enable salary reports' });
        }
      } else {
        // Salary reports can't be disabled once enabled (business rule)
        setMessage({ type: 'error', text: 'Salary reports cannot be disabled once enabled' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to update settings' });
    }

    setIsLoading(false);
  };

  const handleUpdateBaseAmount = async () => {
    setMessage(null);
    const amount = parseFloat(newBaseAmount);

    if (isNaN(amount) || amount < 0) {
      setMessage({ type: 'error', text: 'Please enter a valid amount' });
      return;
    }

    setIsLoading(true);
    try {
      const success = await salaryApi.updateBaseSalary(householdId, { newBaseAmount: amount });
      if (success) {
        setSettings({ ...settings, baseAmount: amount });
        setMessage({ type: 'success', text: 'Base salary updated!' });
      } else {
        setMessage({ type: 'error', text: 'Failed to update base salary' });
      }
    } catch (error) {
      setMessage({ type: 'error', text: 'Failed to update base salary' });
    }
    setIsLoading(false);
  };

  if (!isAdmin) {
    return null;
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm" className="gap-2">
          <DollarSign className="w-4 h-4" />
          Salary Settings
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Settings2 className="w-5 h-5" />
            Salary Report Settings
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-6 mt-4">
          {/* Enable/Disable */}
          <div className="flex items-center justify-between p-4 rounded-lg bg-muted">
            <div>
              <Label className="text-sm font-medium">Enable Salary Reports</Label>
              <p className="text-xs text-muted-foreground">
                Track chore completion for allowance calculations
              </p>
            </div>
            <Switch
              checked={settings.enabled}
              onCheckedChange={handleToggleEnabled}
              disabled={isLoading || settings.enabled}
            />
          </div>

          {settings.enabled && (
            <>
              {/* Base Salary */}
              <div className="space-y-3">
                <h3 className="font-semibold flex items-center gap-2">
                  <DollarSign className="w-4 h-4" />
                  Base Salary Amount
                </h3>
                <p className="text-xs text-muted-foreground">
                  Starting amount before deductions for missed chores
                </p>

                <div className="flex gap-2">
                  <Input
                    type="number"
                    min="0"
                    step="10"
                    value={newBaseAmount}
                    onChange={(e) => setNewBaseAmount(e.target.value)}
                    className="flex-1"
                  />
                  <Button
                    onClick={handleUpdateBaseAmount}
                    disabled={isLoading || parseFloat(newBaseAmount) === settings.baseAmount}
                  >
                    {isLoading ? 'Saving...' : 'Save'}
                  </Button>
                </div>
              </div>

              {/* Info */}
              <div className="p-3 rounded-lg bg-blue-50 border border-blue-200 text-sm">
                <p className="text-blue-800">
                  <strong>How it works:</strong> Members start with {settings.baseAmount} kr.
                  When a required chore is missed, the deduction amount set on that chore
                  is subtracted from their salary for that period.
                </p>
              </div>
            </>
          )}

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
