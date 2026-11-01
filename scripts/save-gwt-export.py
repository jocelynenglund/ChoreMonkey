#!/usr/bin/env python3
"""Combine a Given/When/Then export into one gwt-export.json and keep it on the
runner's machine, so every deploy leaves the app's eventPAD model behind.

  save-gwt-export.py [--app NAME] DIR      one app: DIR holds its <File>.test.json files
  save-gwt-export.py --modules DIR         one app per subfolder of DIR, named after it
  options: --save / --no-save              force saving on or off (default: see below)

DIR is what the tests were run with as GWT_EXPORT=DIR. The combined document,
  {"app", "repo", "build", "commit", "createdAt",
   "files": [{"file": "GuestWishesSpecs.test.json", "scenarios": [...]}, ...]}
keeps each file's name, in name order; a file that is already an object with
"scenarios" contributes that list. Scenarios are passed through untouched, so the
"spec" {file, line} and "slice" {type, method, file, line} that
ItsyBIT.EventStore.Testing 0.5 adds to each scenario are kept. The same shape as
model_check.py's. "repo" is "<owner>/<name>" from GITHUB_REPOSITORY, else
parsed from the origin remote (https or ssh), and left out if neither is known.
"build" is the CI run number as a string (GITHUB_RUN_NUMBER; eventPAD's library
shows it as "build N"), and left out when that is unset, as it is locally.
It is written to DIR/gwt-export.json and, when saving, to
  ~/eventpad-models/<app>/<yyyy-mm-dd>-<app>-<short sha>-built.json  (the dated snapshot)
  .../latest-built.json                                              (the name to read)
  .../latest.json                                                    (an alias, for now)
keeping the newest 20 dated built snapshots per app (older ones saved without the
"-built" suffix are pruned the same way). <date>-<app>-plan.json files (board
exports) may sit in the same folder; they and latest* are never touched. Same
folder and names as the /release skill's model_check.py, so CI and /release land
in one history.

Saving happens by default only on a self-hosted runner (RUNNER_ENVIRONMENT) and
not for pull_request runs; elsewhere the export is combined and summarised only.
The app defaults to the repository's name (GITHUB_REPOSITORY, else the git remote).

The tests are the gate, not this script: a problem here is a warning and exit 0,
so a full disk never blocks a deploy whose tests passed. Stdlib only.
Identical in wishbook, itsybit-secretsanta, barrio and event-store-api.
"""
import datetime, glob, json, os, shutil, subprocess, sys

KEEP = 20


def warn(msg):
    print(f'::warning::{msg}' if os.environ.get('GITHUB_ACTIONS') else f'warning: {msg}', file=sys.stderr)


def git(*args):
    try:
        r = subprocess.run(['git', *args], capture_output=True, text=True)
        return r.stdout.strip() if r.returncode == 0 else ''
    except OSError:
        return ''


def slug(name):
    return ''.join(ch if ch.isalnum() or ch in '-_.' else '-' for ch in name.lower()).strip('-')


def repo_name():
    repo = os.environ.get('GITHUB_REPOSITORY', '').strip()
    if repo:
        return repo
    url = git('remote', 'get-url', 'origin').rstrip('/').removesuffix('.git')
    if not url:
        return None
    # https://github.com/owner/name, ssh://git@host/owner/name, git@host:owner/name
    path = url.split('://', 1)[1].split('/', 1)[-1] if '://' in url else url.split(':', 1)[-1]
    parts = [p for p in path.split('/') if p]
    return '/'.join(parts[-2:]) if len(parts) >= 2 else None


def default_app():
    repo = os.environ.get('GITHUB_REPOSITORY') or git('remote', 'get-url', 'origin')
    return os.path.basename(repo.rstrip('/').removesuffix('.git')) or os.path.basename(os.getcwd())


