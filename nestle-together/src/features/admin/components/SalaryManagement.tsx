import { useState, useEffect } from 'react';
import { getCurrentPeriod, setMemberSalary, closePeriod } from '../../salary/api';
import type { CurrentPeriodResponse, MemberPeriodSummary } from '../../salary/types';
import { useHouseholdStore } from '../../store';
import './SalaryManagement.css';

interface MemberSalaryForm {
  baseSalary: string;
  deductionMultiplier: string;
  bonusMultiplier: string;
}

export function SalaryManagement() {
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

  useEffect(() => {
    loadPeriod();
  }, [currentHouseholdId]);

  async function loadPeriod() {
    if (!currentHouseholdId) return;
    setLoading(true);
    const data = await getCurrentPeriod(currentHouseholdId);
    setPeriodData(data);
    setLoading(false);
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
    </div>
  );
}
