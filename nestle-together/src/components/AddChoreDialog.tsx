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

interface AddChoreDialogProps {
  onAdd: (displayName: string, description: string) => Promise<void> | void;
}

export function AddChoreDialog({ onAdd }: AddChoreDialogProps) {
  const [open, setOpen] = useState(false);
  const [displayName, setDisplayName] = useState('');
  const [description, setDescription] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!displayName.trim()) return;

    setIsSubmitting(true);
    await onAdd(displayName.trim(), description.trim());
    setIsSubmitting(false);
    setDisplayName('');
    setDescription('');
    setOpen(false);
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
