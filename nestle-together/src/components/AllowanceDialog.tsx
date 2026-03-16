import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { MyAllowance } from '@/features/salary/components/MyAllowance';

interface AllowanceDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function AllowanceDialog({ open, onOpenChange }: AllowanceDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>My Allowance</DialogTitle>
        </DialogHeader>
        <MyAllowance />
      </DialogContent>
    </Dialog>
  );
}
