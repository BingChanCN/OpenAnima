---
phase: quick
plan: 1-github
type: execute
wave: 1
depends_on: []
files_modified: [.gitignore]
autonomous: false
requirements: [GITHUB-REPO]

must_haves:
  truths:
    - "GitHub repository exists and is accessible"
    - "All local commits are pushed to remote"
    - "Main branch is set as default"
  artifacts:
    - path: ".git/config"
      provides: "Remote origin pointing to GitHub"
  key_links:
    - from: "local repo"
      to: "GitHub remote"
      via: "git remote origin"
---

<objective>
Create a GitHub repository for OpenAnima and push all existing code.

Purpose: Get the project hosted on GitHub for version control and collaboration.
Output: Live GitHub repository with all current commits pushed.
</objective>

<context>
Project: OpenAnima — .NET 8 plugin system for autonomous agents.
Current state: Local git repo on `master` branch, no remotes configured.
Note: `gh` CLI is not installed. User will need to create the repo manually on GitHub or install `gh` first.
</context>

<tasks>

<task type="checkpoint:decision" gate="blocking">
  <name>Task 1: Choose repo creation method</name>
  <decision>How to create the GitHub repository</decision>
  <context>The `gh` CLI is not installed. We need to either install it or create the repo manually on GitHub.</context>
  <options>
    <option id="option-a">
      <name>Install gh CLI and create repo via command line</name>
      <pros>Fully automated, can be done from terminal</pros>
      <cons>Requires installing gh and authenticating</cons>
    </option>
    <option id="option-b">
      <name>Create repo manually on github.com then add remote</name>
      <pros>No extra tooling needed, quick if already logged in</pros>
      <cons>Requires browser interaction</cons>
    </option>
  </options>
  <resume-signal>Select: option-a or option-b (and provide your GitHub username)</resume-signal>
</task>

<task type="auto">
  <name>Task 2: Verify .gitignore and clean up build artifacts</name>
  <files>.gitignore</files>
  <action>
    Ensure .gitignore properly excludes bin/, obj/, and other .NET build artifacts.
    Check that no build output (bin/obj directories) is tracked in git.
    If tracked build artifacts exist, untrack them with `git rm -r --cached` for bin/ and obj/ paths.
    Stage and commit the cleanup if any changes were needed.
  </action>
  <verify>Run `git status` — no bin/ or obj/ files should appear as tracked or modified.</verify>
  <done>Repository is clean with proper .gitignore covering all build artifacts.</done>
</task>

<task type="auto">
  <name>Task 3: Add remote and push to GitHub</name>
  <files>.git/config</files>
  <action>
    Based on Task 1 decision:

    If option-a: Install gh CLI (`sudo apt install gh` or equivalent), authenticate with `gh auth login`, then create repo with `gh repo create OpenAnima --public --source=. --remote=origin --push`.

    If option-b: User will have created the repo on GitHub. Add remote with `git remote add origin https://github.com/{username}/OpenAnima.git`. Rename branch to main if needed: `git branch -M main`. Push with `git push -u origin main`.

    Ensure the default branch is `main` (rename from `master` if needed).
  </action>
  <verify>Run `git remote -v` shows GitHub URL. Run `git log origin/main --oneline -3` shows commits on remote.</verify>
  <done>All local commits are pushed to GitHub. Remote tracking is set up for main branch.</done>
</task>

</tasks>

<verification>
- `git remote -v` shows origin pointing to GitHub
- `git status` shows branch is up to date with origin/main
- No build artifacts in the repository
</verification>

<success_criteria>
GitHub repository exists with all OpenAnima code pushed. Local repo tracks remote. Build artifacts excluded.
</success_criteria>

<output>
Confirm the GitHub URL is accessible.
</output>
