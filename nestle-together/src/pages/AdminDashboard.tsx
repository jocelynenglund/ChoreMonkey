import { useState } from 'react';
import { useParams, Navigate, Link } from 'react-router-dom';
import { ArrowLeft, ShieldCheck } from 'lucide-react';
import { useHouseholdStore } from '@/stores/householdStore';
import { AdminPanel } from '@/features/admin';
import { PinInput } from '@/components/PinInput';

export default function AdminDashboard() {
  const { id } = useParams<{ id: string }>();
  const { isAuthenticated, currentHouseholdId, isAdmin, accessHousehold } = useHouseholdStore();

  const [pinVerified, setPinVerified] = useState(isAdmin); // already admin = skip PIN
  const [pinError, setPinError] = useState(false);
  const [verifying, setVerifying] = useState(false);

  // Must be logged into this household
  if (!isAuthenticated || currentHouseholdId !== id) {
    return <Navigate to={`/access/${id}`} replace />;
  }

  const handlePinComplete = async (pin: string) => {
    if (!id) return;
    setVerifying(true);
    setPinError(false);
    const success = await accessHousehold(id, pin);
    if (success) {
      setPinVerified(true);
    } else {
      setPinError(true);
    }
    setVerifying(false);
  };

  if (!pinVerified) {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center px-4 py-12">
        <div className="w-full max-w-sm">
          <Link
            to={`/household/${id}`}
            className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground mb-8 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
            Back to household
          </Link>

          <div className="card-elevated p-8 text-center">
            <div className="w-14 h-14 rounded-2xl gradient-monkey flex items-center justify-center mx-auto mb-6">
              <ShieldCheck className="w-7 h-7 text-primary-foreground" />
            </div>
            <h1 className="text-xl font-bold mb-1">Admin Access</h1>
            <p className="text-muted-foreground mb-8 text-sm">
              Enter the admin PIN to continue
            </p>
            <PinInput onComplete={handlePinComplete} error={pinError} disabled={verifying} />
            {pinError && (
              <p className="text-sm text-destructive mt-3">Incorrect PIN. Try again.</p>
            )}
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen pb-8">
      <header className="sticky top-0 z-10 bg-background/80 backdrop-blur-lg border-b border-border">
        <div className="max-w-3xl mx-auto px-4 py-4 flex items-center gap-3">
          <Link
            to={`/household/${id}`}
            className="inline-flex items-center gap-2 text-muted-foreground hover:text-foreground transition-colors"
          >
            <ArrowLeft className="w-4 h-4" />
          </Link>
          <ShieldCheck className="w-5 h-5 text-primary" />
          <h1 className="font-bold text-lg">Admin Panel</h1>
        </div>
      </header>
      <main className="max-w-3xl mx-auto px-4 pt-6">
        <AdminPanel />
      </main>
    </div>
  );
}
