import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { Household, Member, Chore, Invite, ChoreFrequency, MemberCompletion, ChoreCompletion, MemberOverdue, OverdueChore, MyChoresResponse, MyPendingChore, MyOverdueChore, MyCompletedChore } from '@/types/household';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

interface HouseholdState {
  households: Household[];
  members: Member[];
  chores: Chore[];
  invites: Invite[];
  currentHouseholdId: string | null;
  currentMemberId: string | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  currentPinCode: string | null;
  isLoading: boolean;
  error: string | null;

  // Commands (now async)
  createHousehold: (name: string, pinCode: string, ownerNickname?: string, memberPinCode?: string) => Promise<Household | null>;
  addChore: (householdId: string, displayName: string, description: string, frequency?: ChoreFrequency, isOptional?: boolean, startDate?: Date) => Promise<Chore | null>;
  generateInvite: (householdId: string) => Promise<Invite | null>;
  joinHousehold: (householdId: string, inviteId: string, nickname: string) => Promise<Member | null>;
  accessHousehold: (householdId: string, pinCode: string) => Promise<boolean>;
  toggleChoreComplete: (choreId: string) => void;
  completeChore: (householdId: string, choreId: string, memberId: string, completedAt?: Date) => Promise<void>;
  assignChore: (householdId: string, choreId: string, memberIds?: string[], assignToAll?: boolean) => Promise<void>;
  deleteChore: (householdId: string, choreId: string) => Promise<boolean>;
  changeNickname: (householdId: string, memberId: string, newNickname: string) => Promise<boolean>;
  changeStatus: (householdId: string, memberId: string, status: string) => Promise<boolean>;
  setCurrentMember: (memberId: string) => void;
  logout: () => void;

  // Queries (now async for API fetching)
  fetchHousehold: (id: string) => Promise<Household | null>;
  fetchHouseholdChores: (householdId: string) => Promise<Chore[]>;
  fetchInvite: (householdId: string) => Promise<Invite | null>;

  // Queries (fetch from API)
  getHousehold: (id: string) => Promise<Household | null>;
  getHouseholdChores: (householdId: string) => Promise<Chore[]>;
  fetchHouseholdMembers: (householdId: string) => Promise<Member[]>;
  fetchChoreHistory: (householdId: string, choreId: string) => Promise<ChoreCompletion[]>;
  fetchOverdueChores: (householdId: string) => Promise<MemberOverdue[]>;
  fetchMyChores: (householdId: string, memberId: string) => Promise<MyChoresResponse | null>;
  acknowledgeMissed: (householdId: string, choreId: string, memberId: string, period: string) => Promise<boolean>;
  removeMember: (householdId: string, memberId: string, removedByMemberId: string) => Promise<boolean>;

  // Local getters (for cached data)
  getHouseholdMembers: (householdId: string) => Member[];
  getInviteByCode: (inviteId: string) => (Invite & { householdName: string }) | null;
}

const AVATAR_COLORS = [
  'hsl(150 50% 50%)',
  'hsl(15 80% 60%)',
  'hsl(200 70% 55%)',
  'hsl(45 90% 55%)',
  'hsl(280 60% 60%)',
  'hsl(340 70% 60%)',
];

