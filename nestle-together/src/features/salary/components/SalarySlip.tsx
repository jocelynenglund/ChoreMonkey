import type { OfficialSalarySlipResponse } from '../types';
import './SalarySlip.css';

interface SalarySlipProps {
  slip: OfficialSalarySlipResponse;
  onClose: () => void;
  isPreview?: boolean;
}

export function SalarySlip({ slip, onClose, isPreview = false }: SalarySlipProps) {
  const formatDate = (dateStr: string) =>
    new Date(dateStr).toLocaleDateString('sv-SE', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });

  const formatCurrency = (amount: number) => `${amount.toFixed(2)} kr`;

  return (
    <div className="salary-slip-overlay" onClick={onClose}>
      <div className="salary-slip" onClick={(e) => e.stopPropagation()}>
        <div className="slip-content">
          {isPreview && (
            <div className="slip-preview-banner">
              ⚠️ Preview — period not closed. Final amounts may change.
            </div>
          )}
          <header className="slip-header">
            <h2>{isPreview ? 'Salary Slip (Preview)' : 'Salary Slip'}</h2>
            <div className="slip-period">
              {formatDate(slip.periodStart)} &mdash; {formatDate(slip.periodEnd)}
            </div>
            <div className="slip-member">{slip.memberName}</div>
          </header>

          <div className="slip-section">
            <h3>Deductions</h3>
            {slip.deductions.length > 0 ? (
              <table className="slip-table">
                <thead>
                  <tr>
                    <th>Chore</th>
                    <th>Rate</th>
                    <th>Mult.</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {slip.deductions.map((d, i) => (
                    <tr key={i}>
                      <td className="chore-name">{d.choreName}</td>
                      <td>{d.baseRate.toFixed(2)}</td>
                      <td>{d.multiplier.toFixed(1)}x</td>
                      <td>-{formatCurrency(d.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <div className="empty-note">No deductions</div>
            )}
          </div>

          <div className="slip-section">
            <h3>Bonuses</h3>
            {slip.bonuses.length > 0 ? (
              <table className="slip-table">
                <thead>
                  <tr>
                    <th>Chore</th>
                    <th>Rate</th>
                    <th>Mult.</th>
                    <th>Amount</th>
                  </tr>
                </thead>
                <tbody>
                  {slip.bonuses.map((b, i) => (
                    <tr key={i}>
                      <td className="chore-name">{b.choreName}</td>
                      <td>{b.baseRate.toFixed(2)}</td>
                      <td>{b.multiplier.toFixed(1)}x</td>
                      <td>+{formatCurrency(b.amount)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            ) : (
              <div className="empty-note">No bonuses</div>
            )}
          </div>

          <div className="slip-summary">
            <div className="summary-row">
              <span>Base Salary</span>
              <span>{formatCurrency(slip.baseSalary)}</span>
            </div>
            <div className="summary-row deductions">
              <span>Total Deductions</span>
              <span>-{formatCurrency(slip.grossDeductions)}</span>
            </div>
            <div className="summary-row bonuses">
              <span>Total Bonuses</span>
              <span>+{formatCurrency(slip.grossBonuses)}</span>
            </div>
            <div className="summary-row net-pay">
              <span>Net Pay</span>
              <span>{formatCurrency(slip.netPay)}</span>
            </div>
          </div>
        </div>

        <div className="slip-actions">
          <button onClick={onClose}>Close</button>
          <button className="print-btn" onClick={() => window.print()}>
            Print
          </button>
        </div>
      </div>
    </div>
  );
}
