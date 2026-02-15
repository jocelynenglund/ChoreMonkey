export interface Member {
  id: string;
  householdId: string;
  nickname: string;
  avatarColor: string;
  joinedAt: Date;
  status?: string;
}

export interface JoinHouseholdRequest {
  inviteId: string;
  nickname: string;
}

export interface JoinHouseholdResponse {
  memberId: string;
  householdId: string;
  nickname: string;
}

export interface Invite {
  id: string;
  householdId: string;
  link: string;
  createdAt: Date;
  expiresAt: Date;
}

// Re-export from invites for backward compatibility
export type { GenerateInviteResponse } from '../invites/types';
