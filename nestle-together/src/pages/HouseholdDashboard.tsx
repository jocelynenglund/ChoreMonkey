import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, Navigate } from 'react-router-dom';
import { LogOut, ClipboardList } from 'lucide-react';
import { useHouseholdRealtime } from '@/hooks/useHouseholdRealtime';
import { ConnectionStatus } from '@/components/ConnectionStatus';

// Marquee that scrolls long text
function StatusMarquee({ text }: { text: string }) {
  // Calculate duration based on text length (roughly 80px per second)
  const duration = Math.max(6, text.length * 0.12);

  return (
    <div className="mt-3 py-2 bg-muted/50 rounded-md overflow-hidden">
      <div 
        className="inline-flex whitespace-nowrap animate-marquee"
        style={{ animationDuration: `${duration}s` }}
      >
        <span className="text-sm text-muted-foreground px-4">
          💬 {text}
        </span>
        <span className="text-sm text-muted-foreground px-4">
          💬 {text}
        </span>
      </div>
    </div>
  );
}
import { Button } from '@/components/ui/button';
import { useHouseholdStore } from '@/stores/householdStore';
import { ChoreCard } from '@/components/ChoreCard';
import { AddChoreDialog } from '@/components/AddChoreDialog';
import { CompleteChoreDialog } from '@/components/CompleteChoreDialog';
import { InviteDialog } from '@/components/InviteDialog';
import { MemberAvatar } from '@/components/MemberAvatar';
import { TeamOverviewAccordion } from '@/components/TeamOverviewAccordion';
import { CompletionTimeline } from '@/components/CompletionTimeline';
import { SettingsDialog } from '@/components/SettingsDialog';
import { MyChoresSection } from '@/components/MyChoresSection';
import { ProfileDialog } from '@/components/ProfileDialog';
import { RemoveMemberDialog } from '@/components/RemoveMemberDialog';
import { WhatsNewDialog } from '@/components/WhatsNewDialog';
import type { Household, Chore, ChoreFrequency } from '@/types/household';

