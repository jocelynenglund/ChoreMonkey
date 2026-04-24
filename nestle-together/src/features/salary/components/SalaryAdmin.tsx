import { useState, useEffect } from 'react';
import { getCurrentPeriod, setMemberSalary, closePeriod, getPayoutHistory, getOfficialSalarySlip, getAvailablePeriods } from '../api';
import type { CurrentPeriodResponse, MemberPeriodSummary, PeriodPayout, OfficialSalarySlipResponse, AvailablePeriod } from '../types';
import { useHouseholdStore } from '../../store';
import { SalarySlip } from './SalarySlip';
import './SalaryAdmin.css';

interface MemberSalaryForm {
  baseSalary: string;
  deductionMultiplier: string;
  bonusMultiplier: string;
}

function formatPeriodLabel(period: AvailablePeriod, isCurrent: boolean): string {
  const end = new Date(period.periodEnd);
  const label = end.toLocaleDateString('sv-SE', { month: 'long', year: 'numeric' });
  if (period.isClosed) return `${label} ✓`;
  if (isCurrent) return `${label} (current)`;
  return label;
}

function buildPreviewSlip(
  member: MemberPeriodSummary,
  periodStart: string,
  periodEnd: string
): OfficialSalarySlipResponse {
  const deductionMult = member.deductionMultiplier || 1;
  const bonusMult = member.bonusMultiplier || 1;
  return {
    periodId: 'preview',
    periodStart,
    periodEnd,
    memberName: member.name,
    baseSalary: member.baseSalary,
    deductions: member.missedChores.map((mc) => ({
      choreName: `${mc.choreName} (${mc.period})`,
      baseRate: deductionMult !== 0 ? mc.deduction / deductionMult : mc.deduction,
      multiplier: member.deductionMultiplier,
      amount: mc.deduction,
    })),
    bonuses: member.bonusChores.map((bc) => ({
      choreName: bc.choreName,
      baseRate: bonusMult !== 0 ? bc.bonus / bonusMult : bc.bonus,
      multiplier: member.bonusMultiplier,
      amount: bc.bonus,
    })),
    grossDeductions: member.deductions,
    grossBonuses: member.bonuses,
    netPay: member.projected,
  };
}

