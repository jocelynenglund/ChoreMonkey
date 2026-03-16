import { useState, useEffect } from 'react';
import { getCurrentPeriod } from '../api';
import type { MemberPeriodSummary } from '../types';
import { useHouseholdStore } from '../../store';
import './MyAllowance.css';

export function MyAllowance() {
  const { currentHouseholdId, currentMemberId } = useHouseholdStore();
  const [summary, setSummary] = useState<MemberPeriodSummary | null>(null);
  const [periodStart, setPeriodStart] = useState<string>('');
  const [periodEnd, setPeriodEnd] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadSalary() {
      if (!currentHouseholdId || !currentMemberId) return;
      
      setLoading(true);
      setError(null);
      
      try {
        const data = await getCurrentPeriod(currentHouseholdId, currentMemberId);
        if (data && data.members.length > 0) {
          setSummary(data.members[0]);
          setPeriodStart(data.periodStart);
          setPeriodEnd(data.periodEnd);
        } else {
          setError('No salary data available');
        }
      } catch (err) {
        setError('Failed to load salary');
      } finally {
        setLoading(false);
      }
    }
    
    loadSalary();
  }, [currentHouseholdId, currentMemberId]);

  if (loading) {
    return <div className="my-allowance loading">Loading...</div>;
  }

  if (error || !summary) {
    return <div className="my-allowance error">{error || 'No data'}</div>;
  }

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString('sv-SE', { 
      month: 'short', 
      day: 'numeric' 
    });
  };

  const formatCurrency = (amount: number) => {
    return `${amount.toFixed(0)} kr`;
  };

  return (
    <div className="my-allowance">
      <header className="allowance-header">
        <h2>💰 My Allowance</h2>
        <span className="period">
          {formatDate(periodStart)} – {formatDate(periodEnd)}
        </span>
      </header>

      <div className="projected-amount">
        <span className="label">Projected</span>
        <span className="amount">{formatCurrency(summary.projected)}</span>
      </div>

      <div className="breakdown">
        <div className="breakdown-row base">
          <span>Base salary</span>
          <span>{formatCurrency(summary.baseSalary)}</span>
        </div>
        
        {summary.deductions > 0 && (
          <div className="breakdown-row deductions">
            <span>Deductions</span>
            <span className="negative">-{formatCurrency(summary.deductions)}</span>
          </div>
        )}
        
        {summary.bonuses > 0 && (
          <div className="breakdown-row bonuses">
            <span>Bonuses</span>
            <span className="positive">+{formatCurrency(summary.bonuses)}</span>
          </div>
        )}
      </div>

      {summary.missedChores.length > 0 && (
        <details className="missed-section">
          <summary>
            ⚠️ Missed chores ({summary.missedChores.length})
          </summary>
          <ul className="missed-list">
            {summary.missedChores.map((chore, i) => (
              <li key={`${chore.choreId}-${i}`}>
                <span className="chore-name">{chore.choreName}</span>
                <span className="chore-period">{chore.period}</span>
                <span className="chore-deduction">-{formatCurrency(chore.deduction)}</span>
              </li>
            ))}
          </ul>
        </details>
      )}

      {summary.bonusChores.length > 0 && (
        <details className="bonus-section">
          <summary>
            ⭐ Bonus chores ({summary.bonusChores.length})
          </summary>
          <ul className="bonus-list">
            {summary.bonusChores.map((chore, i) => (
              <li key={`${chore.choreId}-${i}`}>
                <span className="chore-name">{chore.choreName}</span>
                <span className="chore-bonus">+{formatCurrency(chore.bonus)}</span>
              </li>
            ))}
          </ul>
        </details>
      )}

      {summary.missedChores.length === 0 && summary.deductions === 0 && (
        <div className="perfect-record">
          ✨ Perfect record this month!
        </div>
      )}
    </div>
  );
}
