import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowLeft, Sparkles, Server, Monitor } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { ChangelogList, type Changelog as ChangelogData } from '@/components/ChangelogList';

interface ApiVersion {
  version: string;
  buildTime: string;
  gitSha: string;
}

const API_BASE_URL = import.meta.env.VITE_API_URL || 'https://localhost:7422';

export default function Changelog() {
  const [changelog, setChangelog] = useState<ChangelogData | null>(null);
  const [apiVersion, setApiVersion] = useState<ApiVersion | null>(null);

  const frontendVersion = {
    version: typeof __BUILD_VERSION__ !== 'undefined' ? __BUILD_VERSION__ : 'dev',
    gitSha: typeof __GIT_SHA__ !== 'undefined' ? __GIT_SHA__ : 'local',
    buildTime: typeof __BUILD_TIME__ !== 'undefined' ? __BUILD_TIME__ : 'unknown',
  };

  useEffect(() => {
    fetch('/changelog.json')
      .then((res) => res.json())
      .then((data: ChangelogData) => setChangelog(data))
      .catch((err) => console.error('Failed to load changelog', err));

    fetch(`${API_BASE_URL}/api/version`)
      .then((res) => res.json())
      .then((data: ApiVersion) => setApiVersion(data))
      .catch((err) => console.error('Failed to load API version', err));
  }, []);

  return (
    <div className="min-h-screen flex flex-col">
      <header className="border-b">
        <div className="max-w-2xl mx-auto px-4 py-4 flex items-center gap-3">
          <Link to="/">
            <Button variant="ghost" size="icon" aria-label="Back to home">
              <ArrowLeft className="w-5 h-5" />
            </Button>
          </Link>
          <div className="flex items-center gap-2">
            <Sparkles className="w-5 h-5 text-primary" />
            <h1 className="text-xl font-semibold">What's New</h1>
          </div>
        </div>
        <div className="max-w-2xl mx-auto px-4 pb-3 flex flex-wrap gap-3 text-xs text-muted-foreground">
          <div
            className="flex items-center gap-1.5"
            title={`SHA: ${frontendVersion.gitSha}\nBuilt: ${frontendVersion.buildTime}`}
          >
            <Monitor className="w-3.5 h-3.5" />
            <span>Web:</span>
            <code className="bg-muted px-1.5 py-0.5 rounded font-mono">{frontendVersion.version}</code>
          </div>
          <div
            className="flex items-center gap-1.5"
            title={apiVersion ? `SHA: ${apiVersion.gitSha}\nBuilt: ${apiVersion.buildTime}` : ''}
          >
            <Server className="w-3.5 h-3.5" />
            <span>API:</span>
            <code className="bg-muted px-1.5 py-0.5 rounded font-mono">{apiVersion?.version || '...'}</code>
          </div>
        </div>
      </header>

      <main className="flex-1 max-w-2xl w-full mx-auto px-4 py-6">
        <ChangelogList autoLoad={false} changelog={changelog} />
      </main>
    </div>
  );
}
