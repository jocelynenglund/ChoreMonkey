import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { SharedWithBarrio } from './SharedWithBarrio';

const fetchDisplayToday = vi.fn();
const fetchDisplayLately = vi.fn();
const fetchDisplayLinks = vi.fn();
const makeDisplayLink = vi.fn();
const revokeDisplayLink = vi.fn();
vi.mock('../api', () => ({
  fetchDisplayToday: (...a: unknown[]) => fetchDisplayToday(...a),
  fetchDisplayLately: (...a: unknown[]) => fetchDisplayLately(...a),
  fetchDisplayLinks: (...a: unknown[]) => fetchDisplayLinks(...a),
  makeDisplayLink: (...a: unknown[]) => makeDisplayLink(...a),
  revokeDisplayLink: (...a: unknown[]) => revokeDisplayLink(...a),
}));

const days = (counts: number[]) => counts.map((count, i) => ({ day: `2026-03-${String(i + 1).padStart(2, '0')}`, count }));

const today = {
  householdName: 'The Smiths',
  day: '2026-03-18',
  householdUrl: 'https://choremonkey.itsybit.se/h/smiths',
  chores: [
    { choreId: 'c1', title: 'Dishes', state: 'done', by: 'Ann' },
    { choreId: 'c2', title: 'Bins', state: 'open' },
  ],
};
const lately = {
  householdName: 'The Smiths',
  from: '2026-02-07',
  to: '2026-03-18',
  people: [
    { name: 'Ann', total: 6, days: days([0, 1, 2, 3, 0]) },
    { name: 'Bob', total: 0, days: days([0, 0]) },
  ],
};
const link = { linkId: 'l1', label: 'barrio', url: 'https://api.example/display/secret-token' };

function renderIt(props: { isAdmin: boolean; adminPin: string | null }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <SharedWithBarrio householdId="h1" {...props} />
    </QueryClientProvider>,
  );
}

describe('SharedWithBarrio', () => {
  beforeEach(() => {
    [fetchDisplayToday, fetchDisplayLately, fetchDisplayLinks, makeDisplayLink, revokeDisplayLink].forEach((m) => m.mockReset());
    fetchDisplayToday.mockResolvedValue(today);
    fetchDisplayLately.mockResolvedValue(lately);
    fetchDisplayLinks.mockResolvedValue([]);
  });

  it('shows today, done and open, and the last 40 days for every member', async () => {
    renderIt({ isAdmin: false, adminPin: null });

    expect(await screen.findByText('Dishes')).toBeInTheDocument();
    expect(screen.getByText('by Ann')).toBeInTheDocument();
    expect(screen.getByText('Bins')).toBeInTheDocument();
    expect(screen.getByText('The last 40 days')).toBeInTheDocument();
    const rows = await screen.findAllByTestId('lately-row');
    expect(rows).toHaveLength(2);
    expect(rows[0]).toHaveTextContent('Ann');
    expect(rows[0]).toHaveTextContent('6');
    expect(fetchDisplayToday).toHaveBeenCalledWith('h1');
  });

  it('shades a day by how many chores were done, three or more being the darkest', async () => {
    renderIt({ isAdmin: false, adminPin: null });
    const rows = await screen.findAllByTestId('lately-row');
    const squares = Array.from(rows[0].querySelectorAll('[data-count]'));
    expect(squares.map((s) => s.className.match(/bg-\S+/)?.[0])).toEqual(['bg-muted', 'bg-green-200', 'bg-green-400', 'bg-green-600', 'bg-muted']);
    expect(squares[3]).toHaveAttribute('title', 'Ann, 2026-03-04: 3');
  });

  it('says so when nothing is on the list', async () => {
    fetchDisplayToday.mockResolvedValue({ ...today, chores: [] });
    renderIt({ isAdmin: false, adminPin: null });
    expect(await screen.findByText('Nothing on the list today.')).toBeInTheDocument();
  });

  it('shows no link controls to a member and never asks for the links', async () => {
    renderIt({ isAdmin: false, adminPin: null });
    await screen.findByText('Dishes');
    expect(screen.queryByText('Make a link')).not.toBeInTheDocument();
    expect(screen.queryByText('Copy link')).not.toBeInTheDocument();
    expect(fetchDisplayLinks).not.toHaveBeenCalled();
  });

  it('lets an admin make a link when there is none', async () => {
    makeDisplayLink.mockResolvedValue(link);
    renderIt({ isAdmin: true, adminPin: '1234' });

    const make = await screen.findByText('Make a link');
    fetchDisplayLinks.mockResolvedValue([link]);
    fireEvent.click(make);

    await waitFor(() => expect(makeDisplayLink).toHaveBeenCalledWith('h1', '1234'));
    expect(await screen.findByText('Copy link')).toBeInTheDocument();
    expect(screen.queryByText('Make a link')).not.toBeInTheDocument();
  });

  it('lets an admin copy the link, open its page, and revoke it', async () => {
    fetchDisplayLinks.mockResolvedValue([link]);
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    revokeDisplayLink.mockResolvedValue(undefined);
    renderIt({ isAdmin: true, adminPin: '1234' });

    fireEvent.click(await screen.findByText('Copy link'));
    await waitFor(() => expect(writeText).toHaveBeenCalledWith(link.url));
    expect(await screen.findByText('Copied')).toBeInTheDocument();

    expect(screen.getByText('Open page').closest('a')).toHaveAttribute('href', link.url);

    fetchDisplayLinks.mockResolvedValue([]);
    fireEvent.click(screen.getByText('Revoke'));
    await waitFor(() => expect(revokeDisplayLink).toHaveBeenCalledWith('h1', '1234', 'l1'));
    expect(await screen.findByText('Make a link')).toBeInTheDocument();
  });
});
