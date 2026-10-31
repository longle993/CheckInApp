const {
  withAndroidManifest,
  withDangerousMod,
} = require("expo/config-plugins");
const fs = require("node:fs/promises");
const path = require("node:path");

module.exports = (config, { host, cleartext = true }) => {
  if (!/^[a-zA-Z0-9.-]+$/.test(host))
    throw new Error("Invalid LAN API hostname.");
  config = withAndroidManifest(config, (config) => {
    const app = config.modResults.manifest.application[0].$;
    app["android:networkSecurityConfig"] = "@xml/checkin_lan_network";
    app["android:usesCleartextTraffic"] = "false";
    return config;
  });
  return withDangerousMod(config, [
    "android",
    async (config) => {
      const folder = path.join(
        config.modRequest.platformProjectRoot,
        "app/src/main/res/xml",
      );
      await fs.mkdir(folder, { recursive: true });
      await fs.writeFile(
        path.join(folder, "checkin_lan_network.xml"),
        `<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
  <base-config cleartextTrafficPermitted="false" />
  <domain-config cleartextTrafficPermitted="${cleartext ? "true" : "false"}">
    <domain includeSubdomains="false">${host}</domain>
  </domain-config>
</network-security-config>
`,
      );
      return config;
    },
  ]);
};
