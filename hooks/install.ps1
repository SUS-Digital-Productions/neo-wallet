<# Configure git to use the project's hooks/ directory. #>
git config core.hooksPath hooks
Write-Host "Git hooks activated (hooks/ directory)." -ForegroundColor Green
Write-Host "Pre-commit: auto-bumps VERSION patch number."
Write-Host "Post-commit: creates a v{version} tag."
Write-Host "Push with: git push origin main --tags"
