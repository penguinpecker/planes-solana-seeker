#!/usr/bin/env bash
# Generates the dedicated keystore the Solana dApp Store requires.
# Per docs.solanamobile.com/dapp-publishing, this keystore MUST be different
# from any Google Play Store signing key, and MUST be kept forever — if it's
# lost you can never update the app on the dApp Store again.

set -euo pipefail

OUT_DIR="${1:-$HOME/.keystores}"
KEYSTORE="${OUT_DIR}/dappstore.keystore"
ALIAS="dappstore"

mkdir -p "${OUT_DIR}"

if [[ -f "${KEYSTORE}" ]]; then
    echo "Refusing to overwrite existing keystore at: ${KEYSTORE}"
    echo "Delete it manually if you really want to regenerate."
    exit 1
fi

keytool -genkey -v \
    -keystore "${KEYSTORE}" \
    -alias "${ALIAS}" \
    -keyalg RSA \
    -keysize 2048 \
    -validity 10000

echo ""
echo "Keystore written to: ${KEYSTORE}"
echo ""
echo "SHA256 fingerprint:"
keytool -list -v -keystore "${KEYSTORE}" -alias "${ALIAS}" | grep -i 'SHA256' || true

echo ""
echo "Next steps:"
echo "  1. Store ${KEYSTORE} and both passwords in a password manager."
echo "  2. In Unity: Edit > Project Settings > Player > Publishing Settings,"
echo "     set the Keystore to ${KEYSTORE} and the alias to '${ALIAS}'."
echo "  3. Build a release APK (Build Settings > Build, NOT Development Build)."
