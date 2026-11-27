// What a connected screen such as barrio sees: today's chores and the last 40 days. Never any money.

export interface DisplayChore {
  choreId: string;
  title: string;
  state: 'open' | 'claimed' | 'done';
  by?: string;
}

export interface DisplayToday {
  householdName: string;
  day: string;
  householdUrl: string;
  chores: DisplayChore[];
}

export interface DisplayDay {
  day: string;
  count: number;
}

export interface DisplayPerson {
  name: string;
  days: DisplayDay[];
  total: number;
}

export interface DisplayLately {
  householdName: string;
  from: string;
  to: string;
  people: DisplayPerson[];
}

export interface DisplayLink {
  linkId: string;
  label: string;
  /** null only when the link's secret can no longer be shown */
  url: string | null;
}
