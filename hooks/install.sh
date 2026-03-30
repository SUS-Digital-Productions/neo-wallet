#!/usr/bin/env bash
# Configure git to use the project's hooks/ directory.
git config core.hooksPath hooks
echo "Git hooks activated (hooks/ directory)."
echo "Pre-commit: auto-bumps VERSION patch number."
echo "Post-commit: creates a v{version} tag."
echo "Push with: git push origin main --tags"
