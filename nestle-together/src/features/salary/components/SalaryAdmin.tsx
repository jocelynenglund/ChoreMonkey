import { useState, useEffect } from 'react';
import { getCurrentPeriod, setMemberSalary, closePeriod, getPayoutHistory, getOfficialSalarySlip, configurePayday } from '../api';
import type { CurrentPeriodResponse, MemberPeriodSummary, PeriodPayout, OfficialSalarySlipResponse } from '../types';
import { useHouseholdStore } from '../../store';
import { SalarySlip } from './SalarySlip';
import './SalaryAdmin.css';

interface MemberSalaryForm {
  baseSalary: string;
  deductionMultiplier: string;
  bonusMultiplier: string;
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
  const [loadingSlip, setLoadingSlip] = useState<string | null>(null);
  const [paydayDay, setPaydayDay] = useState<string>('28');
  const [paydayPin, setPaydayPin] = useState<string>('');
  const [savingPayday, setSavingPayday] = useState(false);

  useEffect(() => {
    loadPeriod();
    loadHistory();
  }, [currentHouseholdId]);

  async function loadPeriod() {
    if (!currentHouseholdId) return;
    setLoading(true);
    const data = await getCurrentPeriod(currentHouseholdId);
    setPeriodData(data);
    setLoading(false);
  }

  async function loadHistory() {
    if (!currentHouseholdId) return;
    const data = await getPayoutHistory(currentHouseholdId);
    // API returns { periods: [...] }
    const periods = Array.isArray(data) ? data : (data as any)?.periods ?? [];
    setHistory(periods);
  }

  async function viewSlip(periodId: string, memberId: string) {
    if (!currentHouseholdId) return;
    const key = `${periodId}-${memberId}`;
    setLoadingSlip(key);
    const slip = await getOfficialSalarySlip(currentHouseholdId, periodId, memberId);
    if (slip) {
      setActiveSlip(slip);
    }
    setLoadingSlip(null);
  }

  function startEditing(member: MemberPeriodSummary) {
    setEditingMember(member.memberId);
    setForm({
      baseSalary: member.baseSalary.toString(),
      deductionMultiplier: '1.0',
      bonusMultiplier: '1.0',
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
      loadPeriod();
    }
    setSaving(false);
  }

  async function handleClosePeriod() {
    if (!currentHouseholdId) return;
    if (!confirm('Close this period? This will finalize all salaries for this month.')) {
      return;
    }
    
    setClosing(true);
    const result = await closePeriod(currentHouseholdId, new Date());
    if (result) {
      alert(`Period closed! ${result.payouts.length} payouts recorded.`);
      loadPeriod();
    }
    setClosing(false);
  }

  async function handleSavePayday() {
    if (!currentHouseholdId) return;
    const day = parseInt(paydayDay, 10);
    const pin = parseInt(paydayPin, 10);
    if (isNaN(day) || day < 1 || day > 28) {
      alert('Payday must be between 1 and 28.');
      return;
    }
    if (isNaN(pin)) {
      alert('Please enter the admin PIN.');
      return;
    }
    setSavingPayday(true);
    const result = await configurePayday(currentHouseholdId, day, pin);
    if (result) {
      setSavedMessage('Payday saved!');
      setTimeout(() => setSavedMessage(null), 2000);
      setPaydayPin('');
      loadPeriod();
    } else {
      alert('Failed to save payday. Check your admin PIN.');
    }
    setSavingPayday(false);
  }

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString('sv-SE', { 
      month: 'long',
      year: 'numeric'
    });
  };

  const formatCurrency = (amount: number) => `${amount.toFixed(0)} kr`;

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
      
      {/* Payday Configuration */}
      <div className="payday-config">
        <h3>Payday</h3>
        <div className="payday-form">
          <label>
            Salary paid on the
            <input
              type="number"
              min="1"
              max="28"
              value={paydayDay}
              onChange={(e) => setPaydayDay(e.target.value)}
              className="payday-input"
            />
            th of each month
          </label>
          <label>
            Admin PIN
            <input
              type="password"
              value={paydayPin}
              onChange={(e) => setPaydayPin(e.target.value)}
              className="payday-pin-input"
              placeholder="PIN"
            />
          </label>
          <button
            onClick={handleSavePayday}
            disabled={savingPayday}
            className="save-btn"
          >
            {savingPayday ? 'Saving...' : 'Save Payday'}
          </button>
        </div>
        {periodData && (
          <p className="period-info">
            Current period: {new Date(periodData.periodStart).toLocaleDateString('sv-SE')} — {new Date(periodData.periodEnd).toLocaleDateString('sv-SE')}
          </p>
        )}
      </div>

      <p className="instructions">Click "Set Up" or "Edit" on a member to configure their salary, then click Save.</p>

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
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="2"
                    value={form.deductionMultiplier}
                    onChange={(e) => setForm({ ...form, deductionMultiplier: e.target.value })}
                  />
                  <span className="hint">0.5 = half deduction, 1.0 = full</span>
                </label>
                <label>
                  Bonus Multiplier
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    max="2"
                    value={form.bonusMultiplier}
                    onChange={(e) => setForm({ ...form, bonusMultiplier: e.target.value })}
                  />
                  <span className="hint">1.5 = 50% extra bonus</span>
                </label>
                <div className="form-actions">
                  <button 
                    type="button"
                    onClick={() => setEditingMember(null)} 
                    disabled={saving}
                    className="cancel-btn"
                  >
                    Cancel
                  </button>
                  <button 
                    type="button"
                    onClick={saveSalary} 
                    disabled={saving} 
                    className="save-btn"
                  >
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
                        <span className="projected">
                          → {formatCurrency(member.projected)}
                        </span>
                      </div>
                      {member.deductions > 0 && (
                        <span className="deduction-badge">
                          -{formatCurrency(member.deductions)} ({member.missedChores.length} missed)
                        </span>
                      )}
                      {member.bonuses > 0 && (
                        <span className="bonus-badge">
                          +{formatCurrency(member.bonuses)} bonus
                        </span>
                      )}
                    </>
                  ) : (
                    <span className="not-configured">No salary configured</span>
                  )}
                </div>
                <button 
                  className="edit-btn"
                  onClick={() => startEditing(member)}
                >
                  {member.baseSalary > 0 ? 'Edit' : 'Set Up'}
                </button>
              </>
            )}
          </div>
        ))}
      </div>

      {/* Success message */}
      {savedMessage && (
        <div className="saved-message">✓ {savedMessage}</div>
      )}

      {/* Close Period - separate section */}
      <div className="close-period-section">
        <h3>End of Month</h3>
        <p className="warning-text">
          Closing the period finalizes all salaries and generates payslips.
          This cannot be undone.
        </p>
        <button
          onClick={handleClosePeriod}
          disabled={closing}
          className="close-period-btn"
        >
          {closing ? 'Closing...' : '📋 Close Period & Generate Payslips'}
        </button>
      </div>

      {/* Payout History */}
      {history.length > 0 && (
        <div className="history-section">
          <h3>Past Periods</h3>
          {history.map((period) => (
            <div key={period.periodId} className="history-period">
              <div className="history-period-header">
                {new Date(period.periodEnd).toLocaleDateString('sv-SE', { month: 'long', year: 'numeric' })}
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
      )}

      {/* Salary Slip Modal */}
      {activeSlip && (
        <SalarySlip slip={activeSlip} onClose={() => setActiveSlip(null)} />
      )}
    </div>
  );
}
