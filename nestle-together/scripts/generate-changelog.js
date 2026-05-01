import { execSync } from 'child_process';
import { writeFileSync } from 'fs';
import { dirname, join } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(__dirname, '../..');

// Pull the full project history. The /changelog page renders all of this;
// the in-app WhatsNewDialog limits to a recent slice client-side.
const gitLog = execSync(
  'git log --format="%H|%h|%s|%cs|%an"',
  { cwd: repoRoot, encoding: 'utf-8', maxBuffer: 10 * 1024 * 1024 }
);

const commits = gitLog
  .trim()
  .split('\n')
  .filter(line => line.length > 0)
  .map(line => {
    const [hash, shortHash, message, date, author] = line.split('|');
    return { hash, shortHash, message, date, author };
  })
  // Filter for user-facing changes (skip merge commits, CI stuff)
  .filter(c => {
    const msg = c.message.toLowerCase();
    // Skip these
    if (msg.startsWith('merge')) return false;
    if (msg.startsWith('chore:') && msg.includes('ci')) return false;
    if (msg.startsWith('docs:')) return false;
    return true;
  })
  // Categorize
  .map(c => {
    let type = 'update';
    const msg = c.message.toLowerCase();
    
    if (msg.startsWith('feat:') || msg.startsWith('feature:') || msg.includes('add')) {
      type = 'feature';
    } else if (msg.startsWith('fix:') || msg.includes('fix')) {
      type = 'fix';
    } else if (msg.startsWith('refactor:')) {
      type = 'refactor';
    }
    
    // Clean up message (remove conventional commit prefix)
    let cleanMessage = c.message
      .replace(/^(feat|fix|chore|refactor|docs|style|test):\s*/i, '')
      .replace(/^(feature):\s*/i, '');
    
    // Capitalize first letter
    cleanMessage = cleanMessage.charAt(0).toUpperCase() + cleanMessage.slice(1);
    
    return {
      ...c,
      type,
      displayMessage: cleanMessage
    };
  });

const changelog = {
  generated: new Date().toISOString(),
  commits
};

const outputPath = join(__dirname, '../public/changelog.json');
writeFileSync(outputPath, JSON.stringify(changelog, null, 2));

console.log(`✓ Generated changelog with ${commits.length} entries`);
