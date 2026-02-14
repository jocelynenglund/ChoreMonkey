import { useState, useEffect } from 'react';
import { useNavigate, Link, useParams } from 'react-router-dom';
import { ArrowLeft, Users } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useHouseholdStore } from '@/stores/householdStore';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export default function JoinHousehold() {
  const navigate = useNavigate();
  const { householdId: urlHouseholdId, inviteId: urlInviteId } = useParams();
  const { joinHousehold, getHousehold } = useHouseholdStore();

  const [nickname, setNickname] = useState('');
  const [error, setError] = useState('');
  const [householdName, setHouseholdName] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [householdId, setHouseholdId] = useState(urlHouseholdId || '');
  const [inviteId, setInviteId] = useState(urlInviteId || '');

  // If we have IDs from URL, fetch household name
  useEffect(() => {
    const fetchHouseholdInfo = async () => {
      if (urlHouseholdId) {
        setIsLoading(true);
        try {
          const household = await getHousehold(urlHouseholdId);
          if (household) {
            setHouseholdName(household.name);
          }
        } catch (e) {
          console.error('Failed to fetch household', e);
        }
        setIsLoading(false);
      }
    };
    fetchHouseholdInfo();
  }, [urlHouseholdId, getHousehold]);

  const handleJoin = async () => {
    if (!nickname.trim()) {
      setError('Please enter your nickname');
      return;
    }

    if (!householdId || !inviteId) {
      setError('Invalid invite link');
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      const member = await joinHousehold(householdId, inviteId, nickname.trim());
      if (!member) {
        setError('Could not join household. The invite may be invalid.');
        setIsLoading(false);
        return;
      }

      navigate(`/household/${householdId}`);
    } catch (e) {
      setError('Could not join household. Please try again.');
      setIsLoading(false);
    }
  };

  // If no URL params, show manual entry (shouldn't happen with proper invite links)
  if (!urlHouseholdId || !urlInviteId) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center px-4 py-12">
        <div className="w-full max-w-md animate-slide-up">
          <Link
            to="/"
            className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground mb-8 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            Back
          </Link>

          <div className="card-elevated p-8">
            <div className="w-14 h-14 rounded-2xl bg-accent/20 flex items-center justify-center mb-6">
              <Users className="w-7 h-7 text-accent" />
            </div>
            <h1 className="text-2xl font-bold mb-2">Join a Household</h1>
            <p className="text-muted-foreground mb-6">
              Ask a family member to share their invite link with you, or paste the link below.
            </p>
            <p className="text-sm text-muted-foreground">
              Invite links look like: <code className="bg-muted px-1 rounded">yoursite.com/join/household-id/invite-id</code>
            </p>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center px-4 py-12">
      <div className="w-full max-w-md animate-slide-up">
        <Link
          to="/"
          className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground mb-8 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Back
        </Link>

        <div className="card-elevated p-8">
          <div className="w-14 h-14 rounded-2xl gradient-monkey flex items-center justify-center mb-6">
            <span className="text-2xl">🐵</span>
          </div>
          <h1 className="text-2xl font-bold mb-2">Welcome!</h1>
          <p className="text-muted-foreground mb-6">
            You're joining{' '}
            <span className="font-semibold text-foreground">
              {householdName || 'a household'}
            </span>.
            What should we call you?
          </p>

          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="nickname">Your Nickname</Label>
              <Input
                id="nickname"
                placeholder="e.g., Mom, Dad, Alex..."
                value={nickname}
                onChange={(e) => setNickname(e.target.value)}
                className="h-12 text-base"
                autoFocus
                disabled={isLoading}
              />
            </div>

            {error && (
              <p className="text-sm text-destructive">{error}</p>
            )}

            <Button
              onClick={handleJoin}
              className="w-full h-12 text-base"
              disabled={isLoading || !nickname.trim()}
            >
              {isLoading ? 'Joining...' : 'Join Household'}
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
