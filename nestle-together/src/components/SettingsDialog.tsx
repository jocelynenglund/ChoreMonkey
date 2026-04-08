import { useState, useEffect } from 'react';
import { Settings, Link, Check } from 'lucide-react';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useHouseholdStore } from '@/stores/householdStore';
import { setHouseholdSlug } from '@/features/household/api';

interface SettingsDialogProps {
  householdId: string;
  currentSlug?: string;
  onSlugChanged?: (slug: string) => void;
}

export function SettingsDialog({ householdId, currentSlug, onSlugChanged }: SettingsDialogProps) {
  const [open, setOpen] = useState(false);
  const [slugInput, setSlugInput] = useState(currentSlug || '');
  const [slugSaving, setSlugSaving] = useState(false);
  const [slugMessage, setSlugMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  useEffect(() => {
    if (currentSlug) setSlugInput(currentSlug);
  }, [currentSlug]);

  const { isAdmin, currentPinCode } = useHouseholdStore();

  if (!isAdmin) return null;

  const handleSaveSlug = async () => {
    setSlugMessage(null);
    if (!slugInput || slugInput.length < 3) {
      setSlugMessage({ type: 'error', text: 'Slug must be at least 3 characters.' });
      return;
    }
    setSlugSaving(true);
    try {
      const pin = parseInt(currentPinCode || '0', 10);
      await setHouseholdSlug(householdId, slugInput, pin);
      setSlugMessage({ type: 'success', text: 'Saved!' });
      onSlugChanged?.(slugInput);
    } catch (err: unknown) {
      const raw = err instanceof Error ? err.message : '';
      const msg = /taken|conflict|already|exist/i.test(raw)
        ? 'That URL is already taken, please try another.'
        : raw || 'Failed to save slug';
      setSlugMessage({ type: 'error', text: msg });
    }
    setSlugSaving(false);
  };

  return (
    <Sheet open={open} onOpenChange={setOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" aria-label="Settings" className="text-muted-foreground hover:text-foreground">
          <Settings className="w-5 h-5" />
        </Button>
      </SheetTrigger>
      <SheetContent side="right" className="w-full sm:max-w-sm overflow-y-auto">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            <Settings className="w-4 h-4" />
            Settings
          </SheetTitle>
        </SheetHeader>

        <div className="space-y-6 mt-6">
          {/* Household URL */}
          <div className="space-y-3">
            <h3 className="font-semibold flex items-center gap-2 text-sm">
              <Link className="w-4 h-4" />
              Household URL
            </h3>
            <p className="text-xs text-muted-foreground">
              Set a vanity URL for your household. Letters, numbers, and hyphens only.
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
                  <span className="font-mono text-foreground">choremonkey.itsybit.se/h/{slugInput}</span>
                </p>
              )}
            </div>
            <Button
              onClick={handleSaveSlug}
              disabled={slugSaving || slugInput === currentSlug}
              variant="outline"
              className="w-full"
            >
              {slugSaving ? 'Saving...' : slugMessage?.type === 'success' ? <><Check className="w-4 h-4 mr-1" /> Saved</> : 'Save URL'}
            </Button>
            {slugMessage?.type === 'error' && (
              <p className="text-sm text-destructive">{slugMessage.text}</p>
            )}
          </div>

          <hr />

          <p className="text-xs text-muted-foreground">
            To change PINs, go to the <strong>Admin Panel</strong> → Settings tab.
          </p>
        </div>
      </SheetContent>
    </Sheet>
  );
}
