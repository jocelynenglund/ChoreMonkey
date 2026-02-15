export interface GenerateInviteResponse {
  householdId: string;
  inviteId: string;
  link: string;
}

export interface InviteInfo {
  householdId: string;
  householdName: string;
  inviteId: string;
  isValid: boolean;
}