export default function HouseholdDashboard() {
  const { id } = useParams<{ id: string }>();
  const [household, setHousehold] = useState<Household | null>(null);
  const [chores, setChores] = useState<Chore[]>([]);
  const [isDataLoading, setIsDataLoading] = useState(true);
  const [completingChore, setCompletingChore] = useState<Chore | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [profileOpen, setProfileOpen] = useState(false);
  const [hoveredMemberStatus, setHoveredMemberStatus] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [whatsNewOpen, setWhatsNewOpen] = useState(false);

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
    removeMember,
  } = useHouseholdStore();

  const members = getHouseholdMembers(id || '') || [];
  const currentMember = currentMemberId ? members.find((m) => m.id === currentMemberId) : undefined;

  // Refresh all data
  const refreshData = useCallback(async () => {
    if (!id) return;
    setIsRefreshing(true);
    try {
      const [fetchedHousehold, fetchedChores] = await Promise.all([
        getHousehold(id),
        getHouseholdChores(id),
        fetchHouseholdMembers(id),
      ]);
      setHousehold(fetchedHousehold);
      setChores(fetchedChores);
      setRefreshKey((k) => k + 1);
    } finally {
      setIsRefreshing(false);
    }
  }, [id, getHousehold, getHouseholdChores, fetchHouseholdMembers]);

  // Real-time updates via SignalR
  const { connectionState, reconnect } = useHouseholdRealtime({
    householdId: id ?? null,
    onRefreshNeeded: refreshData,
    enabled: isAuthenticated && currentHouseholdId === id,
  });

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

  // Is this chore assigned to me?
  const isAssignedToMe = (chore: Chore): boolean => {
    return chore.assignedToAll || (chore.assignedTo?.includes(currentMemberId || '') ?? false);
  };

  // Other chores: not assigned to me, not optional, not completed
  const otherChores = (chores ?? []).filter((c) => !c.isOptional && !isAssignedToMe(c) && !c.completed);
  
  // Bonus chores: optional and not completed
  const bonusChores = (chores ?? []).filter((c) => c.isOptional && !c.completed);

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
      const chore = (chores ?? []).find((c) => c.id === choreOrId);
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
              <button
                onClick={() => setWhatsNewOpen(true)}
                className="w-10 h-10 rounded-xl gradient-monkey flex items-center justify-center hover:scale-105 transition-transform"
                title="What's New"
              >
                <span className="text-xl">🐵</span>
              </button>
              <div>
                <h1 className="font-bold text-lg leading-tight">
                  {household.name}
                </h1>
                <p className="text-xs text-muted-foreground">
                  {members?.length ?? 0} member{(members?.length ?? 0) !== 1 ? 's' : ''}
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <ConnectionStatus
                connectionState={connectionState}
                onRefresh={refreshData}
                onReconnect={reconnect}
                isRefreshing={isRefreshing}
              />
              {currentMember && (
                <button
                  onClick={() => setProfileOpen(true)}
                  className="rounded-full hover:ring-2 hover:ring-primary transition-all"
                  title="Edit profile"
                >
                  <MemberAvatar
                    nickname={currentMember.nickname}
                    color={currentMember.avatarColor}
                    size="sm"
                  />
                </button>
              )}
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
          <div className="flex gap-3 overflow-x-auto pb-2 pt-1 px-1 -mx-1">
            {(members ?? []).map((member) => (
              <div
                key={member.id}
                className="relative group flex-shrink-0"
                style={{ width: '72px', minWidth: '72px' }}
              >
                <button
                  className="flex flex-col items-center gap-1 cursor-pointer w-full"
                  title={member.status || member.nickname}
                  onClick={() => member.status && setHoveredMemberStatus(
                    hoveredMemberStatus === member.status ? null : member.status
                  )}
                >
                  <div className={member.status ? 'ring-2 ring-primary/50 ring-offset-2 rounded-full animate-pulse' : ''}>
                    <MemberAvatar
                      nickname={member.nickname}
                      color={member.avatarColor}
                      size="md"
                    />
                  </div>
                  <span 
                    className="text-xs text-muted-foreground text-center"
                    style={{ 
                      display: 'block',
                      width: '72px',
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap'
                    }}
                  >
                    {member.nickname}
                  </span>
                </button>
                {/* Remove button - admin only, can't remove self */}
                {isAdmin && member.id !== currentMemberId && (
                  <div className="absolute -top-1 -right-1 opacity-0 group-hover:opacity-100 transition-opacity">
                    <RemoveMemberDialog
                      member={member}
                      onRemove={async (pinCode) => {
                        if (currentMemberId) {
                          const success = await removeMember(household.id, member.id, currentMemberId, pinCode);
                          if (success) {
                            await fetchHouseholdMembers(household.id);
                          }
                          return success;
                        }
                        return false;
                      }}
                    />
                  </div>
                )}
              </div>
            ))}
          </div>
          {hoveredMemberStatus && (
            <StatusMarquee text={hoveredMemberStatus} />
          )}
        </div>

        {/* Admin: Overdue Chores (all members) */}
        <div className="mb-6">
          <TeamOverviewAccordion 
            householdId={household.id} 
            onAssignmentChange={() => setRefreshKey(k => k + 1)}
          />
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
        {(otherChores?.length ?? 0) > 0 && (
          <div className="mt-8">
            <div className="flex items-center gap-2 mb-3">
              <h3 className="font-semibold text-muted-foreground">📋 Other Chores</h3>
              <span className="px-2 py-0.5 rounded-full bg-muted text-muted-foreground text-xs font-medium">
                {otherChores?.length ?? 0}
              </span>
            </div>
            <div className="space-y-3">
              {(otherChores ?? []).map((chore) => (
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
        {(bonusChores?.length ?? 0) > 0 && (
          <div className="mt-8">
            <div className="flex items-center gap-2 mb-3">
              <h3 className="font-semibold text-amber-600">🌟 Bonus Chores</h3>
              <span className="px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 text-xs font-medium">
                {bonusChores?.length ?? 0}
              </span>
            </div>
            <div className="space-y-3">
              {(bonusChores ?? []).map((chore) => (
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
          <CompletionTimeline key={refreshKey} householdId={household.id} />
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
      {currentMember && (
        <ProfileDialog
          open={profileOpen}
          onOpenChange={setProfileOpen}
          householdId={household.id}
          memberId={currentMember.id}
          currentNickname={currentMember.nickname}
          currentStatus={currentMember.status ?? ''}
          avatarColor={currentMember.avatarColor}
        />
      )}

      {/* What's New Dialog */}
      <WhatsNewDialog
        variant="controlled"
        open={whatsNewOpen}
        onOpenChange={setWhatsNewOpen}
      />
    </div>
  );
}
