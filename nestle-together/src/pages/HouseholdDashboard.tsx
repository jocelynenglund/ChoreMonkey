import { useState, useEffect, useRef } from 'react';
import { useParams, Navigate } from 'react-router-dom';
import { LogOut, ClipboardList } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useHouseholdStore } from '@/stores/householdStore';
import { useHouseholdRealtime } from '@/hooks/useHouseholdRealtime';
import { ChoreCard } from '@/components/ChoreCard';
import { AddChoreDialog } from '@/components/AddChoreDialog';
import { CompleteChoreDialog } from '@/components/CompleteChoreDialog';
import { InviteDialog } from '@/components/InviteDialog';
import { MemberAvatar } from '@/components/MemberAvatar';
import { OverdueAccordion } from '@/components/OverdueAccordion';
import { CompletionTimeline } from '@/components/CompletionTimeline';
import { SettingsDialog } from '@/components/SettingsDialog';
import { MyChoresSection } from '@/components/MyChoresSection';
import { ProfileDialog } from '@/components/ProfileDialog';
import { WhatsNewDialog } from '@/components/WhatsNewDialog';
import type { Household, Chore, ChoreFrequency } from '@/types/household';

// Smart marquee that only scrolls when text overflows
function StatusMarquee({ text }: { text: string }) {
  const containerRef = useRef<HTMLDivElement>(null);
  const textRef = useRef<HTMLSpanElement>(null);
  const [shouldScroll, setShouldScroll] = useState<boolean | null>(null);

  useEffect(() => {
    // Measure after render
    const measure = () => {
      if (containerRef.current && textRef.current) {
        const containerWidth = containerRef.current.offsetWidth;
        const textWidth = textRef.current.offsetWidth;
        setShouldScroll(textWidth > containerWidth - 32);
      }
    };
    
    // Measure immediately and after a short delay (for fonts/layout)
    measure();
    const timer = setTimeout(measure, 100);
    return () => clearTimeout(timer);
  }, [text]);

  return (
    <div 
      ref={containerRef}
      className="mt-3 py-2 bg-muted/50 rounded-md overflow-hidden"
    >
      <div className={`whitespace-nowrap ${shouldScroll ? 'animate-marquee' : 'px-4 text-center'}`}>
        <span ref={textRef} className="text-sm text-muted-foreground">
          💬 {text}
        </span>
        {shouldScroll && (
          <span className="text-sm text-muted-foreground px-8">
            💬 {text}
          </span>
        )}
      </div>
    </div>
  );
}

