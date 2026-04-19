#!/usr/bin/env bash
# =============================================================================
# run-data-validation.sh — NightDash DataValidator CLI runner (S2-06)
#
# Invokes NightDash.Editor.DataValidator.RunValidation via Unity batchmode.
# In batchmode the validator calls EditorApplication.Exit(0|1) so the
# shell exit code reflects pass/fail and suits CI gate use.
#
# Usage:
#   ./scripts/run-data-validation.sh
#
# Environment overrides:
#   UNITY_PATH=/path/to/Unity   Override the Unity editor executable.
#
# Exit codes:
#   0  All data validation checks passed
#   1  Validation failed or Unity failed to launch
# =============================================================================

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

if [[ -z "${UNITY_PATH:-}" ]]; then
  UNITY_SEARCH_BASE="/Applications/Unity/Hub/Editor"
  if [[ -d "${UNITY_SEARCH_BASE}" ]]; then
    UNITY_PATH="$(find "${UNITY_SEARCH_BASE}" -maxdepth 2 -name "Unity" -type f \
      | sort -V | tail -n 1)"
  fi
fi

if [[ -z "${UNITY_PATH:-}" || ! -x "${UNITY_PATH}" ]]; then
  echo "[ERROR] Unity executable not found. Set UNITY_PATH explicitly." >&2
  exit 1
fi

echo "[INFO] Unity  : ${UNITY_PATH}"
echo "[INFO] Project: ${PROJECT_ROOT}"
echo "[INFO] Running NightDash data validation..."

"${UNITY_PATH}" \
  -batchmode \
  -nographics \
  -projectPath "${PROJECT_ROOT}" \
  -executeMethod NightDash.Editor.DataValidator.RunValidation \
  -logFile -
