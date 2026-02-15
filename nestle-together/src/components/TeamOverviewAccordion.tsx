import { useEffect, useState } from 'react';
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Checkbox } from '@/components/ui/checkbox';
import { CheckCircle2, AlertTriangle, Clock, Users, Settings2 } from 'lucide-react';
import { useAppStore } from '@/features/store';
import type { MemberOverview, ChoreStatus } from '@/features/activity/types';
import { MemberAvatar } from './MemberAvatar';

interface TeamOverviewAccordionProps {
  householdId: string;
  onAssignmentChange?: () => void;
  refreshKey?: number;
}

interface EditingChore {
  choreId: string;
  displayName: string;
  currentAssignees: string[];
}

export function TeamOverviewAccordion({ householdId, onAssignmentChange, refreshKey }: TeamOverviewAccordionProps) {
  const [teamData, setTeamData] = useState<MemberOverview[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [editingChore, setEditingChore] = useState<EditingChore | null>(null);
  const [selectedMembers, setSelectedMembers] = useState<string[]>([]);
  const [assignToAll, setAssignToAll] = useState(false);
  const [isSaving, setIsSaving] = useState(false);
  
  const { fetchTeamOverview, members, isAdmin, assignChore } = useAppStore();
  
  // Only admins can see team overview
  if (!isAdmin) {
    return null;
  }

  const loadTeam = async () => {
    setIsLoading(true);
    const data = await fetchTeamOverview(householdId);
    setTeamData(data);
    setIsLoading(false);
  };

  useEffect(() => {
    loadTeam();
  }, [householdId, fetchTeamOverview, refreshKey]);

  const handleEditChore = (chore: ChoreStatus) => {
    // Find all members currently assigned to this chore
    const currentAssignees = teamData
      .filter(m => m.chores.some(c => c.choreId === chore.choreId))
      .map(m => m.memberId);
    
    // Check if assigned to all (appears in everyone's list)
    const isAssignedToAll = currentAssignees.length === members.length && members.length > 0;
    
    setEditingChore({
      choreId: chore.choreId,
      displayName: chore.displayName,
      currentAssignees,
    });
    setSelectedMembers(currentAssignees);
    setAssignToAll(isAssignedToAll);
  };

  const handleSaveAssignment = async () => {
    if (!editingChore) return;
    
    setIsSaving(true);
    try {
      await assignChore(
        householdId, 
        editingChore.choreId, 
        assignToAll ? undefined : selectedMembers,
        assignToAll
      );
      // Refresh the data
      await loadTeam();
      setEditingChore(null);
      // Notify parent to refresh activity timeline
      onAssignmentChange?.();
    } finally {
      setIsSaving(false);
    }
  };

  const handleMemberToggle = (memberId: string) => {
    setSelectedMembers(prev => 
      prev.includes(memberId)
        ? prev.filter(id => id !== memberId)
        : [...prev, memberId]
    );
    // If manually selecting members, turn off "assign to all"
    setAssignToAll(false);
  };

  const handleAssignToAllToggle = (checked: boolean) => {
    setAssignToAll(checked);
    if (checked) {
      setSelectedMembers(members.map(m => m.id));
    }
  };

  if (isLoading) {
    return (
      <div className="rounded-lg border bg-card p-4">
        <p className="text-muted-foreground text-sm">Loading team overview...</p>
      </div>
    );
  }

  const getMemberColor = (memberId: string) => {
    const member = (members || []).find((m) => m.id === memberId);
    return member?.avatarColor || 'hsl(150 50% 50%)';
  };

  const totalOverdue = teamData.reduce((sum, m) => sum + m.overdueCount, 0);

  return (
    <>
      <div className="rounded-lg border bg-card">
        <div className="p-4 border-b">
          <h3 className="font-semibold flex items-center gap-2">
            <Users className="h-5 w-5 text-blue-500" />
            Household Overview
            {totalOverdue > 0 && (
              <Badge variant="destructive" className="ml-2">
                {totalOverdue} overdue
              </Badge>
            )}
          </h3>
          <p className="text-sm text-muted-foreground mt-1">
            Click the gear icon to reassign chores
          </p>
        </div>
        <Accordion type="multiple" className="w-full">
          {teamData.map((member) => (
            <AccordionItem key={member.memberId} value={member.memberId}>
              <AccordionTrigger className="px-4 hover:no-underline">
                <div className="flex flex-col sm:flex-row sm:items-center gap-1 sm:gap-3 w-full">
                  <div className="flex items-center gap-3">
                    <MemberAvatar
                      nickname={member.nickname}
                      color={getMemberColor(member.memberId)}
                      size="sm"
                    />
                    <span className="font-medium">{member.nickname}</span>
                  </div>
                  <div className="flex gap-2 sm:ml-auto mr-2 pl-11 sm:pl-0">
                    {member.overdueCount > 0 && (
                      <Badge variant="destructive">
                        {member.overdueCount} overdue
                      </Badge>
                    )}
                    <Badge variant="secondary" className="bg-green-100 text-green-700">
                      {member.completedCount}/{member.totalChores} done
                    </Badge>
                  </div>
                </div>
              </AccordionTrigger>
              <AccordionContent className="px-4 pb-4">
                {member.chores.length === 0 ? (
                  <p className="text-sm text-muted-foreground py-2">
                    No chores assigned yet
                  </p>
                ) : (
                  <ul className="space-y-2">
                    {member.chores.map((chore) => (
                      <li
                        key={chore.choreId}
                        className={`flex items-center justify-between py-2 px-3 rounded-md border ${
                          chore.status === 'overdue'
                            ? 'bg-red-50 border-red-100'
                            : chore.status === 'completed'
                            ? 'bg-green-50 border-green-100'
                            : 'bg-amber-50 border-amber-100'
                        }`}
                      >
                        <div className="flex items-center gap-2">
                          {chore.status === 'overdue' && (
                            <AlertTriangle className="h-4 w-4 text-red-500" />
                          )}
                          {chore.status === 'completed' && (
                            <CheckCircle2 className="h-4 w-4 text-green-500" />
                          )}
                          {chore.status === 'pending' && (
                            <Clock className="h-4 w-4 text-amber-500" />
                          )}
                          <span className="font-medium">{chore.displayName}</span>
                          {chore.isOptional && (
                            <Badge variant="outline" className="text-xs">bonus</Badge>
                          )}
                        </div>
                        <div className="flex items-center gap-2">
                          <span className={`text-sm ${
                            chore.status === 'overdue'
                              ? 'text-red-600'
                              : chore.status === 'completed'
                              ? 'text-green-600'
                              : 'text-amber-600'
                          }`}>
                            {chore.status === 'overdue' && chore.overduePeriod
                              ? `from ${chore.overduePeriod}`
                              : chore.status === 'completed'
                              ? '✓ done'
                              : 'pending'}
                          </span>
                          <button
                            onClick={(e) => {
                              e.stopPropagation();
                              handleEditChore(chore);
                            }}
                            className="p-1 rounded hover:bg-black/10 transition-colors"
                            title="Edit assignment"
                          >
                            <Settings2 className="h-4 w-4 text-muted-foreground" />
                          </button>
                        </div>
                      </li>
                    ))}
                  </ul>
                )}
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </div>

      {/* Assignment Dialog */}
      <Dialog open={!!editingChore} onOpenChange={() => setEditingChore(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Assign "{editingChore?.displayName}"</DialogTitle>
          </DialogHeader>
          
          <div className="space-y-4 py-4">
            {/* Assign to Everyone */}
            <div className="flex items-center space-x-3 p-3 rounded-lg border bg-muted/50">
              <Checkbox
                id="assign-all"
                checked={assignToAll}
                onCheckedChange={(checked) => handleAssignToAllToggle(checked as boolean)}
              />
              <label htmlFor="assign-all" className="flex items-center gap-2 cursor-pointer">
                <Users className="h-4 w-4" />
                <span className="font-medium">Assign to everyone</span>
              </label>
            </div>

            {/* Individual members */}
            <div className="space-y-2">
              <p className="text-sm text-muted-foreground">Or select specific members:</p>
              {members.map((member) => (
                <div
                  key={member.id}
                  className="flex items-center space-x-3 p-2 rounded-lg hover:bg-muted/50"
                >
                  <Checkbox
                    id={`member-${member.id}`}
                    checked={selectedMembers.includes(member.id)}
                    onCheckedChange={() => handleMemberToggle(member.id)}
                    disabled={assignToAll}
                  />
                  <label 
                    htmlFor={`member-${member.id}`}
                    className="flex items-center gap-2 cursor-pointer flex-1"
                  >
                    <MemberAvatar
                      nickname={member.nickname}
                      color={member.avatarColor}
                      size="sm"
                    />
                    <span>{member.nickname}</span>
                  </label>
                </div>
              ))}
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setEditingChore(null)}>
              Cancel
            </Button>
            <Button 
              onClick={handleSaveAssignment} 
              disabled={isSaving || (!assignToAll && selectedMembers.length === 0)}
            >
              {isSaving ? 'Saving...' : 'Save'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
