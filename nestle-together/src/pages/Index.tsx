import { Link } from 'react-router-dom';
import { Users, Sparkles, Lock } from 'lucide-react';
import { Button } from '@/components/ui/button';

const Index = () => {
  return (
    <div className="min-h-screen flex flex-col">
      {/* Hero Section */}
      <div className="flex-1 flex flex-col items-center justify-center px-4 py-12">
        <div className="animate-slide-up text-center max-w-lg">
          {/* Logo/Icon */}
          <div className="w-20 h-20 rounded-3xl gradient-monkey flex items-center justify-center mx-auto mb-8 shadow-elevated">
            <span className="text-4xl">🐵</span>
          </div>

          <h1 className="text-4xl md:text-5xl font-extrabold text-foreground mb-4 tracking-tight">
            Chore<span className="text-accent">Monkey</span>
          </h1>

          <p className="text-lg text-muted-foreground mb-8 leading-relaxed">
            Make chores fun! Simple task management for busy families.
            Get everyone swinging into action.
          </p>

          {/* Feature Pills */}
          <div className="flex flex-wrap gap-2 justify-center mb-10">
            {[
              { icon: Users, text: 'Family sharing' },
              { icon: Sparkles, text: 'Fun & easy' },
              { icon: Lock, text: 'Secure PIN' },
            ].map(({ icon: Icon, text }) => (
              <div
                key={text}
                className="flex items-center gap-2 px-4 py-2 rounded-full bg-card shadow-card text-sm font-medium text-foreground"
              >
                <Icon className="w-4 h-4 text-primary" />
                {text}
              </div>
            ))}
          </div>

          {/* CTA Buttons */}
          <div className="flex flex-col sm:flex-row gap-4 justify-center">
            <Link to="/create">
              <Button size="lg" className="w-full sm:w-auto gap-2 text-base shadow-soft h-12 px-8">
                <span className="text-lg">🏠</span>
                Create Household
              </Button>
            </Link>
            <Link to="/join">
              <Button
                size="lg"
                variant="outline"
                className="w-full sm:w-auto gap-2 text-base h-12 px-8"
              >
                <Users className="w-5 h-5" />
                Join Household
              </Button>
            </Link>
          </div>
        </div>
      </div>

      {/* Footer */}
      <footer className="text-center py-6 text-sm text-muted-foreground">
        Made with 🍌 for families everywhere
      </footer>
    </div>
  );
};

export default Index;