export function SalaryAdmin() {
  const { currentHouseholdId } = useHouseholdStore();
  const [periodData, setPeriodData] = useState<CurrentPeriodResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [editingMember, setEditingMember] = useState<string | null>(null);
  const [form, setForm] = useState<MemberSalaryForm>({
    baseSalary: '100',
    deductionMultiplier: '1.0',
    bonusMultiplier: '1.0',
  });
  const [saving, setSaving] = useState(false);
  const [closing, setClosing] = useState(false);
  const [savedMessage, setSavedMessage] = useState<string | null>(null);
  const [history, setHistory] = useState<PeriodPayout[]>([]);
  const [activeSlip, setActiveSlip] = useState<OfficialSalarySlipResponse | null>(null);
  const [slipIsPreview, setSlipIsPreview] = useState(false);
  const [loadingSlip, setLoadingSlip] = useState<string | null>(null);

  // Period selector
  const [availablePeriods, setAvailablePeriods] = useState<AvailablePeriod[]>([]);
  const [selectedPeriodIdx, setSelectedPeriodIdx] = useState<number>(0);

  useEffect(() => {
    loadAll();
  }, [currentHouseholdId]);

  async function loadAll() {
    if (!currentHouseholdId) return;
    setLoading(true);
    const [currentData, periodsData, historyData] = await Promise.all([
      getCurrentPeriod(currentHouseholdId),
      getAvailablePeriods(currentHouseholdId),
      getPayoutHistory(currentHouseholdId),
    ]);
    setPeriodData(currentData);
    setAvailablePeriods(periodsData);
    setSelectedPeriodIdx(0); // default to most recent
    const periods = Array.isArray(historyData) ? historyData : (historyData as any)?.periods ?? [];
    setHistory(periods);
    setLoading(false);
  }

  async function viewSlip(periodId: string, memberId: string) {
    if (!currentHouseholdId) return;
    const key = `${periodId}-${memberId}`;
    setLoadingSlip(key);
    const slip = await getOfficialSalarySlip(currentHouseholdId, periodId, memberId);
    if (slip) {
      setActiveSlip(slip);
      setSlipIsPreview(false);
    }
    setLoadingSlip(null);
  }

  function previewSlip(member: MemberPeriodSummary) {
    if (!periodData) return;
    setActiveSlip(buildPreviewSlip(member, periodData.periodStart, periodData.periodEnd));
    setSlipIsPreview(true);
  }

  function startEditing(member: MemberPeriodSummary) {
    setEditingMember(member.memberId);
    setForm({
      baseSalary: member.baseSalary.toString(),
      deductionMultiplier: member.deductionMultiplier?.toString() ?? '1.0',
      bonusMultiplier: member.bonusMultiplier?.toString() ?? '1.0',
    });
  }

  async function saveSalary() {
    if (!currentHouseholdId || !editingMember) return;
    setSaving(true);
    const success = await setMemberSalary(currentHouseholdId, editingMember, {
      baseSalary: parseFloat(form.baseSalary) || 0,
      deductionMultiplier: parseFloat(form.deductionMultiplier) || 1.0,
      bonusMultiplier: parseFloat(form.bonusMultiplier) || 1.0,
    });
    if (success) {
      setEditingMember(null);
      setSavedMessage('Salary saved!');
      setTimeout(() => setSavedMessage(null), 2000);
      const data = await getCurrentPeriod(currentHouseholdId);
      setPeriodData(data);
    }
    setSaving(false);
  }

  const selectedPeriod = availablePeriods[selectedPeriodIdx] ?? null;
  const periodEnded = selectedPeriod ? new Date() > new Date(selectedPeriod.periodEnd) : false;
  const isCurrentPeriod = !!(
    selectedPeriod &&
    !selectedPeriod.isClosed &&
    periodData &&
    selectedPeriod.periodStart === periodData.periodStart &&
    selectedPeriod.periodEnd === periodData.periodEnd
  );

  async function handleClosePeriod() {
    if (!currentHouseholdId || !selectedPeriod || selectedPeriod.isClosed) return;

    const hasActivity = periodData?.members?.some(
      (m) => m.missedChores.length > 0 || m.bonusChores.length > 0
    );
    const message = hasActivity
      ? 'Close this period? This will finalize all salaries and cannot be undone.'
      : 'No chore activity was recorded for this period — payslips will be empty. Close anyway?';
    if (!confirm(message)) return;

    setClosing(true);
    try {
      const result = await closePeriod(currentHouseholdId, new Date(selectedPeriod.periodEnd));
      if (result) {
        alert(`Period closed! ${result.payouts.length} payouts recorded.`);
        await loadAll();
      }
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'Failed to close period');
    }
    setClosing(false);
  }

  const formatDate = (dateStr: string) =>
    new Date(dateStr).toLocaleDateString('sv-SE', { month: 'long', year: 'numeric' });

  const formatCurrency = (amount: number) => `${amount.toFixed(0)} kr`;

  // Find history entry for selected closed period
  const selectedHistory = selectedPeriod?.isClosed && selectedPeriod.periodId
    ? history.find((h) => h.periodId === selectedPeriod.periodId) ?? null
    : null;

  if (loading) {
    return <div className="salary-admin loading">Loading...</div>;
  }

  return (
    <div className="salary-admin">
      <header className="admin-header">
        <h2>💼 Salary Management</h2>
        {periodData && (
          <span className="period">{formatDate(periodData.periodStart)}</span>
        )}
      </header>

      <p className="instructions">Click "Set Up" or "Edit" on a member to configure their salary, then click Save.</p>

      {/* Member salary config — always for current period */}
      <div className="member-salaries">
        {(!periodData?.members || periodData.members.length === 0) && (
          <div className="no-members">No members found. Add members to your household first.</div>
        )}
        {periodData?.members.map((member) => (
          <div key={member.memberId} className="member-card">
            {editingMember === member.memberId ? (
              <div className="edit-form">
                <h3>{member.name}</h3>
                <label>
                  Base Salary (kr)
                  <input
                    type="number"
                    value={form.baseSalary}
                    onChange={(e) => setForm({ ...form, baseSalary: e.target.value })}
                  />
                </label>
                <label>
                  Deduction Multiplier
                  <input type="number" step="0.1" min="0" max="2"
                    value={form.deductionMultiplier}
                    onChange={(e) => setForm({ ...form, deductionMultiplier: e.target.value })}
                  />
                  <span className="hint">0.5 = half deduction, 1.0 = full</span>
                </label>
                <label>
                  Bonus Multiplier
                  <input type="number" step="0.1" min="0" max="2"
                    value={form.bonusMultiplier}
                    onChange={(e) => setForm({ ...form, bonusMultiplier: e.target.value })}
                  />
                  <span className="hint">1.5 = 50% extra bonus</span>
                </label>
                <div className="form-actions">
                  <button type="button" onClick={() => setEditingMember(null)} disabled={saving} className="cancel-btn">
                    Cancel
                  </button>
                  <button type="button" onClick={saveSalary} disabled={saving} className="save-btn">
                    {saving ? 'Saving...' : '💾 Save'}
                  </button>
                </div>
              </div>
            ) : (
              <>
                <div className="member-info">
                  <h3>{member.name}</h3>
                  {member.baseSalary > 0 ? (
                    <>
                      <div className="salary-summary">
                        <span className="base">Base: {formatCurrency(member.baseSalary)}</span>
                        <span className="projected">→ {formatCurrency(member.projected)}</span>
                      </div>
                      {member.deductions > 0 && (
                        <span className="deduction-badge">
                          -{formatCurrency(member.deductions)} ({member.missedChores.length} missed)
                        </span>
                      )}
                      {member.bonuses > 0 && (
                        <span className="bonus-badge">+{formatCurrency(member.bonuses)} bonus</span>
                      )}
                    </>
                  ) : (
                    <span className="not-configured">No salary configured</span>
                  )}
                </div>
                <button className="edit-btn" onClick={() => startEditing(member)}>
                  {member.baseSalary > 0 ? 'Edit' : 'Set Up'}
                </button>
              </>
            )}
          </div>
        ))}
      </div>

      {savedMessage && <div className="saved-message">✓ {savedMessage}</div>}

      {/* Period selector + close/view */}
      <div className="close-period-section">
        <h3>Periods</h3>

        {availablePeriods.length === 0 && history.length === 0 ? (
          <p className="period-not-ended-note">⏳ No completed periods yet.</p>
        ) : availablePeriods.length === 0 && history.length > 0 ? (
          // Fallback: available-periods failed but we have history
          <div className="history-section">
            <p className="text-xs text-muted-foreground mb-3">Closed periods:</p>
            {history.map((period) => (
              <div key={period.periodId} className="history-period">
                <div className="history-period-header">
                  {new Date(period.periodEnd).toLocaleDateString('sv-SE', { month: 'long', year: 'numeric' })} ✓
                </div>
                <div className="history-payouts">
                  {period.payouts.map((p) => (
                    <div key={p.memberId} className="history-payout-row">
                      <span className="payout-name">{p.name}</span>
                      <span className="payout-amount">{p.netPay.toFixed(0)} kr</span>
                      <button
                        className="view-slip-btn"
                        disabled={loadingSlip === `${period.periodId}-${p.memberId}`}
                        onClick={() => viewSlip(period.periodId, p.memberId)}
                      >
                        {loadingSlip === `${period.periodId}-${p.memberId}` ? '...' : 'View Slip'}
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>
        ) : (
          <>
            <div className="period-selector">
              <label htmlFor="period-select">Select period</label>
              <select
                id="period-select"
                value={selectedPeriodIdx}
                onChange={(e) => setSelectedPeriodIdx(Number(e.target.value))}
                className="period-select"
              >
                {availablePeriods.map((p, i) => {
                  const isCur =
                    !p.isClosed &&
                    !!periodData &&
                    p.periodStart === periodData.periodStart &&
                    p.periodEnd === periodData.periodEnd;
                  return (
                    <option key={i} value={i}>
                      {formatPeriodLabel(p, isCur)}
                    </option>
                  );
                })}
              </select>
            </div>

            {isCurrentPeriod && periodData && (
              <div className="history-section" style={{ marginTop: '1rem' }}>
                <p className="text-xs text-muted-foreground mb-3">
                  Projected payouts so far — final amounts will be set when the period closes.
                </p>
                <div className="history-payouts">
                  {periodData.members.map((m) => (
                    <div key={m.memberId} className="history-payout-row">
                      <span className="payout-name">{m.name}</span>
                      <span className="payout-amount">{m.projected.toFixed(0)} kr</span>
                      <button
                        className="view-slip-btn"
                        onClick={() => previewSlip(m)}
                        disabled={m.baseSalary === 0}
                      >
                        Preview Slip
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {selectedPeriod && !selectedPeriod.isClosed && (
              <div className="period-actions">
                <p className="warning-text">
                  Closing finalizes all salaries and generates payslips. This cannot be undone.
                </p>
                {!periodEnded && (
                  <p className="period-not-ended-note">
                    ⏳ Period ends {new Date(selectedPeriod.periodEnd).toLocaleDateString('sv-SE')}. Come back after that date.
                  </p>
                )}
                <button
                  onClick={handleClosePeriod}
                  disabled={closing || !periodEnded}
                  className="close-period-btn"
                >
                  {closing ? 'Closing...' : '📋 Close Period & Generate Payslips'}
                </button>
              </div>
            )}

            {selectedPeriod?.isClosed && selectedHistory && (
              <div className="history-section" style={{ marginTop: '1rem' }}>
                <p className="period-closed-note">✅ Period closed</p>
                <div className="history-payouts">
                  {selectedHistory.payouts.map((p) => (
                    <div key={p.memberId} className="history-payout-row">
                      <span className="payout-name">{p.name}</span>
                      <span className="payout-amount">{p.netPay.toFixed(0)} kr</span>
                      <button
                        className="view-slip-btn"
                        disabled={loadingSlip === `${selectedHistory.periodId}-${p.memberId}`}
                        onClick={() => viewSlip(selectedHistory.periodId, p.memberId)}
                      >
                        {loadingSlip === `${selectedHistory.periodId}-${p.memberId}` ? '...' : 'View Slip'}
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </div>

      {/* Salary Slip Modal */}
      {activeSlip && (
        <SalarySlip
          slip={activeSlip}
          isPreview={slipIsPreview}
          onClose={() => {
            setActiveSlip(null);
            setSlipIsPreview(false);
          }}
        />
      )}
    </div>
  );
}
