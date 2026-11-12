import DashboardWidgets from "./DashboardWidgets";
import React, { useEffect, useRef, useState } from "react";
import {
  Image,
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
  background: "#faf7f2",
  surface: "#fff",
  text: "#2c2c2c",
  muted: "#707070",
  primary: "#781f36",
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
    [menu, setMenu] = useState(false),
    [screen,setScreen]=useState("home"),[yearId,setYearId]=useState(""),[years,setYears]=useState([]),[preferredClass,setPreferredClass]=useState(""),[calendarRows,setCalendarRows]=useState([]);
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
  useEffect(()=>{if(screen!=="hs"&&screen!=="glv")return;const eligible=sessions.filter(x=>(screen==="glv"?x.catechistAttendanceEnabled:x.studentAttendanceEnabled)&&(x.status==="Open"||x.status==="Scheduled"));const remembered=eligible.filter(x=>x.classRoomId===preferredClass);const candidates=remembered.length?remembered:eligible.filter(x=>x.status==="Open");if(new Set(candidates.map(x=>x.classRoomId)).size===1&&candidates.length)setSession(candidates.find(x=>x.status==="Open")||candidates[0]);},[screen,sessions,preferredClass]);
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
      setRoster([]);setYearId("");setYears([]);setPreferredClass("");setScreen("home");setCalendarRows([]);
    }
  }
  async function loadSessions(selectedYear=yearId) {
    setBusy(true);
    setError("");
    try {
      const day = localToday();
      const yearData=await request("/catalog/years");setYears(yearData.items);
      let pref=null;try{pref=JSON.parse(await SecureStore.getItemAsync("ci-class-"+user.id)||"null");}catch{}
      const wanted=typeof selectedYear==="string"?selectedYear:yearId;
      const remembered=pref?.expires>Date.now()&&yearData.items.some(y=>y.id===pref.year&&y.startDate<=day&&y.endDate>=day)?pref.year:"";
      const valid=yearData.items.filter(y=>y.startDate<=day&&y.endDate>=day);const selected=wanted||(valid.length===1?valid[0].id:"");
      if(!selected){setSessions([]);return;}setYearId(selected);setPreferredClass(pref?.year===selected&&pref.expires>Date.now()?pref.cls:"");
      const data = await request(
        "/attendance-sessions?from=" + day + "&until=" + day+"&yearId="+selected,
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
    if(!retry && ((screen==="glv")!==credential.trim().startsWith("CG1-"))){setError(screen==="glv"?"Cần thẻ giáo lý viên.":"Cần thẻ thiếu nhi.");return;}
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
        "/attendance-sessions/" + session.id + (screen==="glv"?"/catechists/check-ins":"/students/check-ins"),
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
                      BASE + "/web-settings",
                      { signal: controller.signal },
                    );
                    if (!response.ok || typeof (await response.json()).name !== "string") throw new Error();
                    Alert.alert(
                      "Kết nối thành công",
                      "API đã phản hồi thành công.",
                    );
                  } catch {
                    Alert.alert(
                      "Chưa kết nối được",
                      "Kiểm tra kết nối mạng và máy chủ API đang chạy.",
                    );
                  } finally {
                    clearTimeout(timer);
                  }
                }}
              >
                Kiểm tra kết nối API
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
        {screen==="attendance"?<View style={s.card}><Text style={s.title}>Điểm danh</Text><Button onPress={()=>setScreen("hs")}>Điểm danh thiếu nhi</Button><Button onPress={()=>setScreen("glv")}>Điểm danh giáo lý viên</Button></View>:screen==="calendar"?<View style={s.card}><Text style={s.title}>Lịch điểm danh</Text><Text style={s.note}>Năm học: {years.find(y=>y.id===yearId)?.name||"Chưa chọn"}</Text><Button secondary onPress={async()=>{if(!yearId)return;const from=localToday(),until=new Date(Date.now()+7*86400000).toISOString().slice(0,10);try{const data=await request(`/attendance-sessions?from=${from}&until=${until}&yearId=${yearId}`);setCalendarRows(data.items);}catch(e){setError(e.message);}}}>Xem lịch 7 ngày</Button>{calendarRows.map(x=><View style={s.row} key={x.id}><Text style={s.note}>{x.date} · {x.className} · {time(x.opensAtUtc)}–{time(x.closesAtUtc)} · {x.studentAttendanceEnabled?"HS ":""}{x.catechistAttendanceEnabled?"GLV":""}{x.cancelled?" · Hủy":""}</Text></View>)}</View>:screen==="other"?<View style={s.card}><Text style={s.title}>Năm học và tài khoản</Text><Text style={s.note}>CheckInApp 0.2.0 · Android build 3</Text>{years.map(y=><Button key={y.id} secondary onPress={()=>{setSession(null);setPreferredClass("");setYearId(y.id);loadSessions(y.id);}}>{y.name}{yearId===y.id?" ✓":""}</Button>)}<Button secondary onPress={async()=>{await SecureStore.deleteItemAsync("ci-class-"+user.id);setPreferredClass("");Alert.alert("Đã xóa lựa chọn đã nhớ");}}>Xóa lớp đã nhớ</Button><Button onPress={()=>logout()}>Đăng xuất</Button></View>:screen==="home"?<View style={s.card}><Text style={s.title}>Tổng quan</Text><Text style={s.note}>Năm học: {years.find(y=>y.id===yearId)?.name||"Chọn trong Khác"}</Text><Button onPress={()=>{setScreen("hs");setSession(null);}}>Điểm danh thiếu nhi</Button><Button onPress={()=>{setScreen("glv");setSession(null);}}>Điểm danh giáo lý viên</Button><DashboardWidgets request={request} user={user} sessions={sessions} yearName={years.find(y=>y.id===yearId)?.name} navigate={setScreen}/></View>:session ? (
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
              <Text style={s.heading}>Quét thẻ {screen==="glv"?"giáo lý viên":"thiếu nhi"}</Text>
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
            {screen==="hs" && session.studentAttendanceEnabled !== false && <View style={s.card}>
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
                    <Image source={{uri:BASE+"/people/students/"+st.studentId+"/avatar",headers:{Authorization:"Bearer "+token}}} style={{width:40,height:40,borderRadius:20,backgroundColor:T.border}}/><Text style={s.name}>{[st.saintName,st.fullName].filter(Boolean).join(" ")}</Text>
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
            {screen==="glv" && session.catechistAttendanceEnabled !== false && <View style={s.card}><Text style={s.heading}>Giáo lý viên</Text>{teacherRoster.map(t => <View key={t.id} style={s.row}><Image source={{uri:BASE+"/people/catechists/"+t.id+"/avatar",headers:{Authorization:"Bearer "+token}}} style={{width:40,height:40,borderRadius:20,backgroundColor:T.border}}/><View style={{flex:1}}><Text style={s.name}>{[t.saintName,t.fullName].filter(Boolean).join(" ")}</Text><Text style={s.note}>{t.saintName || ""}</Text></View><Text style={s.note}>{t.record ? time(t.record.checkedInAtUtc) : "Chưa đến"}</Text></View>)}{!teacherRoster.length && <Text style={s.note}>Chưa có giáo lý viên được phân công.</Text>}</View>}
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
            {sessions.filter(x=>screen==="glv"?x.catechistAttendanceEnabled:x.studentAttendanceEnabled).sort((a,b)=>Number(b.classRoomId===preferredClass)-Number(a.classRoomId===preferredClass)).map((x) => (
              <View style={s.card} key={x.id}>
                <Text style={s.heading}>{x.className}</Text>
                <Text style={s.muted}>
                  {time(x.opensAtUtc)}–{time(x.closesAtUtc)}
                </Text>
                <Text style={s.name}>
                  {x.present}/{x.expected} đã đến · {x.missing} chưa đến
                </Text>
                <Button secondary onPress={() => {setSession(x);if(preferredClass!==x.classRoomId)Alert.alert("Lưu lớp cho lần sau?","Chỉ lưu lựa chọn trên thiết bị, không chứa dữ liệu học sinh.",[{text:"Chỉ dùng lần này",style:"cancel"},{text:"Lưu lựa chọn",onPress:async()=>{setPreferredClass(x.classRoomId);await SecureStore.setItemAsync("ci-class-"+user.id,JSON.stringify({year:yearId,cls:x.classRoomId,expires:Date.now()+2592000000}));}}]);}}>
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
      <View style={{flexDirection:"row",backgroundColor:T.surface,borderTopWidth:1,borderColor:T.border,paddingVertical:12}}>{[["home","Tổng quan"],["attendance","Điểm danh"],["calendar","Lịch"],["other","Khác"]].map(([id,label])=><Pressable key={id} accessibilityRole="tab" accessibilityState={{selected:screen===id}} style={{flex:1,alignItems:"center",padding:8}} onPress={()=>{setScreen(id);setSession(null);setCamera(false);setResult(null);setError("");}}><Svg width={20} height={20} viewBox="0 0 24 24"><Path fill="none" stroke={screen===id?T.primary:T.muted} strokeWidth={1.7} d={id==="home"?"M3 10 12 3l9 7v11h-6v-7H9v7H3Z":id==="calendar"?"M4 5h16v16H4ZM4 10h16M8 2v6M16 2v6":id==="other"?"M4 6h16M4 12h16M4 18h16":id==="glv"?"M12 3a4 4 0 1 0 0 8 4 4 0 0 0 0-8M4 22v-3a8 8 0 0 1 16 0v3":"M3 8V3h5M16 3h5v5M21 16v5h-5M8 21H3v-5M7 12l3 3 7-7"}/></Svg><Text style={{color:screen===id?T.primary:T.muted,fontFamily:"Inter_600SemiBold",fontSize:12}}>{label}</Text></Pressable>)}</View>
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
