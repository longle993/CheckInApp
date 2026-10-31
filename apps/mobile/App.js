import React, { useEffect, useRef, useState } from "react";
import {
  ActivityIndicator,
  Alert,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
  Platform,
  Modal,
} from "react-native";
import { CameraView, useCameraPermissions } from "expo-camera";
import * as SecureStore from "expo-secure-store";
import * as Crypto from "expo-crypto";
import { StatusBar } from "expo-status-bar";
import Svg, { Rect, Path, G } from "react-native-svg";
import { SafeAreaView } from "react-native-safe-area-context";
import {
  useFonts,
  Inter_400Regular,
  Inter_500Medium,
  Inter_600SemiBold,
} from "@expo-google-fonts/inter";

const BASE =
  process.env.EXPO_PUBLIC_API_URL ||
  (Platform.OS === "android"
    ? "http://10.0.2.2:5080/api/v1"
    : "http://localhost:5080/api/v1");
const LAN_PREVIEW = process.env.EXPO_PUBLIC_LAN_PREVIEW === "1";
const T = {
  background: "#f4f4f6",
  surface: "#fff",
  text: "#2c2c2c",
  muted: "#707070",
  primary: "#4f46e5",
  border: "#eaeaea",
  success: "#007a55",
  successBg: "#ecfdf5",
  error: "#b91c1c",
  errorBg: "#fef2f2",
};
const localToday = () =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
const time = (s) =>
  new Date(s.endsWith("Z") ? s : s + "Z").toLocaleTimeString("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    hour: "2-digit",
    minute: "2-digit",
  });
