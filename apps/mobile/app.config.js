const { URL } = require("node:url");

module.exports = ({ config }) => {
  if (process.env.CHECKIN_LAN_PREVIEW !== "1") return config;
  const endpoint = new URL(process.env.EXPO_PUBLIC_API_URL);
  if (!["http:","https:"].includes(endpoint.protocol) || endpoint.pathname !== "/api/v1") {
    throw new Error("Preview requires an HTTP(S) endpoint ending in /api/v1.");
  }
  return {
    ...config,
    name: "CheckInApp LAN",
    android: {
      ...config.android,
      package: "com.checkinapp.mobile.preview",
      versionCode: 2,
      blockedPermissions: ["android.permission.RECORD_AUDIO"],
    },
    plugins: [
      ...config.plugins,
      ["./plugins/withLanNetwork", { host: endpoint.hostname, cleartext: endpoint.protocol === "http:" }],
    ],
  };
};
