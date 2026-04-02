export interface Household {
  id: string;
  name: string;
  pinCode: string;
  createdAt: Date;
  slug?: string;
}

export interface SetSlugResponse {
  slug: string;
  url: string;
}

export interface HouseholdBySlugResponse {
  householdId: string;
  name: string;
  memberCount: number;
}

export interface AccessResponse {
  success: boolean;
  householdId: string;
  householdName?: string;
  isAdmin: boolean;
  memberId?: string;
}

export interface CreateHouseholdRequest {
  name: string;
  pinCode: number;
  ownerNickname?: string;
  memberPinCode?: number;
}

export interface CreateHouseholdResponse {
  householdId: string;
  memberId: string;
  inviteId: string;
}
