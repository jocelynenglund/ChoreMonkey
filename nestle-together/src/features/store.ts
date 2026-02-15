import { create } from 'zustand';
import { persist } from 'zustand/middleware';

import * as householdApi from './household/api';
import * as membersApi from './members/api';
import * as choresApi from './chores/api';
import * as activityApi from './activity/api';
import * as invitesApi from './invites/api';

import type { Household } from './household/types';
import type { Member, Invite } from './members/types';
import type { Chore, ChoreFrequency, ChoreCompletion, MyChoresResponse, MemberOverdue } from './chores/types';
import type { Activity, MemberOverview } from './activity/types';

const AVATAR_COLORS = [
  'hsl(150 50% 50%)',
  'hsl(15 80% 60%)',
  'hsl(200 70% 55%)',
  'hsl(45 90% 55%)',
  'hsl(280 60% 60%)',
  'hsl(340 70% 60%)',
];

interface AppState {
  // Auth state
  currentHouseholdId: string | null;
  currentMemberId: string | null;
  currentPinCode: string | null;
  isAuthenticated: boolean;
  isAdmin: boolean;
  
  // Cached data
  members: Member[];
  
  // Actions
  createHousehold: (name: string, pinCode: string, ownerNickname?: string, memberPinCode?: string) => Promise<Household | null>;
  accessHousehold: (householdId: string, pinCode: string) => Promise<boolean>;
  joinHousehold: (householdId: string, inviteId: string, nickname: string) => Promise<Member | null>;
  logout: () => void;
  setCurrentMember: (memberId: string) => void;
  
  // Household
  getHousehold: (householdId: string) => Promise<Household | null>;
  
  // Members
  fetchHouseholdMembers: (householdId: string) => Promise<Member[]>;
  getHouseholdMembers: (householdId: string) => Member[];
  generateInvite: (householdId: string) => Promise<Invite | null>;
  changeNickname: (householdId: string, memberId: string, newNickname: string) => Promise<boolean>;
  changeStatus: (householdId: string, memberId: string, status: string) => Promise<boolean>;
  removeMember: (householdId: string, memberId: string, removedByMemberId: string, pinCode?: string) => Promise<boolean>;
  
  // Chores
  getHouseholdChores: (householdId: string) => Promise<Chore[]>;
  addChore: (householdId: string, displayName: string, description: string, frequency?: ChoreFrequency, isOptional?: boolean, startDate?: Date) => Promise<Chore | null>;
  completeChore: (householdId: string, choreId: string, memberId: string, completedAt?: Date) => Promise<void>;
  assignChore: (householdId: string, choreId: string, memberIds?: string[], assignToAll?: boolean) => Promise<void>;
  deleteChore: (householdId: string, choreId: string) => Promise<boolean>;
  fetchChoreHistory: (householdId: string, choreId: string) => Promise<ChoreCompletion[]>;
  
  // My Chores
  fetchMyChores: (householdId: string, memberId: string) => Promise<MyChoresResponse | null>;
  acknowledgeMissed: (householdId: string, choreId: string, memberId: string, period: string) => Promise<boolean>;
  
  // Overdue
  fetchOverdueChores: (householdId: string) => Promise<MemberOverdue[]>;
  
  // Activity
  fetchActivityTimeline: (householdId: string, limit?: number) => Promise<Activity[]>;
  
  // Team Overview (admin only)
  fetchTeamOverview: (householdId: string) => Promise<MemberOverview[]>;
}

