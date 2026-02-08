import { useState } from 'react';
import { useNavigate, Link, useParams } from 'react-router-dom';
import { ArrowLeft, Users, UserPlus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useHouseholdStore } from '@/stores/householdStore';

export default function JoinHousehold() {
  const navigate = useNavigate();
  const { inviteCode: urlInviteCode } = useParams();
  const { getInviteByCode, joinHousehold } = useHouseholdStore();

  const [inviteCode, setInviteCode] = useState(urlInviteCode || '');
  const [nickname, setNickname] = useState('');
  const [step, setStep] = useState(urlInviteCode ? 2 : 1);
  const [error, setError] = useState('');
  const [householdName, setHouseholdName] = useState('');

  const handleVerifyCode = () => {
    const invite = getInviteByCode(inviteCode.toUpperCase());
    if (!invite) {
      setError('Invalid invite code. Please check and try again.');
      return;
    }
    setError('');
    setHouseholdName(invite.householdName);
    setStep(2);
  };

  const handleJoin = () => {
    if (!nickname.trim()) {
      setError('Please enter your nickname');
      return;
    }

    const member = joinHousehold(inviteCode.toUpperCase(), nickname.trim());
    if (!member) {
      setError('Could not join household. Please try again.');
      return;
    }

    navigate(`/household/${member.householdId}`);
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

        <div className="card-elevated p-8">
          {step === 1 ? (
            <>
              <div className="w-14 h-14 rounded-2xl bg-accent/20 flex items-center justify-center mb-6">
                <Users className="w-7 h-7 text-accent" />
              </div>
              <h1 className="text-2xl font-bold mb-2">Join a Household</h1>
              <p className="text-muted-foreground mb-6">
                Enter the invite code shared with you by a family member.
              </p>

              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="code">Invite Code</Label>
                  <Input
                    id="code"
                    placeholder="XXXXXX"
                    value={inviteCode}
                    onChange={(e) => setInviteCode(e.target.value.toUpperCase())}
                    className="h-12 text-base text-center tracking-[0.3em] font-mono uppercase"
                    maxLength={6}
                    autoFocus
                  />
                </div>

                {error && (
                  <p className="text-sm text-destructive">{error}</p>
                )}

                <Button
                  onClick={handleVerifyCode}
                  className="w-full h-12 text-base"
                  disabled={inviteCode.length < 6}
                >
                  Continue
                </Button>
              </div>
            </>
          ) : (
            <>
              <div className="w-14 h-14 rounded-2xl gradient-monkey flex items-center justify-center mb-6">
                <span className="text-2xl">🐵</span>
              </div>
              <h1 className="text-2xl font-bold mb-2">Welcome!</h1>
              <p className="text-muted-foreground mb-6">
                You're joining{' '}
                <span className="font-semibold text-foreground">{householdName}</span>.
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
                  />
                </div>

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
                    onClick={handleJoin}
                    className="flex-1 h-12 text-base"
                  >
                    Join Household
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
