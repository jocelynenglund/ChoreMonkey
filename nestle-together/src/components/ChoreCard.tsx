import { Check, Trash2, User, Users, Repeat, Clock, ChevronDown, History } from 'lucide-react';
import { cn } from '@/lib/utils';
import type { Chore, Member, ChoreFrequency, ChoreCompletion } from '@/types/household';
import { MemberAvatar } from './MemberAvatar';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
  DropdownMenuCheckboxItem,
} from '@/components/ui/dropdown-menu';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible';
import { useState, useEffect } from 'react';
import { useHouseholdStore } from '@/stores/householdStore';

interface ChoreCardProps {
  chore: Chore;
  members: Member[];
  currentMemberId?: string;
  onComplete: () => void;
  onAssign: (memberIds?: string[], assignToAll?: boolean) => void;
  onDelete: () => void;
}

function formatFrequency(frequency?: ChoreFrequency): string {
  if (!frequency) return '';
  switch (frequency.type) {
    case 'daily':
      return 'Daily';
    case 'weekly':
      if (frequency.days && frequency.days.length > 0) {
        const dayNames = frequency.days.map(d => d.charAt(0).toUpperCase() + d.slice(1, 3));
        return dayNames.join(', ');
      }
      return 'Weekly';
    case 'interval':
      return `Every ${frequency.intervalDays} days`;
    case 'once':
    default:
      return 'One-time';
  }
}

function formatCompletionDate(date: Date): string {
  const now = new Date();
  const d = new Date(date);
  const diff = now.getTime() - d.getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));
  
  const time = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  
  if (days === 0) return `Today at ${time}`;
  if (days === 1) return `Yesterday at ${time}`;
  if (days < 7) return `${days} days ago`;
  return d.toLocaleDateString([], { month: 'short', day: 'numeric' });
}

