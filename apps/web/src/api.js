let accessToken = null,
  refreshing = null;
export function clearToken() {
  accessToken = null;
}
export function setToken(value) {
  accessToken = value;
}
export async function api(path, options = {}, retry = true) {
  const { body, ...rest } = options;
  const response = await fetch("/api/v1" + path, {
    ...rest,
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
      "X-Requested-With": "CheckInApp",
      ...(accessToken ? { Authorization: "Bearer " + accessToken } : {}),
      ...rest.headers,
    },
    ...(body !== undefined ? { body: JSON.stringify(body) } : {}),
  });
  if (response.status === 401 && retry && !path.startsWith("/auth/")) {
    try {
      refreshing ??= api(
        "/auth/refresh",
        { method: "POST", body: { client: "web" } },
        false,
      )
        .then((d) => {
          accessToken = d.accessToken;
          return d;
        })
        .finally(() => (refreshing = null));
      await refreshing;
      return api(path, options, false);
    } catch {
      clearToken();
      window.dispatchEvent(new Event("session-expired"));
    }
  }
  if (!response.ok) {
    let data;
    try {
      data = await response.json();
    } catch {
      data = {};
    }
    throw Object.assign(
      new Error(
        data.title ||
          data.message ||
          (response.status === 403
            ? "Bạn không có quyền truy cập."
            : response.status === 429
              ? "Thao tác quá nhanh. Vui lòng thử lại sau."
              : "Không xử lý được yêu cầu."),
      ),
      { code: data.code, status: response.status },
    );
  }
  return response.status === 204 ? null : options.blob ? response.blob() : response.json();
}
export const today = () =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
export const fmtDate = (s) =>
  s ? String(s).slice(0, 10).split("-").reverse().join("/") : "—";
export const fmtTime = (s) =>
  s
    ? new Intl.DateTimeFormat("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
        hour: "2-digit",
        minute: "2-digit",
      }).format(new Date(s.endsWith("Z") ? s : s + "Z"))
    : "—";
export const can = (user, bit) =>
  !!user && (BigInt(user.permissions) & (1n << BigInt(bit))) !== 0n;

export const displayPerson = p => p ? [p.saintName,p.fullName||p.name].filter(Boolean).join(" ").replace(/\s+/g," ").trim() : "";
