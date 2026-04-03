import { useState } from 'react';
import { ClipboardList, Wallet } from 'lucide-react';
import { ChoreManagement } from './ChoreManagement';
import { SalaryAdmin } from '../../salary/components/SalaryAdmin';
import './AdminPanel.css';

type Tab = 'chores' | 'salaries';

export function AdminPanel() {
  const [activeTab, setActiveTab] = useState<Tab>('chores');

  return (
    <div className="admin-panel">
      <div className="admin-tabs">
        <button
          className={`tab ${activeTab === 'chores' ? 'active' : ''}`}
          onClick={() => setActiveTab('chores')}
        >
          <ClipboardList className="w-4 h-4" />
          Chores
        </button>
        <button
          className={`tab ${activeTab === 'salaries' ? 'active' : ''}`}
          onClick={() => setActiveTab('salaries')}
        >
          <Wallet className="w-4 h-4" />
          Salaries
        </button>
      </div>

      <div className="admin-content">
        {activeTab === 'chores' && <ChoreManagement />}
        {activeTab === 'salaries' && <SalaryAdmin />}
      </div>
    </div>
  );
}
