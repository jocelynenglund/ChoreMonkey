export interface Household {
  id: string;
  name: string;
  pinCode: string;
  createdAt: Date;
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
