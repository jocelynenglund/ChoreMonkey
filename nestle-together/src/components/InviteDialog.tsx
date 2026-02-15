import { useState } from 'react';
import { Copy, Check, UserPlus, Link } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import type { Invite } from '@/types/household';

interface InviteDialogProps {
  onGenerate: () => Promise<Invite | null>;
}

export function InviteDialog({ onGenerate }: InviteDialogProps) {
  const [open, setOpen] = useState(false);
  const [invite, setInvite] = useState<Invite | null>(null);
  const [copied, setCopied] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);

  const handleGenerate = async () => {
    setIsGenerating(true);
    const newInvite = await onGenerate();
    setInvite(newInvite);
    setIsGenerating(false);
  };

  const handleCopy = async () => {
    if (!invite) return;
    await navigator.clipboard.writeText(invite.link);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleOpenChange = (isOpen: boolean) => {
    setOpen(isOpen);
    if (!isOpen) {
      setInvite(null);
      setCopied(false);
      setIsGenerating(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogTrigger asChild>
        <Button variant="outline" className="gap-2">
          <UserPlus className="w-5 h-5" />
          Invite
        </Button>
      </DialogTrigger>
      <DialogContent className="sm:max-w-md max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-xl">Invite Family Member</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 mt-4">
          {!invite ? (
            <div className="text-center py-6">
              <div className="w-16 h-16 rounded-full bg-primary/10 flex items-center justify-center mx-auto mb-4">
                <Link className="w-8 h-8 text-primary" />
              </div>
              <p className="text-muted-foreground mb-6">
                Generate a unique invite link to share with family members
              </p>
              <Button onClick={handleGenerate} disabled={isGenerating} className="gap-2">
                <UserPlus className="w-5 h-5" />
                {isGenerating ? 'Generating...' : 'Generate Invite Link'}
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              <div className="p-4 rounded-xl bg-success/10 border border-success/20">
                <p className="text-sm font-medium text-success mb-1">
                  Invite Code
                </p>
                <p className="text-2xl font-bold tracking-wider text-success">
                  {invite.id}
                </p>
              </div>
              <div className="space-y-2">
                <p className="text-sm text-muted-foreground">
                  Or share this link:
                </p>
                <div className="flex gap-2">
                  <Input
                    readOnly
                    value={invite.link}
                    className="font-mono text-sm"
                  />
                  <Button
                    onClick={handleCopy}
                    variant={copied ? 'default' : 'outline'}
                    className="flex-shrink-0"
                  >
                    {copied ? (
                      <Check className="w-5 h-5" />
                    ) : (
                      <Copy className="w-5 h-5" />
                    )}
                  </Button>
                </div>
              </div>
              <p className="text-xs text-muted-foreground text-center">
                This link expires in 7 days
              </p>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
