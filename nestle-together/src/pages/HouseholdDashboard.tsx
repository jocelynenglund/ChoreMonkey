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

  const completedChores = chores.filter((c) => c.completed);
  const pendingChores = chores.filter((c) => !c.completed);
  
  // Split pending chores into sections
  const myChores = pendingChores.filter(
    (c) => !c.isOptional && c.assignedTo?.includes(currentMemberId || '') && !c.assignedToAll
  );
  const everyoneChores = pendingChores.filter(
    (c) => !c.isOptional && c.assignedToAll
  );
  const othersChores = pendingChores.filter(
    (c) => !c.isOptional && c.assignedTo?.length && !c.assignedTo.includes(currentMemberId || '') && !c.assignedToAll
  );
  const unassignedChores = pendingChores.filter(
    (c) => !c.isOptional && !c.assignedTo?.length && !c.assignedToAll
  );
  const bonusChores = pendingChores.filter((c) => c.isOptional);

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
              {pendingChores.length}
            </span>
          </div>
          <AddChoreDialog onAdd={handleAddChore} />
        </div>

        {/* Chores by Section */}
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
          <div className="space-y-6">
            {/* My Chores */}
            {myChores.length > 0 && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-muted-foreground">✅ My Chores</h3>
                  <span className="px-2 py-0.5 rounded-full bg-primary/10 text-primary text-xs font-medium">
                    {myChores.length}
                  </span>
                </div>
                {myChores.map((chore) => (
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
              </div>
            )}

            {/* Everyone's Chores */}
            {everyoneChores.length > 0 && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-muted-foreground">👥 Everyone's Chores</h3>
                  <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 text-xs font-medium">
                    {everyoneChores.length}
                  </span>
                </div>
                {everyoneChores.map((chore) => (
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
              </div>
            )}

            {/* Others' Chores */}
            {othersChores.length > 0 && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-muted-foreground">👤 Others' Chores</h3>
                  <span className="px-2 py-0.5 rounded-full bg-purple-100 text-purple-700 text-xs font-medium">
                    {othersChores.length}
                  </span>
                </div>
                {othersChores.map((chore) => (
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
              </div>
            )}

            {/* Unassigned Chores */}
            {unassignedChores.length > 0 && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-muted-foreground">📋 Unassigned</h3>
                  <span className="px-2 py-0.5 rounded-full bg-gray-100 text-gray-700 text-xs font-medium">
                    {unassignedChores.length}
                  </span>
                </div>
                {unassignedChores.map((chore) => (
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
              </div>
            )}

            {/* Bonus Chores */}
            {bonusChores.length > 0 && (
              <div className="space-y-3">
                <div className="flex items-center gap-2">
                  <h3 className="font-semibold text-amber-600">🌟 Bonus Chores</h3>
                  <span className="px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 text-xs font-medium">
                    {bonusChores.length}
                  </span>
                </div>
                <p className="text-xs text-muted-foreground -mt-1 mb-2">
                  Anyone can do these for extra credit!
                </p>
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
              </div>
            )}

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
                    onComplete={() => openCompleteDialog(chore)}
                    onAssign={(memberIds, assignToAll) => handleAssignChore(chore.id, memberIds, assignToAll)}
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
