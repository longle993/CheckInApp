#!/usr/bin/env bash
set -euo pipefail
apk=/output/CheckInApp-LAN.apk
tools="$ANDROID_HOME/build-tools/37.0.0"
"$tools/apksigner" verify --verbose --print-certs "$apk" > /output/apk-verification.txt
"$tools/aapt2" dump badging "$apk" > /output/apk-info.txt
"$tools/aapt2" dump xmltree --file AndroidManifest.xml "$apk" > /output/manifest.txt
network_path=$("$tools/aapt2" dump resources "$apk" | sed -n '/xml\/checkin_lan_network/{n;s/.*(file) \([^ ]*\).*/\1/p;}')
test -n "$network_path"
"$tools/aapt2" dump xmltree --file "$network_path" "$apk" > /output/network-security.txt
grep -F "com.checkinapp.mobile.preview" /output/apk-info.txt >/dev/null
if grep -F 'application-debuggable' /output/apk-info.txt; then echo 'Unexpected debuggable APK'; exit 1; fi
if grep -F 'android.permission.RECORD_AUDIO' /output/apk-info.txt; then echo 'Unexpected microphone permission'; exit 1; fi
unzip -p "$apk" assets/index.android.bundle > /tmp/checkin-android.bundle
grep -aF "${EXPO_PUBLIC_API_URL:?Missing expected API endpoint}" /tmp/checkin-android.bundle >/dev/null
expected_host=${EXPO_PUBLIC_API_URL#*://}
expected_host=${expected_host%%[:/]*}
grep -F "$expected_host" /output/network-security.txt >/dev/null
rm /tmp/checkin-android.bundle
sha256sum "$apk" > /output/SHA256SUMS
echo 'APK signature, package, permissions, embedded bundle and API endpoint verified.'
