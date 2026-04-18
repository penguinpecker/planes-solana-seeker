#!/usr/bin/env bash
# Signs and builds planes-release.apk with the dApp-Store keystore.
# Prompts interactively for the two passwords so they stay out of your shell
# history. Always runs with bash so the `read -s -p` syntax works regardless
# of whether you invoke it from zsh.

set -euo pipefail

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$PROJECT_ROOT"

export PLANES_KEYSTORE_PATH="${PLANES_KEYSTORE_PATH:-$HOME/.keystores/dappstore.keystore}"
export PLANES_KEYALIAS_NAME="${PLANES_KEYALIAS_NAME:-dappstore}"
export PLANES_APK_OUTPUT="${PLANES_APK_OUTPUT:-$PROJECT_ROOT/Builds/planes-release.apk}"

if [[ ! -f "$PLANES_KEYSTORE_PATH" ]]; then
    echo "ERROR: keystore not found at $PLANES_KEYSTORE_PATH" >&2
    exit 1
fi

read -s -p "Keystore password:  " PLANES_KEYSTORE_PASS; echo
read -s -p "Key alias password (Enter = same): " PLANES_KEYALIAS_PASS; echo
: "${PLANES_KEYALIAS_PASS:=$PLANES_KEYSTORE_PASS}"
export PLANES_KEYSTORE_PASS PLANES_KEYALIAS_PASS

UNITY="/Applications/Unity/Hub/Editor/6000.4.2f1/Unity.app/Contents/MacOS/Unity"
if [[ ! -x "$UNITY" ]]; then
    echo "ERROR: Unity 6000.4.2f1 not found at $UNITY" >&2
    exit 1
fi

mkdir -p "$(dirname "$PLANES_APK_OUTPUT")"

echo "Building release APK (signed with $PLANES_KEYSTORE_PATH) — this takes a few minutes."
"$UNITY" \
    -batchmode -quit -nographics \
    -projectPath "$PROJECT_ROOT" \
    -buildTarget Android \
    -executeMethod BuildScript.BuildAndroid \
    -keystorePath "$PLANES_KEYSTORE_PATH" \
    -keystorePass "$PLANES_KEYSTORE_PASS" \
    -keyaliasName "$PLANES_KEYALIAS_NAME" \
    -keyaliasPass "$PLANES_KEYALIAS_PASS" \
    -logFile /tmp/planes-build.log

STATUS=$?
unset PLANES_KEYSTORE_PASS PLANES_KEYALIAS_PASS

# Unity 6 batch-mode segfaults during shutdown (exit 139) AFTER the APK is
# written. Check the artifact on disk, not the exit code — if the APK exists
# and has content, the build succeeded.
if [[ -s "$PLANES_APK_OUTPUT" ]]; then
    touch "$PLANES_APK_OUTPUT"
    echo ""
    echo "Signed APK:"
    ls -lh "$PLANES_APK_OUTPUT"
    exit 0
fi

echo "Build failed — APK not produced. Last 40 log lines:" >&2
tail -40 /tmp/planes-build.log >&2
exit 1
