import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { Household, Member, Chore, Invite } from '@/types/household';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

interface HouseholdState {
  households: Household[];
  members: Member[];
  chores: Chore[];
  invites: Invite[];
  currentHouseholdId: string | null;
  currentMemberId: string | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;

  // Commands (now async)
  createHousehold: (name: string, pinCode: string) => Promise<Household | null>;
  addChore: (householdId: string, displayName: string, description: string) => Promise<Chore | null>;
  generateInvite: (householdId: string) => Promise<Invite | null>;
  joinHousehold: (inviteId: string, nickname: string) => Member | null;
  accessHousehold: (householdId: string, pinCode: string) => Promise<boolean>;
  toggleChoreComplete: (choreId: string) => void;
  assignChore: (choreId: string, memberId: string | undefined) => void;
  deleteChore: (choreId: string) => void;
  setCurrentMember: (memberId: string) => void;
  logout: () => void;

  // Queries (now async for API fetching)
  fetchHousehold: (id: string) => Promise<Household | null>;
  fetchHouseholdChores: (householdId: string) => Promise<Chore[]>;
  fetchInvite: (householdId: string) => Promise<Invite | null>;

  // Queries (fetch from API)
  getHousehold: (id: string) => Promise<Household | null>;
  getHouseholdChores: (householdId: string) => Promise<Chore[]>;

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
      isLoading: false,
      error: null,

      createHousehold: async (name, pinCode) => {
        const householdId = crypto.randomUUID();
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ householdId, name, pinCode: parseInt(pinCode, 10) }),
          });

          if (!response.ok) {
            throw new Error('Failed to create household');
          }

          const household: Household = {
            id: householdId,
            name: name,
            pinCode,
            createdAt: new Date(),
          };

          const member: Member = {
            id: crypto.randomUUID(),
            householdId: household.id,
            nickname: 'Admin',
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

      addChore: async (householdId, displayName, description) => {
        set({ isLoading: true, error: null });

        try {
          const response = await fetch(`${API_BASE_URL}/api/households/${householdId}/chores`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ displayName, description }),
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

          const inviteId = Math.random().toString(36).substring(2, 8).toUpperCase();
          const invite: Invite = {
            id: inviteId,
            householdId,
            link: `${window.location.origin}/join/${inviteId}`,
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

      joinHousehold: (inviteId, nickname) => {
        const invite = get().invites.find((i) => i.id === inviteId);
        if (!invite) return null;

        const existingMembers = get().members.filter(
          (m) => m.householdId === invite.householdId
        );

        const member: Member = {
          id: crypto.randomUUID(),
          householdId: invite.householdId,
          nickname,
          avatarColor: AVATAR_COLORS[existingMembers.length % AVATAR_COLORS.length],
          joinedAt: new Date(),
        };

        set((state) => ({
          members: [...state.members, member],
          currentHouseholdId: invite.householdId,
          currentMemberId: member.id,
          isAuthenticated: true,
        }));

        return member;
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
            set({ currentHouseholdId: householdId, isAuthenticated: true, isLoading: false });
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

      assignChore: (choreId, memberId) => {
        set((state) => ({
          chores: state.chores.map((c) =>
            c.id === choreId ? { ...c, assignedTo: memberId } : c
          ),
        }));
      },

      deleteChore: (choreId) => {
        set((state) => ({
          chores: state.chores.filter((c) => c.id !== choreId),
        }));
      },

      setCurrentMember: (memberId) => {
        set({ currentMemberId: memberId });
      },

      logout: () => {
        set({
          isAuthenticated: false,
          currentHouseholdId: null,
          currentMemberId: null,
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
          const chores: Chore[] = choreArray.map((c: Record<string, unknown>) => ({
            id: (c.choreId ?? c.id) as string,
            householdId: (c.householdId ?? householdId) as string,
            displayName: c.displayName as string,
            description: c.description as string,
            assignedTo: c.assignedTo as string | undefined,
            completed: (c.completed ?? false) as boolean,
            createdAt: c.createdAt ? new Date(c.createdAt as string) : new Date(),
          }));
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

      getHouseholdMembers: (householdId) =>
        get().members.filter((m) => m.householdId === householdId),

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
    }
  )
);
