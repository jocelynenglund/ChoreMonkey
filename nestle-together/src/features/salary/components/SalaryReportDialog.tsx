import { useState, useEffect } from 'react';
import { FileText, AlertCircle, CheckCircle2, MinusCircle } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useAppStore } from '@/features/store';
import * as salaryApi from '../api';
import type { SalarySettings, GenerateSalaryReportResponse } from '../types';

interface SalaryReportDialogProps {
  householdId: string;
}

function getCurrentWeek(): string {
  const now = new Date();
  const startOfYear = new Date(now.getFullYear(), 0, 1);
  const days = Math.floor((now.getTime() - startOfYear.getTime()) / (24 * 60 * 60 * 1000));
  const week = Math.ceil((days + startOfYear.getDay() + 1) / 7);
  return `${now.getFullYear()}-W${week.toString().padStart(2, '0')}`;
}

function getLastWeek(): string {
  const now = new Date();
  now.setDate(now.getDate() - 7);
  const startOfYear = new Date(now.getFullYear(), 0, 1);
  const days = Math.floor((now.getTime() - startOfYear.getTime()) / (24 * 60 * 60 * 1000));
  const week = Math.ceil((days + startOfYear.getDay() + 1) / 7);
  return `${now.getFullYear()}-W${week.toString().padStart(2, '0')}`;
}

function getCurrentMonth(): string {
  const now = new Date();
  return `${now.getFullYear()}-${(now.getMonth() + 1).toString().padStart(2, '0')}`;
}

function getLastMonth(): string {
  const now = new Date();
  now.setMonth(now.getMonth() - 1);
  return `${now.getFullYear()}-${(now.getMonth() + 1).toString().padStart(2, '0')}`;
}

export function SalaryReportDialog({ householdId }: SalaryReportDialogProps) {
  const [open, setOpen] = useState(false);
  const [settings, setSettings] = useState<SalarySettings>({ enabled: false, baseAmount: 800 });
  const [selectedMember, setSelectedMember] = useState<string>('');
  const [selectedPeriod, setSelectedPeriod] = useState<string>(getLastWeek());
  const [report, setReport] = useState<GenerateSalaryReportResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const { members, currentMemberId, isAdmin } = useAppStore();

  useEffect(() => {
    if (open) {
      loadSettings();
      // Default to current member if not admin
      if (!isAdmin && currentMemberId) {
        setSelectedMember(currentMemberId);
      }
    }
  }, [open, householdId]);

  const loadSettings = async () => {
    const data = await salaryApi.fetchSalarySettings(householdId);
    setSettings(data);
  };

  const handleGenerateReport = async () => {
    if (!selectedMember) {
      setError('Please select a member');
      return;
    }

    setError(null);
    setReport(null);
    setIsLoading(true);

    try {
      const result = await salaryApi.generateSalaryReport(householdId, {
        memberId: selectedMember,
        period: selectedPeriod,
      });

      if (result.success) {
        setReport(result);
      } else {
        setError(result.error || 'Failed to generate report');
      }
    } catch (err) {
      setError('Failed to generate report');
    }

    setIsLoading(false);
  };

  const selectedMemberName = members.find(m => m.id === selectedMember)?.nickname || 'Unknown';

  const periodOptions = [
    { value: getLastWeek(), label: 'Last Week' },
    { value: getCurrentWeek(), label: 'This Week (incomplete)' },
    { value: getLastMonth(), label: 'Last Month' },
    { value: getCurrentMonth(), label: 'This Month (incomplete)' },
  ];

  if (!settings.enabled) {
    return null;
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button variant="outline" size="sm" className="gap-2">
          <FileText className="w-4 h-4" />
          Salary Report
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <FileText className="w-5 h-5" />
            Generate Salary Report
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4 mt-4">
          {/* Member Selection */}
          <div className="space-y-2">
            <Label>Member</Label>
            <Select 
              value={selectedMember} 
              onValueChange={setSelectedMember}
              disabled={!isAdmin && !!currentMemberId}
            >
              <SelectTrigger>
                <SelectValue placeholder="Select member" />
              </SelectTrigger>
              <SelectContent>
                {members.map((member) => (
                  <SelectItem key={member.id} value={member.id}>
                    {member.nickname}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Period Selection */}
          <div className="space-y-2">
            <Label>Period</Label>
            <Select value={selectedPeriod} onValueChange={setSelectedPeriod}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {periodOptions.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label} ({option.value})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Generate Button */}
          <Button 
            onClick={handleGenerateReport} 
            disabled={isLoading || !selectedMember}
            className="w-full"
          >
            {isLoading ? 'Generating...' : 'Generate Report'}
          </Button>

          {/* Error */}
          {error && (
            <div className="flex items-center gap-2 p-3 rounded-lg bg-red-50 text-red-700">
              <AlertCircle className="w-4 h-4" />
              {error}
            </div>
          )}

          {/* Report Results */}
          {report && report.success && (
            <div className="space-y-4 p-4 rounded-lg bg-muted">
              <div className="text-center">
                <h3 className="font-semibold text-lg">{selectedMemberName}'s Salary Report</h3>
                <p className="text-sm text-muted-foreground">Period: {report.period}</p>
              </div>

              {/* Base Amount */}
              <div className="flex justify-between items-center py-2 border-b">
                <span>Base Salary</span>
                <span className="font-medium">{report.baseAmount} kr</span>
              </div>

              {/* Deductions */}
              {report.deductions && report.deductions.length > 0 ? (
                <div className="space-y-2">
                  <p className="text-sm font-medium text-red-600 flex items-center gap-1">
                    <MinusCircle className="w-4 h-4" />
                    Deductions ({report.deductions.length})
                  </p>
                  {report.deductions.map((deduction, index) => (
                    <div 
                      key={index} 
                      className="flex justify-between items-center text-sm py-1 pl-4 text-red-600"
                    >
                      <span>{deduction.choreName} ({deduction.missedPeriod})</span>
                      <span>-{deduction.amount} kr</span>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="flex items-center gap-2 py-2 text-green-600">
                  <CheckCircle2 className="w-4 h-4" />
                  <span className="text-sm">No deductions - all chores completed!</span>
                </div>
              )}

              {/* Final Amount */}
              <div className="flex justify-between items-center py-3 border-t-2 border-primary">
                <span className="font-semibold text-lg">Final Amount</span>
                <span className="font-bold text-xl text-primary">{report.finalAmount} kr</span>
              </div>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