export const useAppStore = create<AppState>()(
  persist(
    (set, get) => ({
      currentHouseholdId: null,
      currentMemberId: null,
      currentPinCode: null,
      isAuthenticated: false,
      isAdmin: false,
      members: [],

      createHousehold: async (name, pinCode, ownerNickname = 'Admin', memberPinCode) => {
        try {
          const request: Parameters<typeof householdApi.createHousehold>[0] = {
            name,
            pinCode: parseInt(pinCode, 10),
            ownerNickname,
          };
          if (memberPinCode) {
            request.memberPinCode = parseInt(memberPinCode, 10);
          }

          const data = await householdApi.createHousehold(request);

          const household: Household = {
            id: data.householdId,
            name,
            pinCode,
            createdAt: new Date(),
          };

          const member: Member = {
            id: data.memberId,
            householdId: data.householdId,
            nickname: ownerNickname,
            avatarColor: AVATAR_COLORS[0],
            joinedAt: new Date(),
          };

          set({
            currentHouseholdId: data.householdId,
            currentMemberId: data.memberId,
            isAuthenticated: true,
            isAdmin: true,
            members: [member],
          });

          return household;
        } catch {
          return null;
        }
      },

      accessHousehold: async (householdId, pinCode) => {
        try {
          const data = await householdApi.accessHousehold(householdId, parseInt(pinCode, 10));

          if (data.success) {
            set({
              currentHouseholdId: householdId,
              currentMemberId: data.memberId || null,
              currentPinCode: pinCode,
              isAuthenticated: true,
              isAdmin: data.isAdmin ?? false,
            });
            return true;
          }
          return false;
        } catch {
          return false;
        }
      },

      joinHousehold: async (householdId, inviteId, nickname) => {
        try {
          const data = await membersApi.joinHousehold(householdId, { inviteId, nickname });
          const existingMembers = get().members.filter(m => m.householdId === householdId);

          const member: Member = {
            id: data.memberId,
            householdId,
            nickname: data.nickname || nickname,
            avatarColor: AVATAR_COLORS[existingMembers.length % AVATAR_COLORS.length],
            joinedAt: new Date(),
          };

          set(state => ({
            members: [...state.members, member],
            currentHouseholdId: householdId,
            currentMemberId: member.id,
            isAuthenticated: true,
          }));

          return member;
        } catch {
          return null;
        }
      },

      logout: () => {
        set({
          currentHouseholdId: null,
          currentMemberId: null,
          currentPinCode: null,
          isAuthenticated: false,
          isAdmin: false,
        });
      },

      setCurrentMember: (memberId) => {
        set({ currentMemberId: memberId });
      },

      getHousehold: (householdId) => householdApi.getHousehold(householdId),

      fetchHouseholdMembers: async (householdId) => {
        const members = await membersApi.fetchMembers(householdId);
        set({ members });
        return members;
      },

      getHouseholdMembers: (householdId) => {
        return get().members.filter(m => m.householdId === householdId);
      },

      generateInvite: async (householdId) => {
        try {
          const data = await membersApi.generateInvite(householdId);
          const fullLink = data.link?.startsWith('http')
            ? data.link
            : `${window.location.origin}${data.link || `/join/${data.householdId}/${data.inviteId}`}`;

          return {
            id: data.inviteId,
            householdId: data.householdId || householdId,
            link: fullLink,
            createdAt: new Date(),
            expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000),
          };
        } catch {
          return null;
        }
      },

      changeNickname: (householdId, memberId, newNickname) =>
        membersApi.changeNickname(householdId, memberId, newNickname),

      changeStatus: (householdId, memberId, status) =>
        membersApi.changeStatus(householdId, memberId, status),

      removeMember: async (householdId, memberId, removedByMemberId, pinCode) => {
        const pin = pinCode || get().currentPinCode;
        if (!pin) return false;
        return membersApi.removeMember(householdId, memberId, parseInt(pin, 10), removedByMemberId);
      },

      getHouseholdChores: (householdId) => choresApi.fetchChores(householdId),

      addChore: async (householdId, displayName, description, frequency, isOptional, startDate) => {
        try {
          await choresApi.addChore(householdId, {
            displayName,
            description,
            frequency,
            isOptional,
            startDate: startDate?.toISOString(),
          });

          // Return a minimal chore object - full data comes from refresh
          return {
            id: crypto.randomUUID(),
            householdId,
            displayName,
            description,
            completed: false,
            createdAt: new Date(),
            frequency: frequency || { type: 'once' },
            isOptional,
          };
        } catch {
          return null;
        }
      },

      completeChore: async (householdId, choreId, memberId, completedAt) => {
        await choresApi.completeChore(householdId, choreId, memberId, completedAt);
      },

      assignChore: async (householdId, choreId, memberIds, assignToAll) => {
        const { currentMemberId } = get();
        await choresApi.assignChore(householdId, choreId, {
          memberIds,
          assignToAll,
          assignedByMemberId: currentMemberId || undefined,
        });
      },

      deleteChore: async (householdId, choreId) => {
        const { currentPinCode, isAdmin } = get();
        if (!isAdmin || !currentPinCode) return false;
        return choresApi.deleteChore(householdId, choreId, parseInt(currentPinCode, 10));
      },

      fetchChoreHistory: (householdId, choreId) =>
        choresApi.fetchChoreHistory(householdId, choreId),

      fetchMyChores: (householdId, memberId) =>
        choresApi.fetchMyChores(householdId, memberId),

      acknowledgeMissed: (householdId, choreId, memberId, period) =>
        choresApi.acknowledgeMissed(householdId, choreId, memberId, period),

      fetchOverdueChores: async (householdId) => {
        const { currentPinCode, isAdmin } = get();
        if (!isAdmin || !currentPinCode) return [];
        return choresApi.fetchOverdueChores(householdId, parseInt(currentPinCode, 10));
      },

      fetchActivityTimeline: (householdId, limit) =>
        activityApi.fetchActivityTimeline(householdId, limit),

      fetchTeamOverview: async (householdId) => {
        const { currentPinCode, isAdmin } = get();
        if (!isAdmin || !currentPinCode) return [];
        return activityApi.fetchTeamOverview(householdId, parseInt(currentPinCode, 10));
      },
    }),
    {
      name: 'choremonkey-storage',
      partialize: (state) => ({
        currentHouseholdId: state.currentHouseholdId,
        currentMemberId: state.currentMemberId,
        currentPinCode: state.currentPinCode,
        isAuthenticated: state.isAuthenticated,
        isAdmin: state.isAdmin,
      }),
    }
  )
);

// Alias for backwards compatibility during migration
export const useHouseholdStore = useAppStore;
