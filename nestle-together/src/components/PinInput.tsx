import { useRef, useState, useEffect } from 'react';
import { cn } from '@/lib/utils';

interface PinInputProps {
  length?: number;
  onComplete: (pin: string) => void;
  error?: boolean;
  disabled?: boolean;
}

export function PinInput({
  length = 4,
  onComplete,
  error = false,
  disabled = false,
}: PinInputProps) {
  const [values, setValues] = useState<string[]>(Array(length).fill(''));
  const inputRefs = useRef<(HTMLInputElement | null)[]>([]);

  useEffect(() => {
    inputRefs.current[0]?.focus();
  }, []);

  useEffect(() => {
    if (error) {
      setValues(Array(length).fill(''));
      inputRefs.current[0]?.focus();
    }
  }, [error, length]);

  const handleChange = (index: number, value: string) => {
    if (disabled) return;
    
    const digit = value.replace(/\D/g, '').slice(-1);
    const newValues = [...values];
    newValues[index] = digit;
    setValues(newValues);

    if (digit && index < length - 1) {
      inputRefs.current[index + 1]?.focus();
    }

    if (newValues.every((v) => v) && newValues.length === length) {
      onComplete(newValues.join(''));
    }
  };

  const handleKeyDown = (index: number, e: React.KeyboardEvent) => {
    if (disabled) return;
    
    if (e.key === 'Backspace' && !values[index] && index > 0) {
      inputRefs.current[index - 1]?.focus();
    }
  };

  const handlePaste = (e: React.ClipboardEvent) => {
    if (disabled) return;
    
    e.preventDefault();
    const pasted = e.clipboardData.getData('text').replace(/\D/g, '').slice(0, length);
    if (pasted.length === length) {
      setValues(pasted.split(''));
      onComplete(pasted);
    }
  };

  return (
    <div className="flex gap-3 justify-center">
      {values.map((value, index) => (
        <input
          key={index}
          ref={(el) => {
            inputRefs.current[index] = el;
          }}
          type="tel"
          inputMode="numeric"
          pattern="[0-9]*"
          autoComplete="one-time-code"
          maxLength={1}
          value={value}
          onChange={(e) => handleChange(index, e.target.value)}
          onKeyDown={(e) => handleKeyDown(index, e)}
          onPaste={handlePaste}
          onFocus={(e) => e.target.select()}
          disabled={disabled}
          className={cn(
            'w-14 h-16 text-center text-2xl font-bold rounded-xl border-2 bg-card transition-all duration-200 outline-none',
            error
              ? 'border-destructive bg-destructive/5 animate-shake'
              : 'border-input focus:border-primary focus:ring-2 focus:ring-primary/20',
            disabled && 'opacity-50 cursor-not-allowed'
          )}
        />
      ))}
    </div>
  );
}