def combine(app, export_dir, commit):
    files = sorted(glob.glob(os.path.join(export_dir, '*.test.json')))
    entries = []
    for f in files:
        try:
            with open(f, encoding='utf-8') as fh:
                data = json.load(fh)
        except (OSError, ValueError) as e:
            warn(f'{app}: skipped {os.path.basename(f)} ({e})')
            continue
        items = data if isinstance(data, list) else data.get('scenarios', []) if isinstance(data, dict) else []
        entries.append({'file': os.path.basename(f), 'scenarios': items})
    doc = {'app': app}
    repo = repo_name()
    if repo:
        doc['repo'] = repo
    build = os.environ.get('GITHUB_RUN_NUMBER', '').strip()
    if build:
        doc['build'] = build
    doc.update({
        'commit': commit,
        'createdAt': datetime.datetime.now(datetime.timezone.utc).isoformat(timespec='seconds'),
        'files': entries,
    })
    scenarios = sum(len(e['scenarios']) for e in entries)
    return doc, scenarios


def save(app, doc):
    base = os.path.join(os.path.expanduser('~/eventpad-models'), slug(app))
    os.makedirs(base, exist_ok=True)
    name = f"{datetime.date.today().isoformat()}-{slug(app)}-{(doc['commit'] or 'local')[:7]}-built.json"
    path = os.path.join(base, name)
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(doc, f, indent=1, ensure_ascii=False)
    # latest-built.json is the name; latest.json stays as an alias for now
    for alias in ('latest-built.json', 'latest.json'):
        shutil.copyfile(path, os.path.join(base, alias))
    # Prune dated built snapshots only (and older ones without a suffix); never plans or latest*
    dated = [p for p in glob.glob(os.path.join(base, '*.json'))
             if not os.path.basename(p).startswith('latest') and not p.endswith('-plan.json')]
    for old in sorted(dated, key=os.path.getmtime, reverse=True)[KEEP:]:
        os.remove(old)
    return path


def one(app, export_dir, commit, do_save):
    doc, scenarios = combine(app, export_dir, commit)
    if not doc['files']:
        warn(f'{app}: no *.test.json in {export_dir} — were the tests run with GWT_EXPORT? Nothing saved.')
        return
    try:
        with open(os.path.join(export_dir, 'gwt-export.json'), 'w', encoding='utf-8') as f:
            json.dump(doc, f, indent=1, ensure_ascii=False)
        where = save(app, doc) if do_save else 'not saved (not a self-hosted push run)'
    except OSError as e:
        warn(f'{app}: could not save the model ({e})')
        return
    line = f"{app} @ {(commit or 'local')[:7]}: {len(doc['files'])} spec files, {scenarios} scenarios -> {where}"
    print(line)
    summary = os.environ.get('GITHUB_STEP_SUMMARY')
    if summary:
        with open(summary, 'a', encoding='utf-8') as f:
            f.write(f'- eventPAD model: {line}\n')


def main(argv):
    app, modules, forced, dirs = None, False, None, []
    it = iter(argv)
    for a in it:
        if a == '--app':
            app = next(it, None)
        elif a == '--modules':
            modules = True
        elif a in ('--save', '--no-save'):
            forced = a == '--save'
        elif a in ('-h', '--help'):
            print(__doc__)
            return 0
        else:
            dirs.append(a)
    if len(dirs) != 1 or (modules and app):
        print(__doc__, file=sys.stderr)
        return 2

    commit = os.environ.get('GITHUB_SHA') or git('rev-parse', 'HEAD')
    do_save = forced if forced is not None else (
        os.environ.get('RUNNER_ENVIRONMENT') == 'self-hosted'
        and os.environ.get('GITHUB_EVENT_NAME') != 'pull_request')

    root = dirs[0]
    if not os.path.isdir(root):
        warn(f'{root} does not exist — were the tests run with GWT_EXPORT? Nothing saved.')
        return 0
    if modules:
        subs = sorted(d for d in os.listdir(root) if os.path.isdir(os.path.join(root, d)))
        if not subs:
            warn(f'no module folders in {root}. Nothing saved.')
        for d in subs:
            one(d, os.path.join(root, d), commit, do_save)
    else:
        one(app or default_app(), root, commit, do_save)
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
