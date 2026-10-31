#!/usr/bin/env bash
set -euo pipefail
test -n "${EXPO_PUBLIC_API_URL:?Missing LAN API URL}"
export CI=1 CHECKIN_LAN_PREVIEW=1 EXPO_PUBLIC_LAN_PREVIEW=1
cp /source/package.json /source/package-lock.json /work/
cd /work
lock_hash=$(sha256sum package-lock.json | cut -d' ' -f1)
if [ ! -d node_modules ] || [ ! -f .installed-lock-hash ] || [ "$(cat .installed-lock-hash)" != "$lock_hash" ]; then
  npm ci --no-audit --no-fund
  printf '%s' "$lock_hash" > .installed-lock-hash
fi
cp /source/App.js /source/index.js /source/app.json /source/app.config.js /work/
cp -R /source/plugins /work/
if [ -d /source/assets ]; then cp -R /source/assets /work/; fi
npx expo prebuild --platform android --no-install
mkdir -p /output/signing
if [ ! -f /output/signing/checkin-preview.keystore ]; then
  openssl rand -hex 24 > /output/signing/password
  keytool -genkeypair -v -keystore /output/signing/checkin-preview.keystore -storepass:file /output/signing/password -keypass:file /output/signing/password -alias checkin-preview -keyalg RSA -keysize 2048 -validity 3650 -dname 'CN=CheckInApp LAN Preview' >/dev/null
fi
node /builder/sign-preview.cjs
cd android
./gradlew :app:assembleRelease --no-daemon --build-cache --console=plain -PreactNativeArchitectures=arm64-v8a,armeabi-v7a -Pandroid.enableMinifyInReleaseBuilds=false -Pandroid.enableShrinkResourcesInReleaseBuilds=false -Dorg.gradle.jvmargs='-Xmx2g -XX:MaxMetaspaceSize=768m' --max-workers=2
cp app/build/outputs/apk/release/app-release.apk /output/CheckInApp-LAN.apk
"$ANDROID_HOME/build-tools/37.0.0/apksigner" verify --verbose --print-certs /output/CheckInApp-LAN.apk > /output/apk-verification.txt
sha256sum /output/CheckInApp-LAN.apk > /output/SHA256SUMS
bash /builder/verify-apk.sh
