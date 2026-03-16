import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { AdminPanel } from '@/features/admin';

interface AdminPanelDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function SalaryManagementDialog({ open, onOpenChange }: AdminPanelDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>⚙️ Admin Panel</DialogTitle>
        </DialogHeader>
        <AdminPanel />
      </DialogContent>
    </Dialog>
  );
}
