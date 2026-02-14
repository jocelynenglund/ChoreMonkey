import { useState } from 'react';
import { Plus } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Textarea } from '@/components/ui/textarea';
import { Label } from '@/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import type { ChoreFrequency } from '@/types/household';

interface AddChoreDialogProps {
  onAdd: (displayName: string, description: string, frequency?: ChoreFrequency) => Promise<void> | void;
}

const DAYS_OF_WEEK = [
  { value: 'monday', label: 'Mon' },
  { value: 'tuesday', label: 'Tue' },
  { value: 'wednesday', label: 'Wed' },
  { value: 'thursday', label: 'Thu' },
  { value: 'friday', label: 'Fri' },
  { value: 'saturday', label: 'Sat' },
  { value: 'sunday', label: 'Sun' },
];

export function AddChoreDialog({ onAdd }: AddChoreDialogProps) {
  const [open, setOpen] = useState(false);
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [frequencyType, setFrequencyType] = useState<ChoreFrequency['type']>('once');
  const [selectedDays, setSelectedDays] = useState<string[]>([]);
  const [intervalDays, setIntervalDays] = useState('3');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!displayName.trim()) return;

    const frequency: ChoreFrequency = { type: frequencyType };
    if (frequencyType === 'weekly' && selectedDays.length > 0) {
      frequency.days = selectedDays;
    }
    if (frequencyType === 'interval') {
      frequency.intervalDays = parseInt(intervalDays, 10) || 3;
    }

    setIsSubmitting(true);
    await onAdd(displayName.trim(), description.trim(), frequency);
    setIsSubmitting(false);
    setDisplayName('');
    setDescription('');
    setFrequencyType('once');
    setSelectedDays([]);
    setIntervalDays('3');
    setOpen(false);
  };

  const toggleDay = (day: string) => {
    setSelectedDays((prev) =>
      prev.includes(day) ? prev.filter((d) => d !== day) : [...prev, day]
    );
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="gap-2 shadow-soft">
          <Plus className="w-5 h-5" />
          Add Chore
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="text-xl">Add New Chore</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4 mt-4">
          <div className="space-y-2">
            <Label htmlFor="displayName">Chore Name</Label>
            <Input
              id="displayName"
              placeholder="e.g., Wash dishes"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              className="h-12"
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="description">Description (optional)</Label>
            <Textarea
              id="description"
              placeholder="Any additional details..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
            />
          </div>
          <div className="space-y-2">
            <Label>How often?</Label>
            <Select value={frequencyType} onValueChange={(v) => setFrequencyType(v as ChoreFrequency['type'])}>
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="once">One-time task</SelectItem>
                <SelectItem value="daily">Every day</SelectItem>
                <SelectItem value="weekly">Specific days</SelectItem>
                <SelectItem value="interval">Every X days</SelectItem>
              </SelectContent>
            </Select>
          </div>
          
          {frequencyType === 'weekly' && (
            <div className="space-y-2">
              <Label>Select days</Label>
              <div className="flex flex-wrap gap-2">
                {DAYS_OF_WEEK.map((day) => (
                  <label
                    key={day.value}
                    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full border cursor-pointer transition-colors ${
                      selectedDays.includes(day.value)
                        ? 'bg-primary text-primary-foreground border-primary'
                        : 'hover:bg-muted'
                    }`}
                  >
                    <Checkbox
                      checked={selectedDays.includes(day.value)}
                      onCheckedChange={() => toggleDay(day.value)}
                      className="hidden"
                    />
                    {day.label}
                  </label>
                ))}
              </div>
            </div>
          )}

          {frequencyType === 'interval' && (
            <div className="space-y-2">
              <Label htmlFor="intervalDays">Every how many days?</Label>
              <Input
                id="intervalDays"
                type="number"
                min="1"
                max="365"
                value={intervalDays}
                onChange={(e) => setIntervalDays(e.target.value)}
                className="h-12 w-24"
              />
            </div>
          )}

          <div className="flex justify-end gap-3 pt-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => setOpen(false)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={!displayName.trim() || isSubmitting}>
              {isSubmitting ? 'Adding...' : 'Add Chore'}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
