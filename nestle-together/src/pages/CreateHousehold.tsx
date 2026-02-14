import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { ArrowLeft, Home, Lock, Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useHouseholdStore } from '@/stores/householdStore';

export default function CreateHousehold() {
  const navigate = useNavigate();
  const createHousehold = useHouseholdStore((s) => s.createHousehold);

  const [name, setName] = useState('');
  const [pin, setPin] = useState('');
  const [confirmPin, setConfirmPin] = useState('');
  const [useSeparateMemberPin, setUseSeparateMemberPin] = useState(false);
  const [memberPin, setMemberPin] = useState('');
  const [step, setStep] = useState(1);
  const [error, setError] = useState('');

  const handleNext = () => {
    if (!name.trim()) {
      setError('Please enter a household name');
      return;
    }
    setError('');
    setStep(2);
  };

  const handleCreate = async () => {
    if (pin.length !== 4) {
      setError('Admin PIN must be 4 digits');
      return;
    }
    if (pin !== confirmPin) {
      setError('Admin PINs do not match');
      return;
    }
    if (useSeparateMemberPin && memberPin.length !== 4) {
      setError('Member PIN must be 4 digits');
      return;
    }

    const household = await createHousehold(
      name.trim(), 
      pin, 
      'Admin',
      useSeparateMemberPin ? memberPin : undefined
    );
    if (household) {
      navigate(`/household/${household.id}`);
    }
  };

  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4 py-12">
      <div className="w-full max-w-md animate-slide-up">
        {/* Back button */}
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground mb-8 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Back
        </Link>

        {/* Progress indicator */}
        <div className="flex items-center gap-2 mb-8">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-full bg-primary text-primary-foreground flex items-center justify-center text-sm font-bold">
              {step > 1 ? <Check className="w-4 h-4" /> : '1'}
            </div>
            <span className={step === 1 ? 'font-medium' : 'text-muted-foreground'}>
              Name
            </span>
          </div>
          <div className="flex-1 h-0.5 bg-border mx-2">
            <div
              className="h-full bg-primary transition-all duration-300"
              style={{ width: step > 1 ? '100%' : '0%' }}
            />
          </div>
          <div className="flex items-center gap-2">
            <div
              className={`w-8 h-8 rounded-full flex items-center justify-center text-sm font-bold ${
                step === 2
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted text-muted-foreground'
              }`}
            >
              2
            </div>
            <span className={step === 2 ? 'font-medium' : 'text-muted-foreground'}>
              PIN
            </span>
          </div>
        </div>

        <div className="card-elevated p-8">
          {step === 1 ? (
            <>
              <div className="w-14 h-14 rounded-2xl gradient-monkey flex items-center justify-center mb-6">
                <span className="text-2xl">🏠</span>
              </div>
              <h1 className="text-2xl font-bold mb-2">Create Your Household</h1>
              <p className="text-muted-foreground mb-6">
                Give your household a name that your family will recognize.
              </p>

              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="name">Household Name</Label>
                  <Input
                    id="name"
                    placeholder="e.g., The Smith Family"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    className="h-12 text-base"
                    autoFocus
                  />
                </div>

                {error && (
                  <p className="text-sm text-destructive">{error}</p>
                )}

                <Button
                  onClick={handleNext}
                  className="w-full h-12 text-base"
                >
                  Continue
                </Button>
              </div>
            </>
          ) : (
            <>
              <div className="w-14 h-14 rounded-2xl bg-accent/20 flex items-center justify-center mb-6">
                <Lock className="w-7 h-7 text-accent" />
              </div>
              <h1 className="text-2xl font-bold mb-2">Set Admin PIN</h1>
              <p className="text-muted-foreground mb-6">
                This PIN gives full access including deleting chores.
              </p>

              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="pin">PIN Code</Label>
                  <Input
                    id="pin"
                    type="password"
                    inputMode="numeric"
                    maxLength={4}
                    placeholder="••••"
                    value={pin}
                    onChange={(e) => setPin(e.target.value.replace(/\D/g, ''))}
                    className="h-12 text-base text-center tracking-[0.5em] font-mono"
                    autoFocus
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="confirmPin">Confirm Admin PIN</Label>
                  <Input
                    id="confirmPin"
                    type="password"
                    inputMode="numeric"
                    maxLength={4}
                    placeholder="••••"
                    value={confirmPin}
                    onChange={(e) =>
                      setConfirmPin(e.target.value.replace(/\D/g, ''))
                    }
                    className="h-12 text-base text-center tracking-[0.5em] font-mono"
                  />
                </div>

                {/* Separate Member PIN option */}
                <div className="pt-2 border-t">
                  <label className="flex items-center gap-3 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={useSeparateMemberPin}
                      onChange={(e) => setUseSeparateMemberPin(e.target.checked)}
                      className="w-4 h-4 rounded border-gray-300"
                    />
                    <span className="text-sm">
                      Set a separate PIN for family members
                    </span>
                  </label>
                  <p className="text-xs text-muted-foreground mt-1 ml-7">
                    Members can view and complete chores but not delete them
                  </p>
                </div>

                {useSeparateMemberPin && (
                  <div className="space-y-2">
                    <Label htmlFor="memberPin">Member PIN</Label>
                    <Input
                      id="memberPin"
                      type="password"
                      inputMode="numeric"
                      maxLength={4}
                      placeholder="••••"
                      value={memberPin}
                      onChange={(e) => setMemberPin(e.target.value.replace(/\D/g, ''))}
                      className="h-12 text-base text-center tracking-[0.5em] font-mono"
                    />
                  </div>
                )}

                {error && (
                  <p className="text-sm text-destructive">{error}</p>
                )}

                <div className="flex gap-3">
                  <Button
                    variant="outline"
                    onClick={() => setStep(1)}
                    className="flex-1 h-12"
                  >
                    Back
                  </Button>
                  <Button
                    onClick={handleCreate}
                    className="flex-1 h-12 text-base"
                  >
                    Create Household
                  </Button>
                </div>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
