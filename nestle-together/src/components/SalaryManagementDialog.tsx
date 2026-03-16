import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { SalaryAdmin } from '@/features/salary/components/SalaryAdmin';

interface SalaryManagementDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function SalaryManagementDialog({ open, onOpenChange }: SalaryManagementDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Salary Management</DialogTitle>
        </DialogHeader>
        <SalaryAdmin />
      </DialogContent>
    </Dialog>
  );
}
