import { useState, useEffect } from 'react';
import { Trash2, Users, DollarSign, Star, Pencil } from 'lucide-react';
import { useHouseholdStore } from '@/stores/householdStore';
import { setChoreRates } from '../../salary/api';
import { updateChore } from '../../chores/api';
import type { Chore } from '../../chores/types';
import './ChoreManagement.css';

interface EditForm {
  displayName: string;
  description: string;
  frequencyType: string;
  intervalDays: string;
  days: string[];
  isOptional: boolean;
  missedDeduction: string;
}

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
  const [editingChore, setEditingChore] = useState<string | null>(null); // rates editing
  const [editingChoreDetails, setEditingChoreDetails] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<EditForm>({
    displayName: '', description: '', frequencyType: 'once',
    intervalDays: '7', days: [], isOptional: false, missedDeduction: '10',
  });
  const [editSaving, setEditSaving] = useState(false);
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

  async function handleSaveRates(choreId: string, isBonus: boolean) {
    if (!currentHouseholdId) return;
    // Bonus chores: only bonusRate, Required chores: only deductionRate
    await setChoreRates(currentHouseholdId, choreId, {
      deductionRate: isBonus ? 0 : parseFloat(ratesForm.deductionRate) || 0,
      bonusRate: isBonus ? parseFloat(ratesForm.bonusRate) || 0 : 0
    });
    setEditingChore(null);
    showMessage('Rates saved');
    loadData(); // Refresh to show updated rates
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

  function startEditingDetails(chore: Chore) {
    setEditingChoreDetails(chore.id);
    setEditForm({
      displayName: chore.displayName,
      description: chore.description || '',
      frequencyType: chore.frequency?.type || 'once',
      intervalDays: chore.frequency?.intervalDays?.toString() || '7',
      days: chore.frequency?.days || [],
      isOptional: chore.isOptional || false,
      missedDeduction: (chore.missedDeduction ?? 10).toString(),
    });
  }

  async function saveChoreDetails(chore: Chore) {
    if (!currentHouseholdId) return;
    setEditSaving(true);
    const freq = editForm.frequencyType === 'once' ? null : {
      type: editForm.frequencyType,
      days: editForm.frequencyType === 'weekly' && editForm.days.length > 0 ? editForm.days : undefined,
      intervalDays: editForm.frequencyType === 'interval' ? parseInt(editForm.intervalDays) || 7 : undefined,
    };
    const ok = await updateChore(currentHouseholdId, chore.id, {
      displayName: editForm.displayName,
      description: editForm.description,
      frequency: freq,
      isOptional: editForm.isOptional,
      isRequired: !editForm.isOptional,
      missedDeduction: parseFloat(editForm.missedDeduction) || 0,
    });
    if (ok) {
      setEditingChoreDetails(null);
      showMessage('Chore updated!');
      loadData();
    }
    setEditSaving(false);
  }

  const WEEKDAYS = ['monday','tuesday','wednesday','thursday','friday','saturday','sunday'];

  function startEditing(chore: Chore) {
    setEditingChore(chore.id);
    // Use saved rates, or fallback to defaults
    setRatesForm({
      deductionRate: (chore.deductionRate ?? chore.missedDeduction ?? 10).toString(),
      bonusRate: (chore.bonusRate ?? 10).toString()
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
            {editingChoreDetails === chore.id ? (
              <div className="edit-form">
                <h4>Edit Chore</h4>
                <label>Name
                  <input type="text" value={editForm.displayName}
                    onChange={e => setEditForm({...editForm, displayName: e.target.value})} />
                </label>
                <label>Description
                  <input type="text" value={editForm.description}
                    onChange={e => setEditForm({...editForm, description: e.target.value})} />
                </label>
                <label>Frequency
                  <select value={editForm.frequencyType}
                    onChange={e => setEditForm({...editForm, frequencyType: e.target.value})}>
                    <option value="once">Once</option>
                    <option value="daily">Daily</option>
                    <option value="weekly">Weekly</option>
                    <option value="interval">Every X days</option>
                  </select>
                </label>
                {editForm.frequencyType === 'interval' && (
                  <label>Every (days)
                    <input type="number" min="1" value={editForm.intervalDays}
                      onChange={e => setEditForm({...editForm, intervalDays: e.target.value})} />
                  </label>
                )}
                {editForm.frequencyType === 'weekly' && (
                  <div className="day-picker">
                    {WEEKDAYS.map(day => (
                      <label key={day} className="day-option">
                        <input type="checkbox"
                          checked={editForm.days.includes(day)}
                          onChange={() => setEditForm({...editForm,
                            days: editForm.days.includes(day)
                              ? editForm.days.filter(d => d !== day)
                              : [...editForm.days, day]
                          })} />
                        {day.slice(0,3)}
                      </label>
                    ))}
                  </div>
                )}
                <label>Deduction if missed (kr)
                  <input type="number" value={editForm.missedDeduction}
                    onChange={e => setEditForm({...editForm, missedDeduction: e.target.value})} />
                </label>
                <div className="form-actions">
                  <button className="cancel-btn" onClick={() => setEditingChoreDetails(null)}>Cancel</button>
                  <button className="save-btn" disabled={editSaving} onClick={() => saveChoreDetails(chore)}>
                    {editSaving ? 'Saving...' : '💾 Save'}
                  </button>
                </div>
              </div>
            ) : editingChore === chore.id ? (
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
                  <button className="save-btn" onClick={() => handleSaveRates(chore.id, false)}>💾 Save</button>
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
                    {(chore.deductionRate ?? chore.missedDeduction) ? ` • -${chore.deductionRate ?? chore.missedDeduction} kr` : ''}
                  </span>
                  <span className="assigned">
                    {chore.assignedToAll ? 'Everyone' : 
                      chore.assignedTo?.length ? `${chore.assignedTo.length} assigned` : 'Unassigned'}
                  </span>
                </div>
                <div className="chore-actions">
                  <button onClick={() => startEditingDetails(chore)} title="Edit chore">
                    <Pencil className="w-4 h-4" />
                  </button>
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
            {editingChoreDetails === chore.id ? (
              <div className="edit-form">
                <h4>Edit Chore</h4>
                <label>Name
                  <input type="text" value={editForm.displayName}
                    onChange={e => setEditForm({...editForm, displayName: e.target.value})} />
                </label>
                <label>Description
                  <input type="text" value={editForm.description}
                    onChange={e => setEditForm({...editForm, description: e.target.value})} />
                </label>
                <div className="form-actions">
                  <button className="cancel-btn" onClick={() => setEditingChoreDetails(null)}>Cancel</button>
                  <button className="save-btn" disabled={editSaving} onClick={() => saveChoreDetails(chore)}>
                    {editSaving ? 'Saving...' : '💾 Save'}
                  </button>
                </div>
              </div>
            ) : editingChore === chore.id ? (
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
                  <button className="save-btn" onClick={() => handleSaveRates(chore.id, true)}>💾 Save</button>
                </div>
              </div>
            ) : (
              <>
                <div className="chore-info">
                  <strong>{chore.displayName}</strong>
                  <span className="chore-meta">bonus{chore.bonusRate ? ` • +${chore.bonusRate} kr` : ''}</span>
                </div>
                <div className="chore-actions">
                  <button onClick={() => startEditingDetails(chore)} title="Edit chore">
                    <Pencil className="w-4 h-4" />
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
    </div>
  );
}
