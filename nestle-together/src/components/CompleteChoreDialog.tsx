import { useState } from 'react';
import { format } from 'date-fns';
import { CalendarIcon, Check } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Calendar } from '@/components/ui/calendar';
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from '@/components/ui/popover';
import { cn } from '@/lib/utils';
import type { Chore } from '@/types/household';

interface CompleteChoreDialogProps {
  chore: Chore | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onComplete: (choreId: string, completedAt?: Date) => Promise<void>;
}

export function CompleteChoreDialog({
  chore,
  open,
  onOpenChange,
  onComplete,
}: CompleteChoreDialogProps) {
  const [selectedDate, setSelectedDate] = useState<Date>(new Date());
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleComplete = async () => {
    if (!chore) return;
    setIsSubmitting(true);
    await onComplete(chore.id, selectedDate);
    setIsSubmitting(false);
    onOpenChange(false);
    // Reset to today for next time
    setSelectedDate(new Date());
  };

  const isToday = (date: Date) => {
    const today = new Date();
    return (
      date.getDate() === today.getDate() &&
      date.getMonth() === today.getMonth() &&
      date.getFullYear() === today.getFullYear()
    );
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="text-xl flex items-center gap-2">
            <Check className="w-5 h-5 text-success" />
            Mark as Done
          </DialogTitle>
        </DialogHeader>

        {chore && (
          <div className="space-y-4 mt-2">
            <div className="p-3 rounded-lg bg-muted">
              <p className="font-medium">{chore.displayName}</p>
              {chore.description && (
                <p className="text-sm text-muted-foreground mt-1">
                  {chore.description}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">When was it completed?</label>
              <Popover>
                <PopoverTrigger asChild>
                  <Button
                    variant="outline"
                    className={cn(
                      'w-full justify-start text-left font-normal h-12',
                      !selectedDate && 'text-muted-foreground'
                    )}
                  >
                    <CalendarIcon className="mr-2 h-4 w-4" />
                    {selectedDate ? (
                      isToday(selectedDate) ? (
                        'Today'
                      ) : (
                        format(selectedDate, 'EEEE, MMMM d')
                      )
                    ) : (
                      <span>Pick a date</span>
                    )}
                  </Button>
                </PopoverTrigger>
                <PopoverContent className="w-auto p-0" align="start">
                  <Calendar
                    mode="single"
                    selected={selectedDate}
                    onSelect={(date) => date && setSelectedDate(date)}
                    disabled={(date) => date > new Date()}
                    initialFocus
                  />
                </PopoverContent>
              </Popover>
              <p className="text-xs text-muted-foreground">
                Use this to catch up on missed days
              </p>
            </div>

            <div className="flex justify-end gap-3 pt-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => onOpenChange(false)}
              >
                Cancel
              </Button>
              <Button
                onClick={handleComplete}
                disabled={isSubmitting}
                className="gap-2"
              >
                {isSubmitting ? (
                  'Saving...'
                ) : (
                  <>
                    <Check className="w-4 h-4" />
                    Complete
                  </>
                )}
              </Button>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
