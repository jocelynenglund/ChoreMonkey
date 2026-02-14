import { useState, useEffect } from 'react';
import { useParams, Navigate } from 'react-router-dom';
import { LogOut, ClipboardList } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useHouseholdStore } from '@/stores/householdStore';
import { ChoreCard } from '@/components/ChoreCard';
import { AddChoreDialog } from '@/components/AddChoreDialog';
import { CompleteChoreDialog } from '@/components/CompleteChoreDialog';
import { InviteDialog } from '@/components/InviteDialog';
import { MemberAvatar } from '@/components/MemberAvatar';
import type { Household, Chore, ChoreFrequency } from '@/types/household';

export default function HouseholdDashboard() {
  const { id } = useParams<{ id: string }>();
  const [household, setHousehold] = useState<Household | null>(null);
  const [chores, setChores] = useState<Chore[]>([]);
  const [isDataLoading, setIsDataLoading] = useState(true);
  const [completingChore, setCompletingChore] = useState<Chore | null>(null);

  const {
    isAuthenticated,
    currentHouseholdId,
    currentMemberId,
    getHousehold,
    getHouseholdMembers,
    fetchHouseholdMembers,
    getHouseholdChores,
    addChore,
    toggleChoreComplete,
    completeChore,
    assignChore,
    deleteChore,
    generateInvite,
    logout,
  } = useHouseholdStore();

  const members = getHouseholdMembers(id || '');
  const currentMember = members.find((m) => m.id === currentMemberId);

  useEffect(() => {
    const fetchData = async () => {
      if (!id) return;
      setIsDataLoading(true);

      const [fetchedHousehold, fetchedChores] = await Promise.all([
        getHousehold(id),
        getHouseholdChores(id),
        fetchHouseholdMembers(id),
      ]);

      setHousehold(fetchedHousehold);
      console.log('Fetched chores:', fetchedChores);
      setChores(fetchedChores);
      setIsDataLoading(false);
    };

    fetchData();
  }, [id, getHousehold, getHouseholdChores, fetchHouseholdMembers]);

  // Redirect if not authenticated or wrong household
  if (!isAuthenticated || currentHouseholdId !== id) {
    return <Navigate to={`/access/${id}`} replace />;
  }

  if (isDataLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="text-center">
          <div className="w-12 h-12 rounded-xl gradient-monkey flex items-center justify-center mx-auto mb-4 animate-pulse">
            <span className="text-2xl">🐵</span>
          </div>
          <p className="text-muted-foreground">Loading household...</p>
        </div>
      </div>
    );
  }

  if (!household) {
    return <Navigate to="/" replace />;
  }

  const pendingChores = chores.filter((c) => !c.completed);
  const completedChores = chores.filter((c) => c.completed);

  const handleAddChore = async (displayName: string, description: string, frequency?: ChoreFrequency) => {
    const newChore = await addChore(household.id, displayName, description, frequency);
    if (newChore) {
      setChores((prev) => [...prev, newChore]);
    }
  };

  const handleCompleteChore = async (choreId: string, completedAt?: Date) => {
    if (!household || !currentMemberId) return;
    await completeChore(household.id, choreId, currentMemberId, completedAt);
    // Refresh chores to get updated lastCompletedAt
    const updatedChores = await getHouseholdChores(household.id);
    setChores(updatedChores);
  };

  const openCompleteDialog = (chore: Chore) => {
    setCompletingChore(chore);
  };

  const handleToggleComplete = (choreId: string) => {
    toggleChoreComplete(choreId);
    setChores((prev) =>
      prev.map((c) => (c.id === choreId ? { ...c, completed: !c.completed } : c))
    );
  };

  const handleAssignChore = async (choreId: string, memberId: string | undefined) => {
    if (!household) return;
    await assignChore(household.id, choreId, memberId);
    setChores((prev) =>
      prev.map((c) => (c.id === choreId ? { ...c, assignedTo: memberId } : c))
    );
  };

  const handleDeleteChore = (choreId: string) => {
    deleteChore(choreId);
    setChores((prev) => prev.filter((c) => c.id !== choreId));
  };

  const handleGenerateInvite = () => {
    return generateInvite(household.id);
  };

  return (
    <div className="min-h-screen pb-8">
      {/* Header */}
      <header className="sticky top-0 z-10 bg-background/80 backdrop-blur-lg border-b border-border">
        <div className="max-w-3xl mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl gradient-monkey flex items-center justify-center">
                <span className="text-xl">🐵</span>
              </div>
              <div>
                <h1 className="font-bold text-lg leading-tight">
                  {household.name}
                </h1>
                <p className="text-xs text-muted-foreground">
                  {members.length} member{members.length !== 1 ? 's' : ''}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              {currentMember && (
                <MemberAvatar
                  nickname={currentMember.nickname}
                  color={currentMember.avatarColor}
                  size="sm"
                />
              )}
              <Button
                variant="ghost"
                size="icon"
                onClick={logout}
                className="text-muted-foreground hover:text-foreground"
              >
                <LogOut className="w-5 h-5" />
              </Button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-3xl mx-auto px-4 pt-6">
        {/* Members Strip */}
        <div className="card-elevated p-4 mb-6">
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-semibold text-sm text-muted-foreground">
              Family Members
            </h2>
            <InviteDialog onGenerate={handleGenerateInvite} />
          </div>
          <div className="flex gap-3 overflow-x-auto pb-1">
            {members.map((member) => (
              <div
                key={member.id}
                className="flex flex-col items-center gap-1 min-w-fit"
              >
                <MemberAvatar
                  nickname={member.nickname}
                  color={member.avatarColor}
                  size="md"
                />
                <span className="text-xs text-muted-foreground">
                  {member.nickname}
                </span>
              </div>
            ))}
          </div>
        </div>

        {/* Chores Section */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <ClipboardList className="w-5 h-5 text-primary" />
            <h2 className="font-bold text-lg">Chores</h2>
            <span className="px-2 py-0.5 rounded-full bg-primary/10 text-primary text-sm font-medium">
              {pendingChores.length}
            </span>
          </div>
          <AddChoreDialog onAdd={handleAddChore} />
        </div>

        {/* Pending Chores */}
        {chores.length === 0 ? (
          <div className="card-elevated p-12 text-center">
            <div className="w-16 h-16 rounded-2xl bg-muted flex items-center justify-center mx-auto mb-4">
              <ClipboardList className="w-8 h-8 text-muted-foreground" />
            </div>
            <h3 className="font-semibold text-lg mb-2">No chores yet!</h3>
            <p className="text-muted-foreground mb-6">
              Add your first chore to get started.
            </p>
          </div>
        ) : (
          <div className="space-y-3">
            {pendingChores.map((chore) => (
              <ChoreCard
                key={chore.id}
                chore={chore}
                members={members}
                currentMemberId={currentMemberId || undefined}
                onToggleComplete={() => handleToggleComplete(chore.id)}
                onComplete={() => openCompleteDialog(chore)}
                onAssign={(memberId) => handleAssignChore(chore.id, memberId)}
                onDelete={() => handleDeleteChore(chore.id)}
              />
            ))}

            {/* Completed Section */}
            {completedChores.length > 0 && (
              <>
                <div className="flex items-center gap-2 mt-8 mb-3">
                  <h3 className="font-semibold text-muted-foreground">
                    Completed
                  </h3>
                  <span className="px-2 py-0.5 rounded-full bg-success/10 text-success text-sm font-medium">
                    {completedChores.length}
                  </span>
                </div>
                {completedChores.map((chore) => (
                  <ChoreCard
                    key={chore.id}
                    chore={chore}
                    members={members}
                    currentMemberId={currentMemberId || undefined}
                    onToggleComplete={() => handleToggleComplete(chore.id)}
                    onComplete={() => openCompleteDialog(chore)}
                    onAssign={(memberId) => handleAssignChore(chore.id, memberId)}
                    onDelete={() => handleDeleteChore(chore.id)}
                  />
                ))}
              </>
            )}
          </div>
        )}
      </main>

      {/* Complete Chore Dialog */}
      <CompleteChoreDialog
        chore={completingChore}
        open={completingChore !== null}
        onOpenChange={(open) => !open && setCompletingChore(null)}
        onComplete={handleCompleteChore}
      />
    </div>
  );
}