export function ChoreCard({
  chore,
  members,
  currentMemberId,
  onComplete,
  onAssign,
  onDelete,
}: ChoreCardProps) {
  const [selectedMembers, setSelectedMembers] = useState<string[]>(chore.assignedTo || []);
  const [assignToAll, setAssignToAll] = useState(chore.assignedToAll || false);
  const [isExpanded, setIsExpanded] = useState(false);
  const [history, setHistory] = useState<ChoreCompletion[]>([]);
  const [historyLoaded, setHistoryLoaded] = useState(false);
  
  const fetchChoreHistory = useHouseholdStore((s) => s.fetchChoreHistory);
  
  const assignedMembers = members.filter((m) => chore.assignedTo?.includes(m.id));
  const isRecurring = chore.frequency && chore.frequency.type !== 'once';
  
  const currentUserCompletion = chore.memberCompletions?.find(mc => mc.memberId === currentMemberId);
  const currentUserCompletedToday = currentUserCompletion?.completedToday ?? false;

  // Load history when expanded
  useEffect(() => {
    if (isExpanded && !historyLoaded) {
      fetchChoreHistory(chore.householdId, chore.id).then((completions) => {
        setHistory(completions);
        setHistoryLoaded(true);
      });
    }
  }, [isExpanded, historyLoaded, chore.householdId, chore.id, fetchChoreHistory]);

  const handleAssignmentChange = (memberId: string, checked: boolean) => {
    let newSelection: string[];
    if (checked) {
      newSelection = [...selectedMembers, memberId];
    } else {
      newSelection = selectedMembers.filter(id => id !== memberId);
    }
    setSelectedMembers(newSelection);
    setAssignToAll(false);
    onAssign(newSelection.length > 0 ? newSelection : undefined, false);
  };

  const handleAssignToAll = () => {
    const allMemberIds = members.map(m => m.id);
    setSelectedMembers(allMemberIds);
    setAssignToAll(true);
    onAssign(allMemberIds, true);
  };

  const handleUnassign = () => {
    setSelectedMembers([]);
    setAssignToAll(false);
    onAssign(undefined, false);
  };

  return (
    <Collapsible open={isExpanded} onOpenChange={setIsExpanded}>
      <div
        className={cn(
          'card-elevated transition-all duration-200 animate-scale-in',
          chore.completed && !isRecurring && 'opacity-60'
        )}
      >
        <div className="p-4 flex items-center gap-4">
          {/* Complete button */}
          {isRecurring ? (
            <Button
              size="sm"
              variant={currentUserCompletedToday ? "secondary" : "outline"}
              onClick={onComplete}
              className="flex-shrink-0 gap-1"
              disabled={currentUserCompletedToday}
            >
              <Check className="w-4 h-4" />
              {currentUserCompletedToday ? 'Done' : 'Done'}
            </Button>
          ) : (
            <button
              onClick={chore.completed ? undefined : onComplete}
              disabled={chore.completed}
              className={cn(
                'w-7 h-7 rounded-full border-2 flex items-center justify-center transition-all duration-200 flex-shrink-0',
                chore.completed
                  ? 'bg-success border-success text-success-foreground cursor-default'
                  : 'border-muted-foreground/30 hover:border-primary hover:bg-primary/10 cursor-pointer'
              )}
            >
              {chore.completed && <Check className="w-4 h-4" />}
            </button>
          )}

          <div className="flex-1 min-w-0">
            <h3
              className={cn(
                'font-semibold text-foreground transition-all',
                chore.completed && !isRecurring && 'line-through text-muted-foreground'
              )}
            >
              {chore.displayName}
            </h3>
            <div className="flex items-center gap-3 text-sm text-muted-foreground">
              {chore.description && (
                <span className="truncate">{chore.description}</span>
              )}
            </div>
            <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
              {chore.frequency && (
                <span className="flex items-center gap-1">
                  <Repeat className="w-3 h-3" />
                  {formatFrequency(chore.frequency)}
                </span>
              )}
            </div>
            
            {/* Multi-user completion status */}
            {isRecurring && (chore.assignedToAll || assignedMembers.length > 1) && chore.memberCompletions && (
              <div className="flex items-center gap-2 mt-2 flex-wrap">
                {chore.memberCompletions.map(mc => {
                  const member = members.find(m => m.id === mc.memberId);
                  if (!member) return null;
                  return (
                    <div 
                      key={mc.memberId}
                      className={cn(
                        "flex items-center gap-1 px-2 py-1 rounded-full text-xs",
                        mc.completedToday 
                          ? "bg-success/20 text-success" 
                          : "bg-muted text-muted-foreground"
                      )}
                    >
                      <MemberAvatar nickname={member.nickname} color={member.avatarColor} size="xs" />
                      <span>{member.nickname}</span>
                      {mc.completedToday && <Check className="w-3 h-3" />}
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          <div className="flex items-center gap-2">
            {/* Expand button for history */}
            <CollapsibleTrigger asChild>
              <button 
                className="p-2 rounded-full hover:bg-muted transition-colors text-muted-foreground"
                title="View history"
              >
                <ChevronDown className={cn(
                  "w-4 h-4 transition-transform",
                  isExpanded && "rotate-180"
                )} />
              </button>
            </CollapsibleTrigger>

            {/* Assignment dropdown */}
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <button className="p-1 rounded-full hover:bg-muted transition-colors">
                  {assignedMembers.length > 1 || chore.assignedToAll ? (
                    <div className="w-8 h-8 rounded-full bg-primary/20 flex items-center justify-center">
                      <Users className="w-4 h-4 text-primary" />
                    </div>
                  ) : assignedMembers.length === 1 ? (
                    <MemberAvatar
                      nickname={assignedMembers[0].nickname}
                      color={assignedMembers[0].avatarColor}
                      size="sm"
                    />
                  ) : (
                    <div className="w-8 h-8 rounded-full border-2 border-dashed border-muted-foreground/30 flex items-center justify-center">
                      <User className="w-4 h-4 text-muted-foreground/50" />
                    </div>
                  )}
                </button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-48">
                <DropdownMenuItem onClick={handleUnassign}>
                  <User className="w-4 h-4 mr-2" />
                  Unassigned
                </DropdownMenuItem>
                <DropdownMenuItem onClick={handleAssignToAll}>
                  <Users className="w-4 h-4 mr-2" />
                  Everyone
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {members.map((member) => (
                  <DropdownMenuCheckboxItem
                    key={member.id}
                    checked={selectedMembers.includes(member.id)}
                    onCheckedChange={(checked) => handleAssignmentChange(member.id, checked)}
                  >
                    <MemberAvatar
                      nickname={member.nickname}
                      color={member.avatarColor}
                      size="sm"
                      className="mr-2"
                    />
                    {member.nickname}
                  </DropdownMenuCheckboxItem>
                ))}
              </DropdownMenuContent>
            </DropdownMenu>

            <button
              onClick={onDelete}
              className="p-2 rounded-full hover:bg-destructive/10 text-muted-foreground hover:text-destructive transition-colors"
            >
              <Trash2 className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Collapsible history section */}
        <CollapsibleContent>
          <div className="px-4 pb-4 pt-2 border-t border-border">
            <div className="flex items-center gap-2 mb-3 text-sm font-medium text-muted-foreground">
              <History className="w-4 h-4" />
              Completion History
            </div>
            {history.length === 0 ? (
              <p className="text-sm text-muted-foreground italic">No completions yet</p>
            ) : (
              <div className="space-y-2 max-h-48 overflow-y-auto">
                {history.slice(0, 10).map((completion, idx) => {
                  const member = members.find(m => m.id === completion.completedBy);
                  return (
                    <div 
                      key={idx} 
                      className="flex items-center gap-3 text-sm"
                    >
                      {member ? (
                        <MemberAvatar 
                          nickname={member.nickname} 
                          color={member.avatarColor} 
                          size="xs" 
                        />
                      ) : (
                        <div className="w-5 h-5 rounded-full bg-muted" />
                      )}
                      <span className="font-medium">
                        {member?.nickname ?? 'Unknown'}
                      </span>
                      <span className="text-muted-foreground">
                        {formatCompletionDate(completion.completedAt)}
                      </span>
                    </div>
                  );
                })}
                {history.length > 10 && (
                  <p className="text-xs text-muted-foreground">
                    +{history.length - 10} more...
                  </p>
                )}
              </div>
            )}
          </div>
        </CollapsibleContent>
      </div>
    </Collapsible>
  );
}
