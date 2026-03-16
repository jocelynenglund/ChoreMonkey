import { useState, useEffect } from 'react';
import { Trash2, Users, DollarSign, Star } from 'lucide-react';
import { useHouseholdStore } from '../../store';
import { setChoreRates } from '../../salary/api';
import type { Chore } from '../../chores/types';
import './ChoreManagement.css';

export function ChoreManagement() {
  const { 
    currentHouseholdId, 
    currentPinCode,
    getHouseholdChores, 
    deleteChore,
    fetchHouseholdMembers,
    assignChore,
    members
  } = useHouseholdStore();
  
  const [chores, setChores] = useState<Chore[]>([]);
  const [loading, setLoading] = useState(true);
  const [editingChore, setEditingChore] = useState<string | null>(null);
  const [assigningChore, setAssigningChore] = useState<string | null>(null);
  const [ratesForm, setRatesForm] = useState({ deductionRate: '10', bonusRate: '10' });
  const [selectedMembers, setSelectedMembers] = useState<string[]>([]);
  const [assignToAll, setAssignToAll] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    loadData();
  }, [currentHouseholdId]);

  async function loadData() {
    if (!currentHouseholdId) return;
    setLoading(true);
    const [choreList] = await Promise.all([
      getHouseholdChores(currentHouseholdId),
      fetchHouseholdMembers(currentHouseholdId)
    ]);
    setChores(choreList);
    setLoading(false);
  }

  async function handleDelete(choreId: string, choreName: string) {
    if (!currentHouseholdId || !currentPinCode) return;
    if (!confirm(`Delete "${choreName}"? This cannot be undone.`)) return;
    
    const success = await deleteChore(currentHouseholdId, choreId);
    if (success) {
      setChores(prev => prev.filter(c => c.id !== choreId));
      showMessage('Chore deleted');
    }
  }

  async function handleSaveRates(choreId: string) {
    if (!currentHouseholdId) return;
    await setChoreRates(currentHouseholdId, choreId, {
      deductionRate: parseFloat(ratesForm.deductionRate) || 0,
      bonusRate: parseFloat(ratesForm.bonusRate) || 0
    });
    setEditingChore(null);
    showMessage('Rates saved');
  }

  async function handleSaveAssignment(choreId: string) {
    if (!currentHouseholdId) return;
    await assignChore(currentHouseholdId, choreId, 
      assignToAll ? undefined : selectedMembers, 
      assignToAll
    );
    setAssigningChore(null);
    showMessage('Assignment saved');
    loadData();
  }

  function startEditing(chore: Chore) {
    setEditingChore(chore.id);
    setRatesForm({
      deductionRate: (chore.missedDeduction || 10).toString(),
      bonusRate: '10'
    });
  }

  function startAssigning(chore: Chore) {
    setAssigningChore(chore.id);
    setSelectedMembers(chore.assignedTo || []);
    setAssignToAll(chore.assignedToAll || false);
  }

  function showMessage(text: string) {
    setMessage(text);
    setTimeout(() => setMessage(null), 2000);
  }

  function toggleMember(memberId: string) {
    setSelectedMembers(prev => 
      prev.includes(memberId) 
        ? prev.filter(id => id !== memberId)
        : [...prev, memberId]
    );
    setAssignToAll(false);
  }

  if (loading) {
    return <div className="chore-management loading">Loading...</div>;
  }

  const requiredChores = chores.filter(c => !c.isOptional);
  const bonusChores = chores.filter(c => c.isOptional);

  return (
    <div className="chore-management">
      {message && <div className="saved-message">✓ {message}</div>}

      <section className="chore-section">
        <h3>Required Chores ({requiredChores.length})</h3>
        {requiredChores.length === 0 && (
          <p className="empty">No required chores yet</p>
        )}
        {requiredChores.map(chore => (
          <div key={chore.id} className="chore-card">
            {editingChore === chore.id ? (
              <div className="edit-form">
                <h4>{chore.displayName}</h4>
                <label>
                  Deduction if Missed (kr)
                  <input
                    type="number"
                    value={ratesForm.deductionRate}
                    onChange={e => setRatesForm({ ...ratesForm, deductionRate: e.target.value })}
                  />
                </label>
                <div className="form-actions">
                  <button className="cancel-btn" onClick={() => setEditingChore(null)}>Cancel</button>
                  <button className="save-btn" onClick={() => handleSaveRates(chore.id)}>💾 Save</button>
                </div>
              </div>
            ) : assigningChore === chore.id ? (
              <div className="assign-form">
                <h4>Assign: {chore.displayName}</h4>
                <label className="assign-all">
                  <input 
                    type="checkbox" 
                    checked={assignToAll}
                    onChange={e => {
                      setAssignToAll(e.target.checked);
                      if (e.target.checked) setSelectedMembers([]);
                    }}
                  />
                  Assign to everyone
                </label>
                {!assignToAll && (
                  <div className="member-list">
                    {members.map(m => (
                      <label key={m.id} className="member-option">
                        <input
                          type="checkbox"
                          checked={selectedMembers.includes(m.id)}
                          onChange={() => toggleMember(m.id)}
                        />
                        {m.nickname}
                      </label>
                    ))}
                  </div>
                )}
                <div className="form-actions">
                  <button className="cancel-btn" onClick={() => setAssigningChore(null)}>Cancel</button>
                  <button className="save-btn" onClick={() => handleSaveAssignment(chore.id)}>💾 Save</button>
                </div>
              </div>
            ) : (
              <>
                <div className="chore-info">
                  <strong>{chore.displayName}</strong>
                  <span className="chore-meta">
                    {chore.frequency?.type || 'once'}
                    {chore.missedDeduction && ` • -${chore.missedDeduction} kr`}
                  </span>
                  <span className="assigned">
                    {chore.assignedToAll ? 'Everyone' : 
                      chore.assignedTo?.length ? `${chore.assignedTo.length} assigned` : 'Unassigned'}
                  </span>
                </div>
                <div className="chore-actions">
                  <button onClick={() => startAssigning(chore)} title="Assign">
                    <Users className="w-4 h-4" />
                  </button>
                  <button onClick={() => startEditing(chore)} title="Edit rates">
                    <DollarSign className="w-4 h-4" />
                  </button>
                  <button onClick={() => handleDelete(chore.id, chore.displayName)} className="delete" title="Delete">
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </>
            )}
          </div>
        ))}
      </section>

      <section className="chore-section">
        <h3><Star className="w-4 h-4 inline" /> Bonus Chores ({bonusChores.length})</h3>
        {bonusChores.length === 0 && (
          <p className="empty">No bonus chores yet</p>
        )}
        {bonusChores.map(chore => (
          <div key={chore.id} className="chore-card bonus">
            {editingChore === chore.id ? (
              <div className="edit-form">
                <h4>{chore.displayName}</h4>
                <label>
                  Bonus Earned (kr)
                  <input
                    type="number"
                    value={ratesForm.bonusRate}
                    onChange={e => setRatesForm({ ...ratesForm, bonusRate: e.target.value })}
                  />
                </label>
                <div className="form-actions">
                  <button className="cancel-btn" onClick={() => setEditingChore(null)}>Cancel</button>
                  <button className="save-btn" onClick={() => handleSaveRates(chore.id)}>💾 Save</button>
                </div>
              </div>
            ) : (
              <>
                <div className="chore-info">
                  <strong>{chore.displayName}</strong>
                  <span className="chore-meta">bonus</span>
                </div>
                <div className="chore-actions">
                  <button onClick={() => startEditing(chore)} title="Edit rates">
                    <DollarSign className="w-4 h-4" />
                  </button>
                  <button onClick={() => handleDelete(chore.id, chore.displayName)} className="delete" title="Delete">
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
              </>
            )}
          </div>
        ))}
      </section>
    </div>
  );
}
