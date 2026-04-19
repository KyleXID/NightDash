#!/usr/bin/env bash
# =============================================================================
# run-editmode-tests.sh — NightDash EditMode Test Runner (S1-03)
#
# Usage:
#   ./scripts/run-editmode-tests.sh
#
# Environment overrides:
#   UNITY_PATH=/path/to/Unity   Override the Unity editor executable path.
#
# Output:
#   Test results XML: <project-root>/Logs/editmode-results.xml
#   Log:              stdout (via -logFile -)
#
# Exit codes:
#   0  All tests passed
#   1  One or more tests failed or Unity returned a non-zero exit code
# =============================================================================

set -euo pipefail

# ---------------------------------------------------------------------------
# Resolve project root (parent of the directory containing this script)
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# ---------------------------------------------------------------------------
# Locate Unity editor executable (macOS Unity Hub convention)
# ---------------------------------------------------------------------------
if [[ -z "${UNITY_PATH:-}" ]]; then
  # Search for any installed Unity version under Unity Hub's default location
  UNITY_SEARCH_BASE="/Applications/Unity/Hub/Editor"
  if [[ -d "${UNITY_SEARCH_BASE}" ]]; then
    # Pick the most recent version (sort -V, take last)
    UNITY_PATH="$(find "${UNITY_SEARCH_BASE}" -maxdepth 2 -name "Unity" -type f \
      | sort -V | tail -n 1)"
  fi
fi

if [[ -z "${UNITY_PATH:-}" || ! -x "${UNITY_PATH}" ]]; then
  echo "[ERROR] Unity executable not found. Set UNITY_PATH explicitly:" >&2
  echo "  UNITY_PATH=/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity \\" >&2
  echo "  ./scripts/run-editmode-tests.sh" >&2
  exit 1
fi

echo "[INFO] Unity  : ${UNITY_PATH}"
echo "[INFO] Project: ${PROJECT_ROOT}"

# ---------------------------------------------------------------------------
# Ensure output directory exists
# ---------------------------------------------------------------------------
mkdir -p "${PROJECT_ROOT}/Logs"
RESULTS_XML="${PROJECT_ROOT}/Logs/editmode-results.xml"

# ---------------------------------------------------------------------------
# Run tests
# ---------------------------------------------------------------------------
echo "[INFO] Running EditMode tests..."

"${UNITY_PATH}" \
  -batchmode \
  -runTests \
  -testPlatform editmode \
  -projectPath "${PROJECT_ROOT}" \
  -testResults "${RESULTS_XML}" \
  -logFile -

EXIT_CODE=$?

if [[ ${EXIT_CODE} -eq 0 ]]; then
  echo "[PASS] EditMode tests completed successfully."
  echo "[INFO] Results: ${RESULTS_XML}"
else
  echo "[FAIL] EditMode tests failed (exit code ${EXIT_CODE})." >&2
  echo "[INFO] Results: ${RESULTS_XML}" >&2
  exit 1
fi
