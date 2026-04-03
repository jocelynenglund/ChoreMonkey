import { useState } from 'react';
import { useParams, Navigate, Link } from 'react-router-dom';
import { ClipboardList, Users, Clock, ShieldCheck, LogOut, Check, Copy } from 'lucide-react';
import { useHouseholdStore } from '@/stores/householdStore';
import { useHouseholdData } from '@/hooks/useHouseholdData';
import { useHouseholdActions } from '@/hooks/useHouseholdActions';
import { useHouseholdRealtime } from '@/hooks/useHouseholdRealtime';
import { ConnectionStatus } from '@/components/ConnectionStatus';
import { MemberAvatar } from '@/components/MemberAvatar';
import { Button } from '@/components/ui/button';
import { CompleteChoreDialog } from '@/components/CompleteChoreDialog';
import { ProfileDialog } from '@/components/ProfileDialog';
import { AllowanceDialog } from '@/components/AllowanceDialog';
import { WhatsNewDialog } from '@/components/WhatsNewDialog';
import { SettingsDialog } from '@/components/SettingsDialog';
import { ChoresTab } from '@/components/tabs/ChoresTab';
import { TeamTab } from '@/components/tabs/TeamTab';
import { ActivityTab } from '@/components/tabs/ActivityTab';
import type { Chore } from '@/types/household';

type Tab = 'chores' | 'team' | 'activity';

