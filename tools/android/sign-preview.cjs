const fs = require("node:fs");
const file = "/work/android/app/build.gradle";
let source = fs.readFileSync(file, "utf8");
if (source.includes("signingConfig signingConfigs.checkinPreview")) {
  process.exit(0);
}
const target = "signingConfigs {";
if (!source.includes(target))
  throw new Error("Android signingConfigs block missing.");
source = source.replace(
  target,
  `${target}
        checkinPreview {
            storeFile file('/output/signing/checkin-preview.keystore')
            storePassword new File('/output/signing/password').text.trim()
            keyAlias 'checkin-preview'
            keyPassword new File('/output/signing/password').text.trim()
        }`,
);
if (!/release\s*\{[\s\S]*?signingConfig signingConfigs\.debug/.test(source))
  throw new Error("Expected Expo release signing configuration missing.");
source = source.replace(
  /(release\s*\{[\s\S]*?signingConfig signingConfigs\.)debug/,
  "$1checkinPreview",
);
fs.writeFileSync(file, source);
