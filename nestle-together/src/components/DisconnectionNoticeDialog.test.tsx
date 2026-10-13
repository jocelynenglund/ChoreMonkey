import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import { DisconnectionNoticeDialog } from "./DisconnectionNoticeDialog";

const fetchDisconnectionNotice = vi.fn();
vi.mock("@/features/chores", () => ({
  fetchDisconnectionNotice: (...args: unknown[]) => fetchDisconnectionNotice(...args),
}));

const disconnected = {
  isDisconnected: true,
  windowStart: new Date("2026-08-13"),
  missedChores: [
    { choreId: "c1", displayName: "Dishes", frequencyType: "daily" },
    { choreId: "c2", displayName: "Trash", frequencyType: "weekly", lastCompleted: new Date("2026-06-01") },
  ],
};

describe("DisconnectionNoticeDialog", () => {
  beforeEach(() => fetchDisconnectionNotice.mockReset());

  it("shows the notice on load when disconnected", async () => {
    fetchDisconnectionNotice.mockResolvedValue(disconnected);
    render(<DisconnectionNoticeDialog householdId="h1" memberId="m1" refreshKey={0} onCompleteChore={vi.fn()} />);

    expect(await screen.findByText("Notice of Disconnection")).toBeInTheDocument();
    expect(screen.getByText("Dishes")).toBeInTheDocument();
    expect(screen.getByText("Trash")).toBeInTheDocument();
    expect(screen.getByText("Never done")).toBeInTheDocument();
    const month = new Date().toLocaleDateString(undefined, { month: "long" });
    expect(screen.getByText(`disconnected in the month of ${month}`)).toBeInTheDocument();
    expect(fetchDisconnectionNotice).toHaveBeenCalledWith("h1", "m1");
  });

  it("dates the lapse from the last real completion, not the check window", async () => {
    fetchDisconnectionNotice.mockResolvedValue(disconnected);
    render(<DisconnectionNoticeDialog householdId="h1" memberId="m1" refreshKey={0} onCompleteChore={vi.fn()} />);

    const fmt = (d: Date) => d.toLocaleDateString(undefined, { day: "numeric", month: "long", year: "numeric" });
    const description = await screen.findByText(/none of your obligatory chores have been done/);
    expect(description.textContent).toContain(`since ${fmt(new Date("2026-06-01"))}`);
    expect(description.textContent).not.toContain(fmt(disconnected.windowStart));
  });

  it("says 'in over two months' when nothing was ever done", async () => {
    fetchDisconnectionNotice.mockResolvedValue({
      ...disconnected,
      missedChores: [{ choreId: "c1", displayName: "Dishes", frequencyType: "daily" }],
    });
    render(<DisconnectionNoticeDialog householdId="h1" memberId="m1" refreshKey={0} onCompleteChore={vi.fn()} />);

    expect(await screen.findByText(/have been done in over two months/)).toBeInTheDocument();
  });

  it("stays hidden when connected", async () => {
    fetchDisconnectionNotice.mockResolvedValue({ isDisconnected: false, windowStart: new Date(), missedChores: [] });
    render(<DisconnectionNoticeDialog householdId="h1" memberId="m1" refreshKey={0} onCompleteChore={vi.fn()} />);

    await waitFor(() => expect(fetchDisconnectionNotice).toHaveBeenCalled());
    expect(screen.queryByText("Notice of Disconnection")).not.toBeInTheDocument();
  });

  it("hands off to the completion flow from 'Do it now'", async () => {
    fetchDisconnectionNotice.mockResolvedValue(disconnected);
    const onCompleteChore = vi.fn();
    render(<DisconnectionNoticeDialog householdId="h1" memberId="m1" refreshKey={0} onCompleteChore={onCompleteChore} />);

    const buttons = await screen.findAllByRole("button", { name: "Do it now" });
    fireEvent.click(buttons[0]);

    expect(onCompleteChore).toHaveBeenCalledWith("c1");
    await waitFor(() => expect(screen.queryByText("Notice of Disconnection")).not.toBeInTheDocument());
  });

  it("does not check without a member", () => {
    render(<DisconnectionNoticeDialog householdId="h1" memberId={null} refreshKey={0} onCompleteChore={vi.fn()} />);
    expect(fetchDisconnectionNotice).not.toHaveBeenCalled();
  });
});
