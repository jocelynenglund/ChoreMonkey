import { useState, useEffect } from 'react';
import { useNavigate, useParams, Link } from 'react-router-dom';
import { ArrowLeft, Lock, Home } from 'lucide-react';
import { PinInput } from '@/components/PinInput';
import { MemberSelector } from '@/components/MemberSelector';
import { useHouseholdStore } from '@/stores/householdStore';
import type { Household, Member } from '@/types/household';

export default function AccessHousehold() {
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const [household, setHousehold] = useState<Household | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const {
    getHousehold,
    getHouseholdMembers,
    fetchHouseholdMembers,
    accessHousehold,
    setCurrentMember,
  } = useHouseholdStore();

  const [members, setMembers] = useState<Member[]>([]);

  const [selectedMemberId, setSelectedMemberId] = useState<string | null>(null);
  const [error, setError] = useState(false);
  const [isVerifying, setIsVerifying] = useState(false);

  useEffect(() => {
    const fetchData = async () => {
      if (!id) {
        setIsLoading(false);
        return;
      }
      setIsLoading(true);
      const [fetchedHousehold, fetchedMembers] = await Promise.all([
        getHousehold(id),
        fetchHouseholdMembers(id),
      ]);
      setHousehold(fetchedHousehold);
      setMembers(fetchedMembers);
      setIsLoading(false);
    };

    fetchData();
  }, [id, getHousehold, fetchHouseholdMembers]);

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 rounded-xl gradient-monkey flex items-center justify-center mx-auto mb-4 animate-pulse">
            <span className="text-2xl">🐵</span>
          </div>
          <p className="text-muted-foreground">Loading...</p>
        </div>
      </div>
    );
  }

  if (!household) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center px-4">
        <div className="text-center animate-fade-in">
          <div className="w-16 h-16 rounded-2xl bg-muted flex items-center justify-center mx-auto mb-4">
            <Home className="w-8 h-8 text-muted-foreground" />
          </div>
          <h1 className="text-2xl font-bold mb-2">Household Not Found</h1>
          <p className="text-muted-foreground mb-6">
            This household doesn't exist or has been removed.
          </p>
          <Link to="/">
            <button className="text-primary hover:underline">
              Go back home
            </button>
          </Link>
        </div>
      </div>
    );
  }

  const handlePinComplete = async (pin: string) => {
    setIsVerifying(true);
    setError(false);

    const success = await accessHousehold(household.id, pin);
    if (success) {
      if (selectedMemberId) {
        setCurrentMember(selectedMemberId);
      }
      navigate(`/household/${household.id}`);
    } else {
      setError(true);
      setIsVerifying(false);
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

        <div className="card-elevated p-8 text-center">
          <div className="w-14 h-14 rounded-2xl gradient-sage flex items-center justify-center mx-auto mb-6">
            <Lock className="w-7 h-7 text-primary-foreground" />
          </div>

          <h1 className="text-2xl font-bold mb-1">{household.name}</h1>
          <p className="text-muted-foreground mb-8">
            Enter your PIN to access the household
          </p>

          {members.length > 1 && (
            <div className="mb-8">
              <p className="text-sm text-muted-foreground mb-4">Who's this?</p>
              <MemberSelector
                members={members}
                selectedId={selectedMemberId}
                onSelect={setSelectedMemberId}
              />
            </div>
          )}

          <div className="mb-4">
            <PinInput
              onComplete={handlePinComplete}
              error={error}
              disabled={isVerifying}
            />
          </div>

          {error && (
            <p className="text-sm text-destructive animate-fade-in">
              Incorrect PIN. Please try again.
            </p>
          )}

          {isVerifying && (
            <p className="text-sm text-muted-foreground animate-fade-in">
              Verifying...
            </p>
          )}
        </div>
      </div>
    </div>
  );
}
