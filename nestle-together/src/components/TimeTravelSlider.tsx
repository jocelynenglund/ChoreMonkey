import { useState, useEffect } from 'react';
import { Clock, RotateCcw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Slider } from '@/components/ui/slider';
import { useHouseholdStore } from '@/stores/householdStore';

interface TimeTravelSliderProps {
  onTimeChange?: () => void;
}

export function TimeTravelSlider({ onTimeChange }: TimeTravelSliderProps) {
  const { asOf, setAsOf, isAdmin } = useHouseholdStore();
  const [isOpen, setIsOpen] = useState(false);
  
  // Slider value: 0 = 30 days ago, 100 = now
  const [sliderValue, setSliderValue] = useState(100);
  
  const now = new Date();
  const thirtyDaysAgo = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
  
  // Convert slider value to date
  const sliderToDate = (value: number): Date | null => {
    if (value >= 100) return null; // null = present
    const range = now.getTime() - thirtyDaysAgo.getTime();
    return new Date(thirtyDaysAgo.getTime() + (value / 100) * range);
  };
  
  // Format date for display
  const formatDate = (date: Date | null): string => {
    if (!date) return 'Now';
    const diff = now.getTime() - date.getTime();
    const days = Math.floor(diff / (24 * 60 * 60 * 1000));
    const hours = Math.floor((diff % (24 * 60 * 60 * 1000)) / (60 * 60 * 1000));
    
    if (days === 0 && hours === 0) return 'Just now';
    if (days === 0) return `${hours}h ago`;
    if (days === 1) return 'Yesterday';
    return `${days} days ago`;
  };

  const handleSliderChange = (value: number[]) => {
    setSliderValue(value[0]);
    const newDate = sliderToDate(value[0]);
    setAsOf(newDate);
  };

  const handleReset = () => {
    setSliderValue(100);
    setAsOf(null);
  };

  // Trigger refresh when asOf changes
  useEffect(() => {
    onTimeChange?.();
  }, [asOf, onTimeChange]);

  if (!isAdmin) return null;

  return (
    <div className="mb-4">
      <Button
        variant="outline"
        size="sm"
        onClick={() => setIsOpen(!isOpen)}
        className={asOf ? 'border-amber-500 text-amber-600' : ''}
      >
        <Clock className="h-4 w-4 mr-2" />
        {asOf ? `🕰️ ${formatDate(asOf)}` : 'Time Travel'}
      </Button>

      {isOpen && (
        <div className="mt-3 p-4 bg-muted/50 rounded-lg border">
          <div className="flex items-center justify-between mb-3">
            <span className="text-sm font-medium">
              {asOf ? `Viewing: ${asOf.toLocaleDateString()} ${asOf.toLocaleTimeString()}` : 'Present'}
            </span>
            {asOf && (
              <Button variant="ghost" size="sm" onClick={handleReset}>
                <RotateCcw className="h-4 w-4 mr-1" />
                Back to now
              </Button>
            )}
          </div>
          
          <div className="flex items-center gap-4">
            <span className="text-xs text-muted-foreground whitespace-nowrap">30d ago</span>
            <Slider
              value={[sliderValue]}
              onValueChange={handleSliderChange}
              max={100}
              step={1}
              className="flex-1"
            />
            <span className="text-xs text-muted-foreground">Now</span>
          </div>
          
          {asOf && (
            <p className="mt-2 text-xs text-amber-600">
              ⚠️ Viewing historical data. Some features are read-only.
            </p>
          )}
        </div>
      )}
    </div>
  );
}