export default function HouseholdDashboard() {
  const { id } = useParams<{ id: string }>();
  const { isAuthenticated, currentHouseholdId, currentMemberId, isAdmin, logout, removeMember, fetchHouseholdMembers } = useHouseholdStore();

  const { household, chores, isDataLoading, isRefreshing, refreshKey, refreshData, setHousehold, setChores, bumpRefreshKey } = useHouseholdData(id);
  const actions = useHouseholdActions({ household, chores, setChores, bumpRefreshKey });

  const { connectionState, reconnect } = useHouseholdRealtime({
    householdId: id ?? null,
    onRefreshNeeded: refreshData,
    enabled: isAuthenticated && currentHouseholdId === id,
  });

  const [activeTab, setActiveTab] = useState<Tab>('chores');
  const [completingChore, setCompletingChore] = useState<Chore | null>(null);
  const [profileOpen, setProfileOpen] = useState(false);
  const [allowanceOpen, setAllowanceOpen] = useState(false);
  const [whatsNewOpen, setWhatsNewOpen] = useState(false);
  const [hoveredMemberStatus, setHoveredMemberStatus] = useState<string | null>(null);
  const [slugCopied, setSlugCopied] = useState(false);

  const getHouseholdMembers = useHouseholdStore((s) => s.getHouseholdMembers);
  const members = getHouseholdMembers(id || '') || [];
  const currentMember = currentMemberId ? members.find((m) => m.id === currentMemberId) : undefined;

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

  if (!household) return <Navigate to="/" replace />;

  const openCompleteDialog = (choreOrId: Chore | string) => {
    if (typeof choreOrId === 'string') {
      const chore = chores.find((c) => c.id === choreOrId);
      if (chore) setCompletingChore(chore);
    } else {
      setCompletingChore(choreOrId);
    }
  };

  const handleRemoveMember = async (memberId: string, pinCode: string) => {
    if (!currentMemberId) return false;
    const success = await removeMember(household.id, memberId, currentMemberId, pinCode);
    if (success) await fetchHouseholdMembers(household.id);
    return success;
  };

  return (
    <div className="min-h-screen pb-20"> {/* pb-20 for bottom nav */}
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
                <h1 className="font-bold text-lg leading-tight">{household.name}</h1>
                <p className="text-xs text-muted-foreground">
                  {members.length} member{members.length !== 1 ? 's' : ''}
                  {household.slug && (
                    <button
                      className="ml-2 inline-flex items-center gap-1 text-primary hover:underline font-mono"
                      title="Copy household link"
                      onClick={() => {
                        navigator.clipboard.writeText(`${window.location.origin}/h/${household.slug}`);
                        setSlugCopied(true);
                        setTimeout(() => setSlugCopied(false), 2000);
                      }}
                    >
                      /h/{household.slug}
                      {slugCopied ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
                    </button>
                  )}
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
                  <MemberAvatar nickname={currentMember.nickname} color={currentMember.avatarColor} size="sm" />
                </button>
              )}
              <SettingsDialog
                householdId={household.id}
                currentSlug={household.slug}
                onSlugChanged={(slug) => setHousehold((prev) => prev ? { ...prev, slug } : prev)}
              />
              <Button variant="ghost" size="icon" onClick={logout} className="text-muted-foreground hover:text-foreground">
                <LogOut className="w-5 h-5" />
              </Button>
            </div>
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="max-w-3xl mx-auto px-4 pt-6">
        {activeTab === 'chores' && (
          <ChoresTab
            householdId={household.id}
            currentMemberId={currentMemberId}
            members={members}
            chores={chores}
            refreshKey={refreshKey}
            isAdmin={isAdmin}
            onCompleteChore={openCompleteDialog}
            onAssignChore={actions.handleAssignChore}
            onDeleteChore={actions.handleDeleteChore}
            onAddChore={actions.handleAddChore}
            onSetChoreRates={actions.handleSetChoreRates}
          />
        )}
        {activeTab === 'team' && (
          <TeamTab
            householdId={household.id}
            members={members}
            currentMemberId={currentMemberId}
            isAdmin={isAdmin}
            refreshKey={refreshKey}
            hoveredMemberStatus={hoveredMemberStatus}
            onHoverStatus={setHoveredMemberStatus}
            onGenerateInvite={actions.handleGenerateInvite}
            onRemoveMember={handleRemoveMember}
            onAssignmentChange={bumpRefreshKey}
          />
        )}
        {activeTab === 'activity' && (
          <ActivityTab householdId={household.id} refreshKey={refreshKey} />
        )}
      </main>

      {/* Bottom Tab Bar */}
      <nav className="fixed bottom-0 left-0 right-0 z-20 bg-background/90 backdrop-blur-lg border-t border-border">
        <div className="max-w-3xl mx-auto flex">
          <button
            className={`flex-1 flex flex-col items-center gap-1 py-3 text-xs transition-colors ${activeTab === 'chores' ? 'text-primary' : 'text-muted-foreground hover:text-foreground'}`}
            onClick={() => setActiveTab('chores')}
          >
            <ClipboardList className="w-5 h-5" />
            Chores
          </button>
          <button
            className={`flex-1 flex flex-col items-center gap-1 py-3 text-xs transition-colors ${activeTab === 'team' ? 'text-primary' : 'text-muted-foreground hover:text-foreground'}`}
            onClick={() => setActiveTab('team')}
          >
            <Users className="w-5 h-5" />
            Team
          </button>
          <button
            className={`flex-1 flex flex-col items-center gap-1 py-3 text-xs transition-colors ${activeTab === 'activity' ? 'text-primary' : 'text-muted-foreground hover:text-foreground'}`}
            onClick={() => setActiveTab('activity')}
          >
            <Clock className="w-5 h-5" />
            Activity
          </button>
          {isAdmin && (
            <Link
              to={`/household/${id}/admin`}
              className="flex-1 flex flex-col items-center gap-1 py-3 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              <ShieldCheck className="w-5 h-5" />
              Admin
            </Link>
          )}
        </div>
      </nav>

      {/* Dialogs */}
      <CompleteChoreDialog
        chore={completingChore}
        open={completingChore !== null}
        onOpenChange={(open) => !open && setCompletingChore(null)}
        onComplete={actions.handleCompleteChore}
      />

      {currentMember && (
        <ProfileDialog
          open={profileOpen}
          onOpenChange={setProfileOpen}
          householdId={household.id}
          memberId={currentMember.id}
          currentNickname={currentMember.nickname}
          currentStatus={currentMember.status ?? ''}
          avatarColor={currentMember.avatarColor}
          onViewAllowance={() => setAllowanceOpen(true)}
        />
      )}

      <AllowanceDialog open={allowanceOpen} onOpenChange={setAllowanceOpen} />

      <WhatsNewDialog variant="controlled" open={whatsNewOpen} onOpenChange={setWhatsNewOpen} />
    </div>
  );
}
