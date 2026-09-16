# Release Process Guide

A checklist for shipping a versioned release from a git repo to GitHub Releases.
Commands below are .NET/dotnet examples (from AuraTxt) — swap the build commands
for whatever your project uses; the surrounding workflow is what's reusable.

## 0. Ground rules

- **Never build/publish/push without an explicit request.** A verified fix is not
  implicit permission to release — releasing is a separate decision on its own cadence.
- **Never push --force or move an existing tag without explicit confirmation** —
  these rewrite shared/published state and can't be cleanly undone.
- **Stage commits surgically.** Never `git add -A` / `git add .`. Add only the files
  that belong to the change; leave unrelated untracked cruft alone.
- **Disclose known open issues before shipping**, don't silently ship around them.
  Let the person decide whether to release anyway.

## 1. Pre-flight

```sh
git status --porcelain      # see what's uncommitted, and what's untracked cruft to exclude
git diff --stat             # sanity-check the size/shape of the change
dotnet test                 # or your project's test runner — must be green before committing
```

Decide the version number **before** touching anything:
- New version → bump the version field in the project file, e.g. `<Version>2.1</Version>`.
- Reusing an existing version tag (e.g. shipping a follow-up fix batch under the same
  number) → this means force-moving an already-published tag and overwriting existing
  release assets. Flag this explicitly and get confirmation — it's a deviation from
  "always increment" and has real trade-offs (existing downloaders get no update signal,
  the tag no longer means one fixed thing).

## 2. Commit

```sh
git add <specific files>          # never a blanket add
git commit -m "$(cat <<'EOF'
<type>(<scope>): <one-line summary>

- <bullet per meaningful change, why not just what>
- ...

Co-Authored-By: <attribution line your tooling requires>
EOF
)"
```

Write the commit message as a list of *what changed and why it mattered*, not a diff
narration — this becomes the seed for the release notes later.

## 3. Push commit + tag

```sh
git push                          # push the commit first, on its own
```

**New version:**
```sh
git tag v2.1
git push origin v2.1
```

**Reusing an existing tag** (only after explicit confirmation — this is destructive):
```sh
git tag -f v2.0
git push origin v2.0 --force
```

## 4. Build release artifacts

Two build modes are typical for a desktop app: framework-dependent (small, needs a
runtime installed) and self-contained (large, no runtime needed). Example (dotnet):

```sh
# Framework-dependent
dotnet publish App/App.csproj -c Release -r win-x64 --self-contained false \
  -p:PublishSingleFile=true -o publish/release
# then delete .pdb files — not shipped

# Self-contained
dotnet publish App/App.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish/release
```

**Gotchas that cost real time if missed:**
- `PublishSingleFile=true` matters on *both* modes — omitting it produces a folder of
  loose dependency DLLs instead of one exe.
- **Switching `--self-contained` mode against the same output directory triggers the
  SDK's stale-output cleanup**, which deletes files it doesn't consider its own output
  (config files, readme, bundled assets/icons/themes/etc. that live alongside the exe).
  Back those up before republishing in a different mode, or just re-copy them after.
- After building, copy any static files that ship alongside the exe (readme, default
  config, assets) into the output directory *before* zipping — they aren't produced by
  `dotnet publish` itself.
- Zip each package to a location **outside** the publish output directory (e.g. a scratch
  dir), so the next build (different mode) doesn't wipe out a zip you already made.

## 5. Publish to GitHub

```sh
# New release
gh release create v2.1 <zip1> <zip2> --title "App v2.1" --notes "$(cat <<'EOF'
## What's new
- ...

## Upgrading
- ...

## Downloads
| Package | Size | Requires |
|---|---|---|
| ... |
EOF
)"

# Updating an existing release (reused-tag case)
gh release upload v2.0 <zip1> <zip2> --clobber
gh release edit v2.0 --notes "..."   # append the new fixes, keep prior notes for context
```

Release notes structure that's worked well:
- **What's new** — one bullet per fix/feature, written as *symptom → root cause → what
  changed*, not just "fixed X". This is what users actually read to decide if it matters
  to them.
- **Known issues** — anything you're shipping around, stated plainly, so it's not a
  surprise.
- **Upgrading** — anything the user must do manually (don't overwrite config, etc.).
- **Downloads** — a table mapping each asset to size + requirements, so the user picks
  the right one without opening either zip.

If the version number is new, also update any README/docs download links to point at it.

## 6. Post-release cleanup

```sh
# Remove build output from the release folder, keep only persistent runtime files
rm -f publish/release/*.exe publish/release/*.dll
ls publish/release   # should be left with only config/assets that aren't build output
```

Don't leave large compiled binaries sitting in a working directory after they've been
uploaded — they're reproducible from source, and there's no reason to keep them around
locally once they're on the release.

## 7. Sanity checklist before calling it done

- [ ] Tests pass
- [ ] Commit pushed, tag pushed, both point at the same commit
- [ ] Both/all release assets uploaded and correct size (sanity-check, not just "did it run")
- [ ] Release notes accurately describe what changed, including known issues
- [ ] README/docs download links updated if the version number changed
- [ ] Local release-output directory cleaned of build artifacts