export const useHouseholdStore = create<HouseholdState>()(
  persist(
    (set, get) => ({
      households: [],
      members: [],
      chores: [],
      invites: [],
      currentHouseholdId: null,
      currentMemberId: null,
      isAuthenticated: false,
      isAdmin: false,
      currentPinCode: null,
      isLoading: false,
      error: null,

      createHousehold: async (name, pinCode, ownerNickname = 'Admin', memberPinCode) => {
        set({ isLoading: true, error: null });

        try {
          const body: Record<string, unknown> = { 
            name, 
            pinCode: parseInt(pinCode, 10), 
            ownerNickname 
          };
          if (memberPinCode) {
            body.memberPinCode = parseInt(memberPinCode, 10);
          }
          
          const response = await fetch(`${API_BASE_URL}/api/households`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
          });

          if (!response.ok) {
            throw new Error('Failed to create household');
          }

          const data = await response.json();
          // API returns { householdId, memberId, name }
          const householdId = data.householdId;
          const memberId = data.memberId;

          const household: Household = {
            id: householdId,
            name: name,
            pinCode,
            createdAt: new Date(),
          };

          const member: Member = {
            id: memberId,
            householdId: household.id,
            nickname: ownerNickname,
            avatarColor: AVATAR_COLORS[0],
            joinedAt: new Date(),
          };

          set((state) => ({
            households: [...state.households, household],
            members: [...state.members, member],
            currentHouseholdId: household.id,
            currentMemberId: member.id,
            isAuthenticated: true,
            isLoading: false,
          }));

          return household;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return null;
        }
      },

      addChore: async (householdId, displayName, description, frequency, isOptional = false, startDate) => {
        set({ isLoading: true, error: null });

        try {
          const body: Record<string, unknown> = { displayName, description, isOptional };
          if (frequency) {
            body.frequency = {
              type: frequency.type,
              days: frequency.days,
              intervalDays: frequency.intervalDays,
            };
          }
          if (startDate) {
            body.startDate = startDate.toISOString();
          }

          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/chores`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
          });

          if (!response.ok) {
            throw new Error('Failed to add chore');
          }

          const chore: Chore = {
            id: crypto.randomUUID(),
            householdId,
            displayName,
            description,
            completed: false,
            createdAt: new Date(),
            frequency: frequency || { type: 'once' },
            isOptional,
          };

          set((state) => ({
            chores: [...state.chores, chore],
            isLoading: false,
          }));

          return chore;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return null;
        }
      },

      generateInvite: async (householdId) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/invite`, {
            method: 'POST',
          });

          if (!response.ok) {
            throw new Error('Failed to generate invite');
          }

          const data = await response.json();
          // API returns { householdId, inviteId, link }
          // Link from API is relative path like /join/{householdId}/{inviteId}
          const fullLink = data.link?.startsWith('http') 
            ? data.link 
            : `${window.location.origin}${data.link || `/join/${data.householdId}/${data.inviteId}`}`;
          
          const invite: Invite = {
            id: data.inviteId,
            householdId: data.householdId || householdId,
            link: fullLink,
            createdAt: new Date(),
            expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
          };

          set((state) => ({
            invites: [...state.invites, invite],
            isLoading: false,
          }));

          return invite;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return null;
        }
      },

      joinHousehold: async (householdId, inviteId, nickname) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/join`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ inviteId, nickname }),
          });

          if (!response.ok) {
            throw new Error('Failed to join household');
          }

          const data = await response.json();
          // API returns { memberId, householdId, nickname }
          const existingMembers = get().members.filter(
            (m) => m.householdId === householdId
          );

          const member: Member = {
            id: data.memberId,
            householdId: householdId,
            nickname: data.nickname || nickname,
            avatarColor: AVATAR_COLORS[existingMembers.length % AVATAR_COLORS.length],
            joinedAt: new Date(),
          };

          set((state) => ({
            members: [...state.members, member],
            currentHouseholdId: householdId,
            currentMemberId: member.id,
            isAuthenticated: true,
            isLoading: false,
          }));

          return member;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return null;
        }
      },

      accessHousehold: async (householdId, pinCode) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/access`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ pinCode: parseInt(pinCode, 10) }),
          });

          if (!response.ok) {
            set({ isLoading: false, error: 'Invalid PIN' });
            return false;
          }

          const data = await response.json();

          if (data.success) {
            set({ 
              currentHouseholdId: householdId, 
              isAuthenticated: true, 
              isAdmin: data.isAdmin ?? false,
              currentPinCode: pinCode,
              isLoading: false 
            });
            return true;
          }

          set({ isLoading: false, error: 'Invalid PIN' });
          return false;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return false;
        }
      },

      toggleChoreComplete: (choreId) => {
        set((state) => ({
          chores: state.chores.map((c) =>
            c.id === choreId ? { ...c, completed: !c.completed } : c
          ),
        }));
      },

      completeChore: async (householdId, choreId, memberId, completedAt) => {
        try {
          const body: Record<string, unknown> = { memberId };
          if (completedAt) {
            body.completedAt = completedAt.toISOString();
          }

          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/complete`,
            {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify(body),
            }
          );

          if (!response.ok) {
            throw new Error('Failed to complete chore');
          }

          const data = await response.json();
          
          set((state) => ({
            chores: state.chores.map((c) =>
              c.id === choreId
                ? {
                    ...c,
                    lastCompletedAt: new Date(data.completedAt),
                    lastCompletedBy: data.completedBy,
                  }
                : c
            ),
          }));
        } catch (error) {
          console.error('Failed to complete chore', error);
        }
      },

      assignChore: async (householdId, choreId, memberIds, assignToAll = false) => {
        const { currentMemberId } = get();
        try {
          await fetch(`${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/assign`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ 
              memberIds: memberIds || null, 
              assignToAll,
              assignedByMemberId: currentMemberId 
            }),
          });

          set((state) => ({
            chores: state.chores.map((c) =>
              c.id === choreId ? { ...c, assignedTo: memberIds, assignedToAll: assignToAll } : c
            ),
          }));
        } catch (error) {
          console.error('Failed to assign chore', error);
        }
      },

      deleteChore: async (householdId, choreId) => {
        const { currentPinCode, isAdmin } = get();
        if (!isAdmin || !currentPinCode) {
          console.error('Must be admin to delete chores');
          return false;
        }

        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/delete`,
            {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ pinCode: parseInt(currentPinCode, 10) }),
            }
          );

          if (response.ok) {
            set((state) => ({
              chores: state.chores.filter((c) => c.id !== choreId),
            }));
            return true;
          }
          return false;
        } catch (error) {
          console.error('Failed to delete chore', error);
          return false;
        }
      },

      changeNickname: async (householdId, memberId, newNickname) => {
        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/members/${memberId}/nickname`,
            {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ nickname: newNickname }),
            }
          );

          if (response.ok) {
            // Update local member data
            set((state) => ({
              members: state.members.map((m) =>
                m.id === memberId ? { ...m, nickname: newNickname } : m
              ),
            }));
            return true;
          }
          return false;
        } catch (error) {
          console.error('Failed to change nickname', error);
          return false;
        }
      },

      changeStatus: async (householdId, memberId, status) => {
        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/members/${memberId}/status`,
            {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ status }),
            }
          );

          if (response.ok) {
            // Update local member data
            set((state) => ({
              members: state.members.map((m) =>
                m.id === memberId ? { ...m, status } : m
              ),
            }));
            return true;
          }
          return false;
        } catch (error) {
          console.error('Failed to change status', error);
          return false;
        }
      },

      setCurrentMember: (memberId) => {
        set({ currentMemberId: memberId });
      },

      logout: () => {
        set({
          isAuthenticated: false,
          isAdmin: false,
          currentHouseholdId: null,
          currentMemberId: null,
          currentPinCode: null,
        });
      },

      fetchHousehold: async (id) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${id}`);

          if (!response.ok) {
            throw new Error('Failed to fetch household');
          }

          const data = await response.json();
          // API may return { household: {...} } or direct object
          const householdData = data.household ?? data;
          // Map API response (householdId) to frontend type (id)
          const household: Household = {
            id: householdData.householdId ?? householdData.id ?? id,
            name: householdData.householdName ?? householdData.name ?? '',
            pinCode: householdData.pinCode ?? '',
            createdAt: householdData.createdAt ? new Date(householdData.createdAt) : new Date(),
          };

          set((state) => ({
            households: state.households.some((h) => h.id === id)
              ? state.households.map((h) => (h.id === id ? household : h))
              : [...state.households, household],
            isLoading: false,
          }));

          return household;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return null;
        }
      },

      fetchHouseholdChores: async (householdId) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/chores`);

          if (!response.ok) {
            throw new Error('Failed to fetch chores');
          }

          const data = await response.json();
          // API returns { chores: [...] } or direct array
          const choreArray = Array.isArray(data) ? data : (data.chores ?? []);
          // Map API response to frontend type
          const chores: Chore[] = choreArray.map((c: Record<string, unknown>) => {
            const frequency = c.frequency as ChoreFrequency | undefined;
            const lastCompletedAt = c.lastCompletedAt ? new Date(c.lastCompletedAt as string) : undefined;
            // One-time chores are "completed" once they have any completion
            const isOneTime = !frequency || frequency.type === 'once';
            const completed = isOneTime && lastCompletedAt != null;
            
            return {
              id: (c.choreId ?? c.id) as string,
              householdId: (c.householdId ?? householdId) as string,
              displayName: c.displayName as string,
              description: c.description as string,
              assignedTo: c.assignedTo as string[] | undefined,
              assignedToAll: c.assignedToAll as boolean | undefined,
              completed,
              createdAt: c.createdAt ? new Date(c.createdAt as string) : new Date(),
              frequency,
              lastCompletedAt,
              lastCompletedBy: c.lastCompletedBy as string | undefined,
              memberCompletions: c.memberCompletions as MemberCompletion[] | undefined,
              isOptional: c.isOptional as boolean | undefined,
            };
          });
          set((state) => ({
            chores: [
              ...state.chores.filter((c) => c.householdId !== householdId),
              ...chores,
            ],
            isLoading: false,
          }));

          return chores;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return [];
        }
      },

      fetchInvite: async (householdId) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/invite`);

          if (!response.ok) {
            throw new Error('Failed to fetch invite');
          }

          const invite = await response.json();

          set((state) => ({
            invites: state.invites.some((i) => i.householdId === householdId)
              ? state.invites.map((i) => (i.householdId === householdId ? invite : i))
              : [...state.invites, invite],
            isLoading: false,
          }));

          return invite;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return null;
        }
      },

      getHousehold: (id) => get().fetchHousehold(id),

      getHouseholdChores: (householdId) => get().fetchHouseholdChores(householdId),

      fetchHouseholdMembers: async (householdId) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/members`);

          if (!response.ok) {
            throw new Error('Failed to fetch members');
          }

          const data = await response.json();
          // API returns { members: [...] } or direct array
          const memberArray = Array.isArray(data) ? data : (data.members ?? []);
          // Map API response to frontend type
          const members: Member[] = memberArray.map((m: Record<string, unknown>, index: number) => ({
            id: (m.memberId ?? m.id) as string,
            householdId: householdId,
            nickname: m.nickname as string,
            avatarColor: AVATAR_COLORS[index % AVATAR_COLORS.length],
            joinedAt: m.joinedAt ? new Date(m.joinedAt as string) : new Date(),
            status: m.status as string | undefined,
          }));

          set((state) => ({
            members: [
              ...state.members.filter((m) => m.householdId !== householdId),
              ...members,
            ],
            isLoading: false,
          }));

          return members;
        } catch (error) {
          set({ isLoading: false, error: (error as Error).message });
          return [];
        }
      },

      fetchChoreHistory: async (householdId, choreId) => {
        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/history`
          );
          if (!response.ok) {
            throw new Error('Failed to fetch chore history');
          }
          const data = await response.json();
          const completions = (data.completions ?? []).map((c: Record<string, unknown>) => ({
            completedBy: c.completedBy as string,
            completedAt: new Date(c.completedAt as string),
          }));
          return completions;
        } catch (error) {
          console.error('Failed to fetch chore history', error);
          return [];
        }
      },

      fetchOverdueChores: async (householdId) => {
        const { currentPinCode } = get();
        if (!currentPinCode) {
          return []; // No PIN, can't fetch overdue
        }
        
        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/overdue`,
            {
              headers: {
                'X-Pin-Code': currentPinCode,
              },
            }
          );
          
          // 401/403 = not admin, return empty (not an error)
          if (response.status === 401 || response.status === 403) {
            return [];
          }
          
          if (!response.ok) {
            throw new Error('Failed to fetch overdue chores');
          }
          const data = await response.json();
          const memberOverdue: MemberOverdue[] = (data.memberOverdue ?? []).map(
            (m: Record<string, unknown>) => ({
              memberId: m.memberId as string,
              nickname: m.nickname as string,
              overdueCount: m.overdueCount as number,
              chores: ((m.chores as Record<string, unknown>[]) ?? []).map(
                (c: Record<string, unknown>) => ({
                  choreId: c.choreId as string,
                  displayName: c.displayName as string,
                  overduePeriod: c.overduePeriod as string,
                  lastCompleted: c.lastCompleted
                    ? new Date(c.lastCompleted as string)
                    : undefined,
                })
              ),
            })
          );
          return memberOverdue;
        } catch (error) {
          console.error('Failed to fetch overdue chores', error);
          return [];
        }
      },

      fetchMyChores: async (householdId, memberId) => {
        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/my-chores?memberId=${memberId}`
          );
          
          if (!response.ok) {
            return null;
          }
          
          const data = await response.json();
          
          const result: MyChoresResponse = {
            pending: (data.pending ?? []).map((c: Record<string, unknown>) => ({
              choreId: c.choreId as string,
              displayName: c.displayName as string,
              frequencyType: c.frequencyType as string | undefined,
              dueDescription: c.dueDescription as string | undefined,
            })),
            overdue: (data.overdue ?? []).map((c: Record<string, unknown>) => ({
              choreId: c.choreId as string,
              displayName: c.displayName as string,
              frequencyType: c.frequencyType as string | undefined,
              overduePeriod: c.overduePeriod as string,
              periodKey: c.periodKey as string,
            })),
            completed: (data.completed ?? []).map((c: Record<string, unknown>) => ({
              choreId: c.choreId as string,
              displayName: c.displayName as string,
              completedAt: new Date(c.completedAt as string),
            })),
          };
          
          return result;
        } catch (error) {
          console.error('Failed to fetch my chores', error);
          return null;
        }
      },

      acknowledgeMissed: async (householdId, choreId, memberId, period) => {
        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/chores/${choreId}/acknowledge-missed`,
            {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ memberId, period }),
            }
          );
          return response.ok;
        } catch (error) {
          console.error('Failed to acknowledge missed chore', error);
          return false;
        }
      },

      removeMember: async (householdId, memberId, removedByMemberId) => {
        const { currentPinCode } = get();
        if (!currentPinCode) {
          return false;
        }
        
        try {
          const response = await fetch(
            `${API_BASE_URL}/api/households/${householdId}/members/${memberId}/remove`,
            {
              method: 'POST',
              headers: {
                'Content-Type': 'application/json',
                'X-Pin-Code': currentPinCode,
              },
              body: JSON.stringify({ removedByMemberId }),
            }
          );
          
          if (response.ok) {
            // Remove from local state
            set((state) => ({
              members: state.members.filter((m) => m.id !== memberId),
            }));
            return true;
          }
          return false;
        } catch (error) {
          console.error('Failed to remove member', error);
          return false;
        }
      },

      getHouseholdMembers: (householdId) => {
        const allMembers = get().members;
        if (!Array.isArray(allMembers)) return [];
        return allMembers.filter((m) => m.householdId === householdId);
      },

      getInviteByCode: (inviteId) => {
        const invite = get().invites.find((i) => i.id === inviteId);
        if (!invite) return null;
        const household = get().households.find((h) => h.id === invite.householdId);
        if (!household) return null;
        return { ...invite, householdName: household.name };
      },
    }),
    {
      name: 'household-storage',
      // Ensure arrays are never null when restoring from localStorage
      merge: (persistedState, currentState) => {
        const persisted = persistedState as Partial<HouseholdState> | undefined;
        return {
          ...currentState,
          ...persisted,
          // Ensure arrays are always arrays, never null
          households: persisted?.households ?? [],
          members: persisted?.members ?? [],
          chores: persisted?.chores ?? [],
          invites: persisted?.invites ?? [],
        };
      },
    }
  )
);