export default function HouseholdDashboard() {
  const { id } = useParams<{ id: string }>();
  const [household, setHousehold] = useState<Household | null>(null);
  const [isDataLoading, setIsDataLoading] = useState(true);
  const [completingChore, setCompletingChore] = useState<Chore | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [profileOpen, setProfileOpen] = useState(false);
  const [hoveredMemberStatus, setHoveredMemberStatus] = useState<string | null>(null);

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
    isAdmin,
    chores: storeChores,
  } = useHouseholdStore();

  // Connect to SignalR for real-time updates
  useHouseholdRealtime(id || null);

  const members = getHouseholdMembers(id || '');
  const currentMember = members.find((m) => m.id === currentMemberId);
  
  // Get chores from store (updates when SignalR events trigger refetch)
  const chores = storeChores.filter((c) => c.householdId === id);

  useEffect(() => {
    const fetchData = async () => {
      if (!id) return;
      setIsDataLoading(true);

      const [fetchedHousehold] = await Promise.all([
        getHousehold(id),
        getHouseholdChores(id),
        fetchHouseholdMembers(id),
      ]);

      setHousehold(fetchedHousehold);
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

  // Is this chore assigned to me?
  const isAssignedToMe = (chore: Chore): boolean => {
    return chore.assignedToAll || (chore.assignedTo?.includes(currentMemberId || '') ?? false);
  };

  // Other chores: not assigned to me, not optional, not completed
  const otherChores = chores.filter((c) => !c.isOptional && !isAssignedToMe(c) && !c.completed);
  
  // Bonus chores: optional and not completed
  const bonusChores = chores.filter((c) => c.isOptional && !c.completed);

  const handleAddChore = async (displayName: string, description: string, frequency?: ChoreFrequency, isOptional?: boolean, startDate?: Date) => {
    const newChore = await addChore(household.id, displayName, description, frequency, isOptional, startDate);
    if (newChore) {
      setChores((prev) => [...prev, newChore]);
      setRefreshKey((k) => k + 1); // Refresh MyChoresSection
    }
  };

  const handleCompleteChore = async (choreId: string, completedAt?: Date) => {
    if (!household || !currentMemberId) return;
    await completeChore(household.id, choreId, currentMemberId, completedAt);
    // Refresh chores to get updated lastCompletedAt
    const updatedChores = await getHouseholdChores(household.id);
    setChores(updatedChores);
    setRefreshKey((k) => k + 1); // Refresh MyChoresSection
  };

  const openCompleteDialog = (choreOrId: Chore | string) => {
    if (typeof choreOrId === 'string') {
      const chore = chores.find((c) => c.id === choreOrId);
      if (chore) setCompletingChore(chore);
    } else {
      setCompletingChore(choreOrId);
    }
  };

  const handleAssignChore = async (choreId: string, memberIds?: string[], assignToAll?: boolean) => {
    if (!household) return;
    await assignChore(household.id, choreId, memberIds, assignToAll);
    setChores((prev) =>
      prev.map((c) => (c.id === choreId ? { ...c, assignedTo: memberIds, assignedToAll: assignToAll } : c))
    );
    setRefreshKey((k) => k + 1); // Refresh MyChoresSection
  };

  const handleDeleteChore = async (choreId: string) => {
    if (!household) return;
    const success = await deleteChore(household.id, choreId);
    if (success) {
      setChores((prev) => prev.filter((c) => c.id !== choreId));
      setRefreshKey((k) => k + 1); // Refresh MyChoresSection
    }
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
                <button
                  onClick={() => setProfileOpen(true)}
                  className="rounded-full hover:ring-2 hover:ring-primary/50 transition-all"
                >
                  <MemberAvatar
                    nickname={currentMember.nickname}
                    color={currentMember.avatarColor}
                    size="sm"
                  />
                </button>
              )}
              <WhatsNewDialog />
              <SettingsDialog householdId={household.id} />
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
                className="flex flex-col items-center gap-1 w-16 flex-shrink-0 cursor-pointer"
                title={member.nickname}
                onMouseEnter={() => member.status && setHoveredMemberStatus(member.status)}
                onMouseLeave={() => setHoveredMemberStatus(null)}
                onClick={() => member.status && setHoveredMemberStatus(
                  hoveredMemberStatus === member.status ? null : member.status
                )}
              >
                <MemberAvatar
                  nickname={member.nickname}
                  color={member.avatarColor}
                  size="md"
                />
                <span className="text-xs text-muted-foreground truncate w-full text-center">
                  {member.nickname}
                </span>
                {member.status && (
                  <span className="text-[10px] text-muted-foreground">💬</span>
                )}
              </div>
            ))}
          </div>
          {/* Status Display - at bottom (scrolls if text overflows) */}
          {hoveredMemberStatus && (
            <StatusMarquee text={hoveredMemberStatus} />
          )}
        </div>

        {/* Admin: Overdue Chores (all members) */}
        <div className="mb-6">
          <OverdueAccordion householdId={household.id} />
        </div>

        {/* My Chores Section Header */}
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2">
            <ClipboardList className="w-5 h-5 text-primary" />
            <h2 className="font-bold text-lg">My Chores</h2>
          </div>
          <AddChoreDialog onAdd={handleAddChore} />
        </div>

        {/* My Chores - Personal Read Model */}
        {currentMemberId && (
          <MyChoresSection
            key={refreshKey}
            householdId={household.id}
            memberId={currentMemberId}
            onCompleteChore={openCompleteDialog}
          />
        )}

        {/* Other Chores (not assigned to me) */}
        {otherChores.length > 0 && (
          <div className="mt-8">
            <div className="flex items-center gap-2 mb-3">
              <h3 className="font-semibold text-muted-foreground">📋 Other Chores</h3>
              <span className="px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-xs font-medium">
                {otherChores.length}
              </span>
            </div>
            <div className="space-y-3">
              {otherChores.map((chore) => (
                <ChoreCard
                  key={chore.id}
                  chore={chore}
                  members={members}
                  currentMemberId={currentMemberId || undefined}
                  isAdmin={isAdmin}
                  onComplete={() => openCompleteDialog(chore)}
                  onAssign={(memberIds, assignToAll) => handleAssignChore(chore.id, memberIds, assignToAll)}
                  onDelete={() => handleDeleteChore(chore.id)}
                />
              ))}
            </div>
          </div>
        )}

        {/* Bonus Chores */}
        {bonusChores.length > 0 && (
          <div className="mt-8">
            <div className="flex items-center gap-2 mb-3">
              <h3 className="font-semibold text-amber-600">🌟 Bonus Chores</h3>
              <span className="px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 text-xs font-medium">
                {bonusChores.length}
              </span>
            </div>
            <div className="space-y-3">
              {bonusChores.map((chore) => (
                <ChoreCard
                  key={chore.id}
                  chore={chore}
                  members={members}
                  currentMemberId={currentMemberId || undefined}
                  isAdmin={isAdmin}
                  onComplete={() => openCompleteDialog(chore)}
                  onAssign={(memberIds, assignToAll) => handleAssignChore(chore.id, memberIds, assignToAll)}
                  onDelete={() => handleDeleteChore(chore.id)}
                />
              ))}
            </div>
          </div>
        )}

        {/* Recent Activity Timeline */}
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

      {/* Profile Dialog */}
      {currentMember && household && (
        <ProfileDialog
          open={profileOpen}
          onOpenChange={setProfileOpen}
          householdId={household.id}
          memberId={currentMember.id}
          currentNickname={currentMember.nickname}
          currentStatus={currentMember.status}
          avatarColor={currentMember.avatarColor}
        />
      )}
    </div>
  );
}
