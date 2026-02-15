export interface Activity {
  id: string;
  type: string;
  text: string;
  timestamp: Date;
  memberId?: string;
  choreId?: string;
}
