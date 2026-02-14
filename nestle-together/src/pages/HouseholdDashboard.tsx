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
import { OverdueAccordion } from '@/components/OverdueAccordion';
import { CompletionTimeline } from '@/components/CompletionTimeline';
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

  // Helper: check if I've completed this chore in the current period
  const isCompletedByMe = (chore: Chore): boolean => {
    if (chore.completed) return true; // One-time chore done
    const myCompletion = chore.memberCompletions?.find(mc => mc.memberId === currentMemberId);
    if (!myCompletion) return false;
    
    const freqType = chore.frequency?.type || 'once';
    const hasNoDays = !chore.frequency?.days?.length;
    
    // Weekly with no specific days = weekly-anyday
    if (freqType === 'weekly' && hasNoDays) {
      return myCompletion.completedThisWeek;
    }
    // Daily or weekly with specific days: check today
    return myCompletion.completedToday;
  };

  // Is this chore assigned to me?
  const isAssignedToMe = (chore: Chore): boolean => {
    return chore.assignedToAll || (chore.assignedTo?.includes(currentMemberId || '') ?? false);
  };

  // My chores: assigned to me, split by completion status
  const allMyChores = chores.filter((c) => !c.isOptional && isAssignedToMe(c));
  const myPendingChores = allMyChores.filter((c) => !isCompletedByMe(c));
  const myCompletedChores = allMyChores.filter((c) => isCompletedByMe(c));

  // Other chores: not assigned to me (excluding bonus/optional)
  const otherChores = chores.filter((c) => !c.isOptional && !isAssignedToMe(c) && !c.completed);
  
  // Bonus chores
  const bonusChores = chores.filter((c) => c.isOptional && !c.completed);

  const handleAddChore = async (displayName: string, description: string, frequency?: ChoreFrequency, isOptional?: boolean) => {
    const newChore = await addChore(household.id, displayName, description, frequency, isOptional);
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

  const handleAssignChore = async (choreId: string, memberIds?: string[], assignToAll?: boolean) => {
    if (!household) return;
    await assignChore(household.id, choreId, memberIds, assignToAll);
    setChores((prev) =>
      prev.map((c) => (c.id === choreId ? { ...c, assignedTo: memberIds, assignedToAll: assignToAll } : c))
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

        {/* Overdue Chores Accordion */}
        <div className="mb-6">
          <OverdueAccordion householdId={household.id} />
        </div>

        {/* Chores Section */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <ClipboardList className="w-5 h-5 text-primary" />
            <h2 className="font-bold text-lg">Chores</h2>
            <span className="px-2 py-0.5 rounded-full bg-primary/10 text-primary text-sm font-medium">
              {myPendingChores.length}
            </span>
          </div>
          <AddChoreDialog onAdd={handleAddChore} />
        </div>

        {/* Chores List */}
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
            {/* My Pending Chores */}
            {myPendingChores.length > 0 && (
              <>
                <div className="flex items-center gap-2 mb-2">
                  <h3 className="font-semibold text-primary">📌 My Chores</h3>
                  <span className="px-2 py-0.5 rounded-full bg-primary/10 text-primary text-xs font-medium">
                    {myPendingChores.length}
                  </span>
                </div>
                {myPendingChores.map((chore) => (
                  <ChoreCard
                    key={chore.id}
                    chore={chore}
                    members={members}
                    currentMemberId={currentMemberId || undefined}
                    onComplete={() => openCompleteDialog(chore)}
                    onAssign={(memberIds, assignToAll) => handleAssignChore(chore.id, memberIds, assignToAll)}
                    onDelete={() => handleDeleteChore(chore.id)}
                  />
                ))}
              </>
            )}

            {/* My Completed Chores */}
            {myCompletedChores.length > 0 && (
              <>
                <div className="flex items-center gap-2 mt-4 mb-2">
                  <h3 className="font-semibold text-success">✅ My Completed</h3>
                  <span className="px-2 py-0.5 rounded-full bg-success/10 text-success text-xs font-medium">
                    {myCompletedChores.length}
                  </span>
                </div>
                {myCompletedChores.map((chore) => (
                  <ChoreCard
                    key={chore.id}
                    chore={chore}
                    members={members}
                    currentMemberId={currentMemberId || undefined}
                    onComplete={() => openCompleteDialog(chore)}
                    onAssign={(memberIds, assignToAll) => handleAssignChore(chore.id, memberIds, assignToAll)}
                    onDelete={() => handleDeleteChore(chore.id)}
                  />
                ))}
              </>
            )}

            {/* Other Chores */}
            {otherChores.length > 0 && (
              <>
                <div className="flex items-center gap-2 mt-6 mb-2">
                  <h3 className="font-semibold text-muted-foreground">📋 Other Chores</h3>
                  <span className="px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-xs font-medium">
                    {otherChores.length}
                  </span>
                </div>
                {otherChores.map((chore) => (
                  <ChoreCard
                    key={chore.id}
                    chore={chore}
                    members={members}
                    currentMemberId={currentMemberId || undefined}
                    onComplete={() => openCompleteDialog(chore)}
                    onAssign={(memberIds, assignToAll) => handleAssignChore(chore.id, memberIds, assignToAll)}
                    onDelete={() => handleDeleteChore(chore.id)}
                  />
                ))}
              </>
            )}

            {/* Bonus Chores */}
            {bonusChores.length > 0 && (
              <>
                <div className="flex items-center gap-2 mt-6 mb-3">
                  <h3 className="font-semibold text-amber-600">🌟 Bonus Chores</h3>
                  <span className="px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 text-xs font-medium">
                    {bonusChores.length}
                  </span>
                </div>
                {bonusChores.map((chore) => (
                  <ChoreCard
                    key={chore.id}
                    chore={chore}
                    members={members}
                    currentMemberId={currentMemberId || undefined}
                    onComplete={() => openCompleteDialog(chore)}
                    onAssign={(memberIds, assignToAll) => handleAssignChore(chore.id, memberIds, assignToAll)}
                    onDelete={() => handleDeleteChore(chore.id)}
                  />
                ))}
              </>
            )}

          </div>
        )}

        {/* Recent Activity Timeline - moved to bottom */}
        <div className="mt-8">
          <CompletionTimeline householdId={household.id} />
        </div>
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