function Brand() {
  return (
    <View style={{ flexDirection: "row", alignItems: "center", gap: 10 }}>
      <Svg width={32} height={32} viewBox="0 0 32 32">
        <Rect width={32} height={32} rx={8} fill={T.primary} />
        <G
          fill="none"
          stroke="white"
          strokeWidth={2}
          strokeLinecap="round"
          strokeLinejoin="round"
        >
          <Rect x={7} y={7} width={7} height={7} rx={1} />
          <Path d="M20 7h5v5M7 20v5h5m7-4 3 3 5-7" />
        </G>
      </Svg>
      <Text style={s.brand}>CheckInApp</Text>
    </View>
  );
}
function Button({ children, onPress, disabled, secondary = false }) {
  return (
    <Pressable
      accessibilityRole="button"
      disabled={disabled}
      onPress={onPress}
      style={[s.button, secondary && s.secondary, disabled && { opacity: 0.5 }]}
    >
      <Text style={[s.buttonText, secondary && { color: T.text }]}>
        {children}
      </Text>
    </Pressable>
  );
}
export default function App() {
  const [fontsLoaded, fontError] = useFonts({
    Inter_400Regular,
    Inter_500Medium,
    Inter_600SemiBold,
  });
  const [user, setUser] = useState(null),
    [token, setToken] = useState(null),
    [ready, setReady] = useState(false),
    [busy, setBusy] = useState(false),
    [email, setEmail] = useState(""),
    [password, setPassword] = useState(""),
    [name, setName] = useState(""),
    [register, setRegister] = useState(false),
    [error, setError] = useState(""),
    [sessions, setSessions] = useState([]),
    [session, setSession] = useState(null),
    [roster, setRoster] = useState([]),
    [teacherRoster, setTeacherRoster] = useState([]),
    [camera, setCamera] = useState(false),
    [permission, requestPermission] = useCameraPermissions(),
    [result, setResult] = useState(null),
    [code, setCode] = useState(""),
    [tab, setTab] = useState("recent"),
    [menu, setMenu] = useState(false);
  const tokenRef = useRef(null),
    refreshRef = useRef(null),
    gate = useRef(false),
    pending = useRef(null);
  function accept(data) {
    tokenRef.current = data.accessToken;
    setToken(data.accessToken);
    setUser(data.user);
  }
  async function raw(path, body, access, method) {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 12000);
    let response;
    try {
      response = await fetch(BASE + path, {
        signal: controller.signal,
        method: method || (body ? "POST" : "GET"),
        headers: {
          "Content-Type": "application/json",
          ...(access ? { Authorization: "Bearer " + access } : {}),
        },
        ...(body ? { body: JSON.stringify(body) } : {}),
      });
    } catch {
      throw new Error(
        "Không kết nối được API. Kiểm tra Wi-Fi và máy chủ rồi thử lại.",
      );
    } finally {
      clearTimeout(timeout);
    }
    let data = null;
    try {
      data = await response.json();
    } catch {}
    if (!response.ok)
      throw Object.assign(
        new Error(
          data?.title ||
            data?.message ||
            (response.status === 403
              ? "Bạn không có quyền thực hiện thao tác."
              : "Không kết nối được máy chủ."),
        ),
        { status: response.status },
      );
    return data;
  }
  async function renew() {
    if (!refreshRef.current)
      refreshRef.current = (async () => {
        const refreshToken = await SecureStore.getItemAsync("ci-refresh");
        if (!refreshToken) throw new Error("Vui lòng đăng nhập.");
        const data = await raw("/auth/refresh", {
          client: "mobile",
          refreshToken,
        });
        await SecureStore.setItemAsync("ci-refresh", data.refreshToken);
        accept(data);
        return data.accessToken;
      })().finally(() => (refreshRef.current = null));
    return refreshRef.current;
  }
  async function request(path, body, method) {
    try {
      return await raw(path, body, tokenRef.current, method);
    } catch (e) {
      if (e.status === 401) {
        try {
          let fresh = await renew();
          return await raw(path, body, fresh, method);
        } catch {
          await logout(false);
          throw new Error("Phiên đã hết hạn. Đăng nhập lại.");
        }
      }
      throw e;
    }
  }
  useEffect(() => {
    renew()
      .catch(() => {})
      .finally(() => setReady(true));
  }, []);
  useEffect(() => {
    if (user) loadSessions();
  }, [user]);
  useEffect(() => {
    setCamera(false);
    setResult(null);
    setError("");
    pending.current = null;
    gate.current = false;
    if (session) loadRoster(session.id);
  }, [session?.id]);
  async function login() {
    setBusy(true);
    setError("");
    try {
      if (register) {
        await raw("/auth/register", { email, password, fullName: name });
        setRegister(false);
        Alert.alert("Đã gửi đăng ký", "Vui lòng chờ admin duyệt tài khoản.");
      } else {
        const data = await raw("/auth/login", {
          email,
          password,
          client: "mobile",
        });
        await SecureStore.setItemAsync("ci-refresh", data.refreshToken);
        accept(data);
        setPassword("");
      }
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  async function logout(remote = true) {
    setCamera(false);
    try {
      if (remote) await raw("/auth/logout", {}, tokenRef.current);
    } catch {
    } finally {
      await SecureStore.deleteItemAsync("ci-refresh");
      tokenRef.current = null;
      setToken(null);
      setUser(null);
      setSession(null);
      setSessions([]);
      setRoster([]);
    }
  }
  async function loadSessions() {
    setBusy(true);
    setError("");
    try {
      const day = localToday();
      const data = await request(
        "/attendance-sessions?from=" + day + "&until=" + day,
      );
      setSessions(data.items.filter((x) => !x.cancelled));
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  }
  async function loadRoster(id) {
    try {
      let data = await request("/attendance-sessions/" + id + "/roster");
      setRoster(data.items);
      const teachers = await request("/attendance-sessions/" + id + "/catechists");
      setTeacherRoster(teachers.items);
    } catch (e) {
      setError(e.message);
    }
  }
  async function scan(credential, type = "qr", retry = false) {
    if (gate.current && !retry) return;
    gate.current = true;
    setCamera(false);
    setBusy(true);
    setError("");
    setResult(null);
    pending.current = retry
      ? pending.current
      : {
          credential: credential.trim(),
          clientRequestId: Crypto.randomUUID(),
          method: type === "qr" ? "QR" : "Barcode",
        };
    try {
      const data = await request(
        "/attendance-sessions/" + session.id + "/check-ins",
        pending.current,
      );
      setResult(data);
      pending.current = null;
      await loadRoster(session.id);
    } catch (e) {
      setError((e.status ? "" : "Chưa xác nhận: ") + e.message);
      if (e.status) pending.current = null;
    } finally {
      setBusy(false);
    }
  }
  async function startScan() {
    setError("");
    if (!permission?.granted) {
      const granted = await requestPermission();
      if (!granted.granted) {
        setError(
          "Chưa có quyền camera. Cho phép trong Cài đặt hoặc nhập mã thẻ.",
        );
        return;
      }
    }
    gate.current = false;
    setResult(null);
    setCamera(true);
  }
  if (!ready || (!fontsLoaded && !fontError))
    return (
      <SafeAreaView style={s.center}>
        <ActivityIndicator color={T.primary} />
        <Text style={s.muted}>Đang kiểm tra phiên đăng nhập…</Text>
      </SafeAreaView>
    );
  if (!user)
    return (
      <SafeAreaView style={s.safe}>
        <StatusBar style="dark" />
        <ScrollView
          contentContainerStyle={s.login}
          keyboardShouldPersistTaps="handled"
        >
          <Brand />
          <Text style={s.title}>
            {register ? "Đăng ký tài khoản" : "Đăng nhập"}
          </Text>
          <Text style={s.muted}>
            Tài khoản cần được admin duyệt trước khi sử dụng.
          </Text>
          {register && (
            <>
              <Text style={s.label}>Họ và tên</Text>
              <TextInput
                style={s.input}
                value={name}
                onChangeText={setName}
                autoFocus
                accessibilityLabel="Họ và tên"
              />
            </>
          )}
          <Text style={s.label}>Email</Text>
          <TextInput
            style={s.input}
            value={email}
            onChangeText={setEmail}
            autoCapitalize="none"
            keyboardType="email-address"
            autoComplete="username"
            accessibilityLabel="Email"
          />
          <Text style={s.label}>Mật khẩu</Text>
          <TextInput
            style={s.input}
            value={password}
            onChangeText={setPassword}
            secureTextEntry
            autoComplete={register ? "new-password" : "current-password"}
            accessibilityLabel="Mật khẩu"
          />
          {!!error && (
            <Text style={s.error} accessibilityLiveRegion="polite">
              {error}
            </Text>
          )}
          <Button
            disabled={busy || !email || !password || (register && !name)}
            onPress={login}
          >
            {busy ? "Đang xử lý…" : register ? "Gửi đăng ký" : "Đăng nhập"}
          </Button>
          <Button
            secondary
            onPress={() => {
              setRegister(!register);
              setError("");
            }}
          >
            {register ? "Đã có tài khoản" : "Chưa có tài khoản? Đăng ký"}
          </Button>
          <Text style={s.note}>
            Mật khẩu đăng ký cần từ 12 ký tự, có chữ và số.
          </Text>
          {LAN_PREVIEW && (
            <>
              <Text style={s.note}>API cài thử: {BASE}</Text>
              <Button
                secondary
                onPress={async () => {
                  const controller = new AbortController();
                  const timer = setTimeout(() => controller.abort(), 10000);
                  try {
                    const response = await fetch(
                      BASE.replace(/\/api\/v1\/?$/, "/health"),
                      { signal: controller.signal },
                    );
                    if (!response.ok) throw new Error();
                    Alert.alert(
                      "Kết nối thành công",
                      "Điện thoại đã truy cập được API qua Wi-Fi.",
                    );
                  } catch {
                    Alert.alert(
                      "Chưa kết nối được",
                      "Kiểm tra hai thiết bị cùng Wi-Fi, máy chủ đang chạy và firewall cho phép cổng 5082.",
                    );
                  } finally {
                    clearTimeout(timer);
                  }
                }}
              >
                Kiểm tra kết nối LAN
              </Button>
            </>
          )}
        </ScrollView>
      </SafeAreaView>
    );
  const missing = roster.filter((x) => !x.present),
    recent = roster
      .filter((x) => x.present)
      .sort((a, b) =>
        String(b.recordedAtUtc).localeCompare(String(a.recordedAtUtc)),
      );
  const checkInAllowed = (BigInt(user.permissions) & (1n << 11n)) !== 0n;
  return (
    <SafeAreaView style={s.safe}>
      <StatusBar style="dark" />
      <View style={s.header}>
        <Pressable onPress={() => setMenu(true)} accessibilityLabel="Mở menu">
          <Text style={s.menu}>☰</Text>
        </Pressable>
        <Brand />
        <View style={{ flex: 1 }} />
        <Pressable onPress={() => logout()} accessibilityLabel="Đăng xuất">
          <Text style={s.muted}>Thoát</Text>
        </Pressable>
      </View>
      <ScrollView
        contentContainerStyle={s.content}
        keyboardShouldPersistTaps="handled"
        refreshControl={undefined}
      >
        {session ? (
          <>
            <Pressable
              onPress={() => {
                setSession(null);
                setCamera(false);
                loadSessions();
              }}
            >
              <Text style={s.link}>‹ Buổi hôm nay</Text>
            </Pressable>
            <Text style={s.title}>Điểm danh {session.className}</Text>
            <Text style={s.muted}>
              {session.date.split("-").reverse().join("/")} ·{" "}
              {time(session.opensAtUtc)}–{time(session.closesAtUtc)}
            </Text>
            <Text style={s.pill}>
              {session.status === "Open"
                ? "Đang mở"
                : session.status === "Closed"
                  ? "Đã kết thúc"
                  : "Chưa đến giờ"}
            </Text>
            <View style={s.card}>
              <Text style={s.heading}>Quét thẻ HS / GLV</Text>
              <Text style={s.note}>QR / Code 128</Text>
              <View style={s.camera}>
                {camera ? (
                  <CameraView
                    style={StyleSheet.absoluteFill}
                    facing="back"
                    barcodeScannerSettings={{ barcodeTypes: ["qr", "code128"] }}
                    onBarcodeScanned={
                      busy ? undefined : ({ data, type }) => scan(data, type)
                    }
                  />
                ) : (
                  <View style={s.aim}>
                    <Text style={{ fontSize: 64, color: T.muted }}>⌗</Text>
                  </View>
                )}
              </View>
              {busy && <ActivityIndicator color={T.primary} />}
              <Button
                onPress={startScan}
                disabled={busy || session.status !== "Open" || !checkInAllowed}
              >
                {result || error ? "Quét tiếp" : "Bật camera"}
              </Button>
              {camera && (
                <Button secondary onPress={() => setCamera(false)}>
                  Dừng camera
                </Button>
              )}
              {!!result && (
                <View style={s.success} accessibilityLiveRegion="polite">
                  <Text style={s.successTitle}>
                    {result.resultCode === "Created"
                      ? "✓ Đã điểm danh"
                      : "Đã có kết quả điểm danh"}
                  </Text>
                  <Text style={s.successText}>
                    {result.personType === "Catechist" ? "GLV · " : "HS · "}{result.studentName} · {result.studentCode}
                  </Text>
                  <Text style={s.successText}>
                    {result.present ? "Có mặt" : "Kết quả hiện tại: vắng"} ·{" "}
                    {time(result.recordedAtUtc)}
                  </Text>
                </View>
              )}
              {!!error && (
                <Text style={s.error} accessibilityLiveRegion="polite">
                  {error}
                </Text>
              )}
              {!!pending.current && !busy && (
                <Button secondary onPress={() => scan("", undefined, true)}>
                  Thử lại yêu cầu chưa lưu
                </Button>
              )}
              <Text style={s.label}>Nhập mã thẻ thay thế</Text>
              <TextInput
                style={s.input}
                value={code}
                onChangeText={setCode}
                autoCapitalize="none"
                accessibilityLabel="Mã thẻ"
              />
              <Button
                secondary
                disabled={
                  !code.trim() ||
                  busy ||
                  session.status !== "Open" ||
                  !checkInAllowed
                }
                onPress={() => {
                  gate.current = false;
                  scan(code, "code128");
                  setCode("");
                }}
              >
                Kiểm tra
              </Button>
              <Text style={s.note}>
                Chỉ tính có mặt sau khi server xác nhận. Mất mạng không lưu cục
                bộ.
              </Text>
            </View>
            {session.studentAttendanceEnabled !== false && <View style={s.card}>
              <View style={s.metrics}>
                {[
                  [roster.length - missing.length, "Đã đến"],
                  [missing.length, "Chưa đến"],
                  [roster.length, "Sĩ số"],
                ].map(([value, label]) => (
                  <View key={label}>
                    <Text style={s.metric}>{value}</Text>
                    <Text style={s.note}>{label}</Text>
                  </View>
                ))}
              </View>
              <View style={s.tabs}>
                <Pressable onPress={() => setTab("recent")}>
                  <Text style={tab === "recent" ? s.activeTab : s.muted}>
                    Vừa điểm danh
                  </Text>
                </Pressable>
                <Pressable onPress={() => setTab("missing")}>
                  <Text style={tab === "missing" ? s.activeTab : s.muted}>
                    Chưa đến ({missing.length})
                  </Text>
                </Pressable>
              </View>
              {(tab === "recent" ? recent : missing).map((st) => (
                <View key={st.studentId} style={s.row}>
                  <View style={{ flex: 1 }}>
                    <Text style={s.name}>{st.fullName}</Text>
                    <Text style={s.note}>
                      {st.code} · {st.saintName || "Chưa có Tên Thánh"}
                    </Text>
                  </View>
                  <Text style={s.note}>
                    {st.present ? time(st.recordedAtUtc) : "Chưa đến"}
                  </Text>
                </View>
              ))}
              {!(tab === "recent" ? recent : missing).length && (
                <Text style={s.muted}>
                  Chưa có học sinh trong danh sách này.
                </Text>
              )}
              <Text style={s.note}>
                Buổi đang mở: chưa đến chưa kết luận là vắng.
              </Text>
            </View>}
            {session.catechistAttendanceEnabled !== false && <View style={s.card}><Text style={s.heading}>Giáo lý viên</Text>{teacherRoster.map(t => <View key={t.id} style={s.row}><View style={{flex:1}}><Text style={s.name}>{t.fullName}</Text><Text style={s.note}>{t.saintName || ""}</Text></View><Text style={s.note}>{t.record ? time(t.record.checkedInAtUtc) : "Chưa đến"}</Text></View>)}{!teacherRoster.length && <Text style={s.note}>Chưa có giáo lý viên được phân công.</Text>}</View>}
          </>
        ) : (
          <>
            <Text style={s.title}>Điểm danh hôm nay</Text>
            <Text style={s.muted}>
              {localToday().split("-").reverse().join("/")} · Chỉ các buổi có
              lịch
            </Text>
            <View style={[s.card, s.metrics]}>
              <View>
                <Text style={s.metric}>{sessions.length}</Text>
                <Text style={s.note}>Lớp có lịch</Text>
              </View>
              <View>
                <Text style={s.metric}>
                  {sessions.reduce((a, x) => a + x.present, 0)}
                </Text>
                <Text style={s.note}>Đã đến</Text>
              </View>
              <View>
                <Text style={s.metric}>
                  {sessions.reduce((a, x) => a + x.missing, 0)}
                </Text>
                <Text style={s.note}>Chưa đến</Text>
              </View>
            </View>
            {busy && <ActivityIndicator color={T.primary} />}{" "}
            {!!error && <Text style={s.error}>{error}</Text>}
            {!busy && !sessions.length && !error && (
              <Text style={s.muted}>Hôm nay không có lịch điểm danh.</Text>
            )}
            {sessions.map((x) => (
              <View style={s.card} key={x.id}>
                <Text style={s.heading}>{x.className}</Text>
                <Text style={s.muted}>
                  {time(x.opensAtUtc)}–{time(x.closesAtUtc)}
                </Text>
                <Text style={s.name}>
                  {x.present}/{x.expected} đã đến · {x.missing} chưa đến
                </Text>
                <Button secondary onPress={() => setSession(x)}>
                  Mở buổi
                </Button>
              </View>
            ))}
            <Button secondary onPress={loadSessions}>
              Tải lại
            </Button>
            <Text style={s.note}>
              Buổi hủy, ngày nghỉ và buổi tương lai không cộng vào số vắng.
            </Text>
          </>
        )}
      </ScrollView>
      <Modal
        visible={menu}
        transparent
        animationType="slide"
        onRequestClose={() => setMenu(false)}
      >
        <Pressable style={s.overlay} onPress={() => setMenu(false)}>
          <Pressable style={s.drawer}>
            <Text style={s.brand}>CheckInApp</Text>
            <Text style={s.name}>{user.fullName}</Text>
            <Button
              secondary
              onPress={() => {
                setSession(null);
                setCamera(false);
                setMenu(false);
                loadSessions();
              }}
            >
              Buổi hôm nay
            </Button>
            <Button
              secondary
              onPress={() => {
                setMenu(false);
                logout();
              }}
            >
              Đăng xuất
            </Button>
          </Pressable>
        </Pressable>
      </Modal>
    </SafeAreaView>
  );
}
const s = StyleSheet.create({
  safe: { flex: 1, backgroundColor: T.background },
  center: { flex: 1, alignItems: "center", justifyContent: "center", gap: 16 },
  login: {
    padding: 24,
    gap: 16,
    flexGrow: 1,
    justifyContent: "center",
    maxWidth: 500,
    width: "100%",
    alignSelf: "center",
  },
  header: {
    padding: 16,
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    backgroundColor: T.surface,
  },
  brand: {
    fontSize: 18,
    fontFamily: "Inter_600SemiBold",
    fontWeight: "600",
    color: T.text,
  },
  menu: { fontSize: 24, color: T.muted },
  content: { padding: 16, gap: 16, paddingBottom: 40 },
  title: {
    fontSize: 20,
    fontFamily: "Inter_600SemiBold",
    fontWeight: "600",
    color: T.text,
  },
  heading: {
    fontSize: 16,
    fontFamily: "Inter_600SemiBold",
    fontWeight: "600",
    color: T.text,
  },
  name: {
    fontSize: 14,
    fontFamily: "Inter_500Medium",
    fontWeight: "500",
    color: T.text,
  },
  muted: {
    fontSize: 14,
    fontFamily: "Inter_400Regular",
    color: T.muted,
    lineHeight: 21,
  },
  note: {
    fontSize: 12,
    fontFamily: "Inter_400Regular",
    color: T.muted,
    lineHeight: 18,
  },
  label: {
    fontSize: 14,
    fontFamily: "Inter_500Medium",
    fontWeight: "500",
    color: T.text,
  },
  input: {
    height: 48,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: T.border,
    paddingHorizontal: 14,
    fontSize: 16,
    backgroundColor: T.surface,
    color: T.text,
  },
  button: {
    minHeight: 44,
    padding: 12,
    borderRadius: 12,
    alignItems: "center",
    justifyContent: "center",
    backgroundColor: T.primary,
  },
  secondary: {
    backgroundColor: T.surface,
    borderWidth: 1,
    borderColor: T.border,
  },
  buttonText: {
    fontSize: 14,
    fontFamily: "Inter_500Medium",
    fontWeight: "500",
    color: "#fff",
  },
  card: { borderRadius: 16, backgroundColor: T.surface, padding: 16, gap: 16 },
  metrics: { flexDirection: "row", justifyContent: "space-between", gap: 12 },
  metric: {
    fontSize: 24,
    fontFamily: "Inter_600SemiBold",
    fontWeight: "600",
    color: T.text,
    fontVariant: ["tabular-nums"],
  },
  camera: {
    height: 270,
    borderRadius: 12,
    backgroundColor: T.background,
    alignItems: "center",
    justifyContent: "center",
    overflow: "hidden",
  },
  aim: {
    width: 172,
    height: 172,
    borderWidth: 2,
    borderColor: T.muted,
    borderRadius: 16,
    alignItems: "center",
    justifyContent: "center",
  },
  success: {
    backgroundColor: T.successBg,
    padding: 16,
    gap: 6,
    borderRadius: 12,
  },
  successTitle: {
    color: T.success,
    fontFamily: "Inter_600SemiBold",
    fontWeight: "600",
    fontSize: 14,
  },
  successText: { color: T.success, fontSize: 12 },
  error: { color: T.error, fontSize: 14, lineHeight: 21 },
  link: { color: T.primary, fontSize: 14 },
  pill: {
    alignSelf: "flex-start",
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 6,
    backgroundColor: T.successBg,
    color: T.success,
    fontSize: 12,
  },
  tabs: {
    flexDirection: "row",
    gap: 24,
    borderBottomWidth: 1,
    borderColor: T.border,
    paddingBottom: 12,
  },
  activeTab: {
    color: T.primary,
    fontFamily: "Inter_600SemiBold",
    fontWeight: "600",
  },
  row: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingVertical: 12,
    borderBottomWidth: 1,
    borderColor: "#f7f7f8",
  },
  overlay: { flex: 1, backgroundColor: "#0006" },
  drawer: {
    width: 280,
    height: "100%",
    backgroundColor: T.surface,
    padding: 24,
    gap: 24,
  },
});
