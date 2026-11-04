using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VaroGO.Client.EditorTools.DataSync
{
    // 구글 시트 → ScriptableObject 값 동기화.
    // - Play 버튼을 누르면 자동 실행 (Tools/Data/Auto Sync On Play 로 끄고 켬, PC별 설정)
    // - Tools/Data/Sync Now 로 수동 실행
    // 기존 에셋의 값만 SerializedObject로 덮어쓴다 (에셋 교체 없음 → 프리팹 참조 유지).
    // 실패해도 Play는 막지 않는다: 이전 값 그대로 쓰고 콘솔에 경고.
    [InitializeOnLoad]
    static class SheetSync
    {
        const string Tag = "[Sheet]";
        const string AutoPref = "VaroGO.SheetSync.AutoOnPlay";
        const string MenuAuto = "Tools/Data/Auto Sync On Play";

        static bool AutoOnPlay
        {
            get => EditorPrefs.GetBool(AutoPref, true);
            set => EditorPrefs.SetBool(AutoPref, value);
        }

        static SheetSync()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            Editor.finishedDefaultHeaderGUI += DrawSyncedHeader;
        }

        static void OnPlayMode(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredPlayMode) { FlushDeferred(); return; }
            if (s != PlayModeStateChange.ExitingEditMode) return;
            if (!AutoOnPlay || Application.isBatchMode) return;

            // Console의 'Clear on Play'가 켜져 있으면 Play 직전 로그가 지워진다
            // → 로그를 모아 두었다가 Play가 시작된 뒤(도메인 리로드 이후) 다시 찍는다
            deferring = true;
            try { SyncAll(fromPlay: true); }
            finally
            {
                deferring = false;
                SessionState.SetString(DeferKey, string.Join(Sep, deferred));
                deferred.Clear();
            }
        }

        // ---------- 로그 (Play 진입 시 지연 출력) ----------

        const string DeferKey = "VaroGO.SheetSync.DeferredLogs";
        const string Sep = "\u001e";
        static bool deferring;
        static readonly List<string> deferred = new List<string>();

        static void LogI(string m, UnityEngine.Object ctx = null) => Emit('I', m, ctx);
        static void LogW(string m, UnityEngine.Object ctx = null) => Emit('W', m, ctx);
        static void LogE(string m, UnityEngine.Object ctx = null) => Emit('E', m, ctx);

        static void Emit(char type, string m, UnityEngine.Object ctx)
        {
            if (deferring) { deferred.Add(type + m); return; }
            if (type == 'E') Debug.LogError(m, ctx);
            else if (type == 'W') Debug.LogWarning(m, ctx);
            else Debug.Log(m, ctx);
        }

        static void FlushDeferred()
        {
            var all = SessionState.GetString(DeferKey, "");
            SessionState.EraseString(DeferKey);
            if (all.Length == 0) return;
            foreach (var e in all.Split(new[] { Sep }, StringSplitOptions.RemoveEmptyEntries))
                Emit(e[0], e.Substring(1), null);
        }

        [MenuItem("Tools/Data/Sync Now", priority = 1)]
        static void SyncNowMenu() => SyncAll(fromPlay: false);

        [MenuItem(MenuAuto, priority = 2)]
        static void ToggleAuto() => AutoOnPlay = !AutoOnPlay;

        [MenuItem(MenuAuto, true)]
        static bool ToggleAutoValidate() { Menu.SetChecked(MenuAuto, AutoOnPlay); return true; }

        [MenuItem("Tools/Data/Open Sheet", priority = 20)]
        static void OpenSheet()
        {
            var s = FindSettings();
            if (s != null && s.tables.Count > 0) Application.OpenURL(s.EditUrl(s.tables[0]));
        }

        [MenuItem("Tools/Data/Select Settings", priority = 21)]
        static void SelectSettings()
        {
            var s = FindSettings();
            if (s != null) { Selection.activeObject = s; EditorGUIUtility.PingObject(s); }
        }

        // ---------- 동기화 ----------

        public static void SyncAll(bool fromPlay)
        {
            var settings = FindSettings();
            if (settings == null)
            {
                if (!fromPlay) LogW($"{Tag} SheetSyncSettings 에셋이 없습니다. Create > VaroGO/Data/Sheet Sync Settings 로 만드세요.");
                return;
            }
            var tables = settings.tables.Where(t => t != null && t.enabled && !string.IsNullOrWhiteSpace(settings.IdOf(t))).ToList();
            if (tables.Count == 0) return;

            var sw = Stopwatch.StartNew();
            var texts = Download(settings, tables, out var errors);

            int changedTotal = 0, errorTotal = 0;
            var lines = new List<string>();
            for (int i = 0; i < tables.Count; i++)
            {
                var t = tables[i];
                if (texts[i] == null)
                {
                    errorTotal++;
                    lines.Add($"  {t.name}: 받기 실패 → 이전 값 사용 ({errors[i]})");
                    continue;
                }
                var r = t.target != null ? ApplyKeyValue(t, texts[i]) : ApplyPrefabRows(t, texts[i]);
                changedTotal += r.changed;
                errorTotal += r.errors;
                lines.Add($"  {t.name}: {r.changed}개 변경, {r.errors}개 오류");
            }
            if (changedTotal > 0) AssetDatabase.SaveAssets();

            string msg = $"{Tag} 동기화 {sw.Elapsed.TotalSeconds:0.0}초 — {changedTotal}개 변경, {errorTotal}개 오류\n{string.Join("\n", lines)}";
            if (errorTotal > 0) LogW(msg, settings);
            else LogI(msg, settings);
        }

        static SheetSyncSettings FindSettings()
        {
            var guid = AssetDatabase.FindAssets("t:" + nameof(SheetSyncSettings)).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<SheetSyncSettings>(AssetDatabase.GUIDToAssetPath(guid));
        }

        // 탭들을 동시에 받는다. 메인 스레드를 timeout 동안만 막는다.
        static string[] Download(SheetSyncSettings s, List<SheetSyncSettings.Table> tables, out string[] errors)
        {
            var result = new string[tables.Count];
            var errs = new string[tables.Count];
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(s.timeoutSeconds) })
            {
                var tasks = tables.Select((t, i) => Task.Run(async () =>
                {
                    try
                    {
                        using (var res = await http.GetAsync(s.CsvUrl(t)))
                        {
                            var body = await res.Content.ReadAsStringAsync();
                            var type = res.Content.Headers.ContentType?.MediaType ?? "";
                            int code = (int)res.StatusCode;
                            if (code == 400 || code == 401 || code == 403)
                                errs[i] = $"HTTP {code} — 시트가 '링크가 있는 모든 사용자: 보기'로 공유됐는지, gid({t.gid})가 맞는지 확인";
                            else if (!res.IsSuccessStatusCode) errs[i] = $"HTTP {code}";
                            else if (type.Contains("html") || body.TrimStart().StartsWith("<"))
                                errs[i] = "로그인 페이지가 옴 — 시트를 '링크가 있는 모든 사용자: 보기'로 공유하세요";
                            else result[i] = body;
                        }
                    }
                    catch (TaskCanceledException) { errs[i] = $"{s.timeoutSeconds:0.#}초 시간 초과"; }
                    catch (Exception e) { errs[i] = e.GetBaseException().Message; }
                })).ToArray();

                try { Task.WaitAll(tasks, TimeSpan.FromSeconds(s.timeoutSeconds + 1)); }
                catch (AggregateException) { }
            }
            for (int i = 0; i < errs.Length; i++)
                if (result[i] == null && errs[i] == null) errs[i] = "시간 초과";
            errors = errs;
            return result;
        }

        // ---------- 세로형 (key / value) ----------

        static (int changed, int errors) ApplyKeyValue(SheetSyncSettings.Table t, string csv)
        {
            var rows = CsvReader.Parse(csv);
            int header = rows.FindIndex(r => IndexOf(r, "key") >= 0 && IndexOf(r, "value") >= 0);
            if (header < 0)
            {
                LogE($"{Tag} {t.name}: 'key'와 'value' 열이 있는 헤더 행을 못 찾았습니다.", t.target);
                return (0, 1);
            }
            int kCol = IndexOf(rows[header], "key"), vCol = IndexOf(rows[header], "value");

            var so = new SerializedObject(t.target);
            var type = t.target.GetType();
            var seen = new HashSet<string>();
            var changes = new List<string>();
            int errors = 0;

            for (int r = header + 1; r < rows.Count; r++)
            {
                var row = rows[r];
                string key = Cell(row, kCol);
                if (key.Length == 0 || key.StartsWith("#")) continue; // 빈 줄·주석
                string raw = Cell(row, vCol);
                string where = $"{t.name} {r + 1}행 '{key}'";

                if (!seen.Add(key)) { Warn(ref errors, t, $"{where}: key 중복 — 위쪽 행 값만 씀"); continue; }

                var prop = so.FindProperty(key);
                if (prop == null) { Warn(ref errors, t, $"{where}: 코드에 없는 key (오타?)"); continue; }

                if (!TrySet(prop, raw, FieldOf(type, key), out string before, out string err))
                {
                    Warn(ref errors, t, $"{where}: {err} (값 '{raw}') → 이전 값 {before} 유지");
                    continue;
                }
                if (before != null) changes.Add($"{key}: {before} → {Show(prop)}");
            }

            // 코드에는 있는데 시트에 없는 값 (안내만)
            var missing = new List<string>();
            var it = so.GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
                if (it.name != "m_Script" && IsSupported(it) && !seen.Contains(it.name)) missing.Add(it.name);
            if (missing.Count > 0)
                LogI($"{Tag} {t.name}: 시트에 없는 값 {missing.Count}개 (에셋 값 유지): {string.Join(", ", missing)}", t.target);

            if (changes.Count > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(t.target);
                LogI($"{Tag} {t.name} 변경 {changes.Count}개:\n  {string.Join("\n  ", changes)}", t.target);
            }
            return (changes.Count, errors);
        }

        static void Warn(ref int errors, SheetSyncSettings.Table t, string msg)
        {
            errors++;
            LogW($"{Tag} {msg}", t.target);
        }

        // ---------- 프리팹 시트 (prefab / child / component / key / value) ----------
        // 프리팹 에셋의 컴포넌트 값을 직접 바꾼다. _Net Variant는 원본을 따라간다.
        // child: 비우면 루트, "A/B" = 자식 경로, "A/*" = A 바로 아래 자식 중 그 컴포넌트가 있는 것 전부.
        // 씬에 놓인 인스턴스가 Inspector로 이미 덮어쓴 값은 그대로 남는다 (Unity 프리팹 규칙).

        class Row { public int line; public string prefab, child, comp, key, raw; }

        static (int changed, int errors) ApplyPrefabRows(SheetSyncSettings.Table t, string csv)
        {
            var rows = CsvReader.Parse(csv);
            string[] need = { "prefab", "child", "component", "key", "value" };
            int header = rows.FindIndex(r => need.All(n => IndexOf(r, n) >= 0));
            if (header < 0)
            {
                LogE($"{Tag} {t.name}: 'prefab / child / component / key / value' 열이 있는 헤더 행을 못 찾았습니다.");
                return (0, 1);
            }
            var col = need.Select(n => IndexOf(rows[header], n)).ToArray();

            int errors = 0;
            var seen = new HashSet<string>();
            var byPrefab = new Dictionary<string, List<Row>>();
            var order = new List<string>();
            for (int r = header + 1; r < rows.Count; r++)
            {
                var row = new Row
                {
                    line = r + 1,
                    prefab = Cell(rows[r], col[0]), child = Cell(rows[r], col[1]).Trim('/'),
                    comp = Cell(rows[r], col[2]), key = Cell(rows[r], col[3]), raw = Cell(rows[r], col[4]),
                };
                if (row.key.Length == 0 || row.key.StartsWith("#") || row.prefab.Length == 0) continue;
                if (!seen.Add($"{row.prefab}|{row.child}|{row.comp}|{row.key}"))
                { Warn(ref errors, t, $"{t.name} {row.line}행 '{row.key}': 같은 프리팹·자식·컴포넌트·key 중복 — 위쪽 행 값만 씀"); continue; }
                if (!byPrefab.TryGetValue(row.prefab, out var list))
                { byPrefab[row.prefab] = list = new List<Row>(); order.Add(row.prefab); }
                list.Add(row);
            }

            int changed = 0;
            foreach (var path in order)
            {
                var list = byPrefab[path];
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null)
                {
                    Warn(ref errors, t, $"{t.name} {list[0].line}행: 프리팹 없음 '{path}' (이름·폴더가 바뀌었나?) — {list.Count}행 건너뜀");
                    continue;
                }

                // 1) 에셋을 읽기만 해서 바뀔 값이 있는지 본다 (값이 같으면 프리팹을 열지 않음 → 빠름)
                var preview = new List<string>();
                errors += ApplyToRoot(t, asset, list, preview, log: true);
                if (preview.Count == 0) continue;

                // 2) 바뀔 게 있으면 프리팹을 열어 실제로 쓰고 저장
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var applied = new List<string>();
                    ApplyToRoot(t, root, list, applied, log: false);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed += applied.Count;
                    LogI($"{Tag} {t.name} · {System.IO.Path.GetFileNameWithoutExtension(path)} 변경 {applied.Count}개:\n  {string.Join("\n  ", applied)}", asset);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            return (changed, errors);
        }

        // root 아래 대상 컴포넌트에 값을 넣는다. changes에 바뀐 내용, 반환값은 오류 수.
        // log=false면 오류 로그를 다시 찍지 않는다 (미리보기에서 이미 찍음).
        static int ApplyToRoot(SheetSyncSettings.Table t, GameObject root, List<Row> rows, List<string> changes, bool log)
        {
            int errors = 0;
            void Err(string m) { errors++; if (log) LogW($"{Tag} {m}", root); }

            var cache = new Dictionary<Component, SerializedObject>();
            foreach (var row in rows)
            {
                string where = $"{t.name} {row.line}행 {root.name}/{(row.child.Length > 0 ? row.child + "/" : "")}{row.comp}.{row.key}";
                var targets = FindComponents(root, row.child, row.comp, out string findErr);
                if (targets.Count == 0) { Err($"{where}: {findErr}"); continue; }

                foreach (var c in targets)
                {
                    if (!cache.TryGetValue(c, out var so)) cache[c] = so = new SerializedObject(c);
                    var prop = so.FindProperty(row.key);
                    if (prop == null) { Err($"{where}: 코드에 없는 key (오타?)"); break; }
                    if (!TrySet(prop, row.raw, FieldOf(c.GetType(), row.key), out string before, out string err))
                    { Err($"{where}: {err} (값 '{row.raw}') → 이전 값 {before} 유지"); break; }
                    if (before != null)
                        changes.Add($"{(c.transform == root.transform ? "" : PathOf(root.transform, c.transform) + "/")}{row.comp}.{row.key}: {before} → {Show(prop)}");
                }
            }
            // 미리보기(에셋 직접 읽기)에서는 적용하지 않는다
            if (!log) foreach (var so in cache.Values) so.ApplyModifiedPropertiesWithoutUndo();
            return errors;
        }

        static List<Component> FindComponents(GameObject root, string child, string comp, out string err)
        {
            err = null;
            var result = new List<Component>();
            var holders = new List<Transform>();
            if (child.Length == 0) holders.Add(root.transform);
            else if (child == "*" || child.EndsWith("/*"))
            {
                var parentPath = child == "*" ? "" : child.Substring(0, child.Length - 2);
                var parent = parentPath.Length == 0 ? root.transform : root.transform.Find(parentPath);
                if (parent == null) { err = $"자식 '{parentPath}' 없음"; return result; }
                foreach (Transform c in parent) holders.Add(c);
            }
            else
            {
                var tr = root.transform.Find(child);
                if (tr == null) { err = $"자식 '{child}' 없음 (이름이 바뀌었나?)"; return result; }
                holders.Add(tr);
            }

            foreach (var h in holders)
                foreach (var mb in h.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name == comp) { result.Add(mb); break; }
            if (result.Count == 0) err = $"컴포넌트 '{comp}' 없음";
            return result;
        }

        static string PathOf(Transform root, Transform t)
        {
            var parts = new List<string>();
            for (; t != null && t != root; t = t.parent) parts.Insert(0, t.name);
            return string.Join("/", parts);
        }

        static int IndexOf(List<string> row, string name) =>
            row.FindIndex(c => string.Equals(c.Trim(), name, StringComparison.OrdinalIgnoreCase));

        static string Cell(List<string> row, int col) => col < row.Count ? row[col].Trim() : "";

        static FieldInfo FieldOf(Type type, string name)
        {
            for (var t = type; t != null && t != typeof(ScriptableObject); t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            return null;
        }

        // ---------- 값 변환 ----------

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static bool IsSupported(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Float or SerializedPropertyType.Integer or SerializedPropertyType.Boolean
                or SerializedPropertyType.String or SerializedPropertyType.Enum
                or SerializedPropertyType.Vector2 or SerializedPropertyType.Vector3 or SerializedPropertyType.Color => !p.isArray || p.propertyType == SerializedPropertyType.String,
            _ => false,
        };

        // 성공 시 before = 바뀌기 전 값 (안 바뀌었으면 null)
        static bool TrySet(SerializedProperty p, string raw, FieldInfo field, out string before, out string err)
        {
            string old = Show(p);
            before = old;
            err = null;
            var range = field?.GetCustomAttribute<RangeAttribute>();
            var min = field?.GetCustomAttribute<MinAttribute>();

            switch (p.propertyType)
            {
                case SerializedPropertyType.Float:
                {
                    if (!TryFloat(raw, out float v)) { err = "숫자가 아님"; return false; }
                    if (!InRange(v, range, min, out err)) return false;
                    if (Mathf.Approximately(p.floatValue, v)) { before = null; return true; }
                    p.floatValue = v;
                    return true;
                }
                case SerializedPropertyType.Integer:
                {
                    if (!TryFloat(raw, out float f) || Mathf.Abs(f - Mathf.Round(f)) > 0.0001f) { err = "정수가 아님"; return false; }
                    int v = Mathf.RoundToInt(f);
                    if (!InRange(v, range, min, out err)) return false;
                    if (p.intValue == v) { before = null; return true; }
                    p.intValue = v;
                    return true;
                }
                case SerializedPropertyType.Boolean:
                {
                    bool v;
                    switch (raw.ToUpperInvariant())
                    {
                        case "TRUE": case "1": case "Y": case "O": case "ON": v = true; break;
                        case "FALSE": case "0": case "N": case "X": case "OFF": v = false; break;
                        default: err = "TRUE/FALSE가 아님"; return false;
                    }
                    if (p.boolValue == v) { before = null; return true; }
                    p.boolValue = v;
                    return true;
                }
                case SerializedPropertyType.String:
                    if (p.stringValue == raw) { before = null; return true; }
                    p.stringValue = raw;
                    return true;
                case SerializedPropertyType.Enum:
                {
                    int idx = Array.FindIndex(p.enumNames, n => string.Equals(n, raw, StringComparison.OrdinalIgnoreCase));
                    if (idx < 0 && int.TryParse(raw, out int n2) && n2 >= 0 && n2 < p.enumNames.Length) idx = n2;
                    if (idx < 0) { err = $"선택지에 없음 ({string.Join("/", p.enumNames)})"; return false; }
                    if (p.enumValueIndex == idx) { before = null; return true; }
                    p.enumValueIndex = idx;
                    return true;
                }
                case SerializedPropertyType.Vector2:
                case SerializedPropertyType.Vector3:
                {
                    int n = p.propertyType == SerializedPropertyType.Vector2 ? 2 : 3;
                    var parts = raw.Split(new[] { ',', ' ', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    var vals = new float[n];
                    if (parts.Length != n || parts.Where((s, i) => !TryFloat(s, out vals[i])).Any())
                    { err = n == 2 ? "'x, y' 형식이 아님" : "'x, y, z' 형식이 아님"; return false; }
                    if (n == 2)
                    {
                        var v = new Vector2(vals[0], vals[1]);
                        if (p.vector2Value == v) { before = null; return true; }
                        p.vector2Value = v;
                    }
                    else
                    {
                        var v = new Vector3(vals[0], vals[1], vals[2]);
                        if (p.vector3Value == v) { before = null; return true; }
                        p.vector3Value = v;
                    }
                    return true;
                }
                case SerializedPropertyType.Color:
                {
                    // #RRGGBB / #RRGGBBAA 또는 r, g, b(, a) (0~1)
                    Color v;
                    if (raw.StartsWith("#")) { if (!ColorUtility.TryParseHtmlString(raw, out v)) { err = "'#RRGGBBAA' 형식이 아님"; return false; } }
                    else
                    {
                        var parts = raw.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
                        var c = new float[] { 0, 0, 0, 1 };
                        if (parts.Length < 3 || parts.Length > 4 || parts.Where((s, i) => !TryFloat(s, out c[i])).Any())
                        { err = "'#RRGGBBAA' 또는 'r, g, b, a' 형식이 아님"; return false; }
                        v = new Color(c[0], c[1], c[2], c[3]);
                    }
                    // #hex는 1/255 단위라 반 칸 안이면 같은 색으로 본다 (0.45 같은 값이 미세하게 바뀌지 않게)
                    var o = p.colorValue;
                    const float tol = 0.5f / 255f + 0.0001f;
                    if (Mathf.Abs(o.r - v.r) <= tol && Mathf.Abs(o.g - v.g) <= tol && Mathf.Abs(o.b - v.b) <= tol && Mathf.Abs(o.a - v.a) <= tol)
                    { before = null; return true; }
                    p.colorValue = v;
                    return true;
                }
                default:
                    err = $"시트로 넣을 수 없는 타입 ({p.propertyType})";
                    return false;
            }
        }

        static bool TryFloat(string s, out float v) =>
            float.TryParse(s.Replace(" ", ""), NumberStyles.Float, Inv, out v);

        static bool InRange(float v, RangeAttribute range, MinAttribute min, out string err)
        {
            err = null;
            if (range != null && (v < range.min || v > range.max)) { err = $"범위 밖 ({range.min}~{range.max})"; return false; }
            if (min != null && v < min.min) { err = $"{min.min} 이상이어야 함"; return false; }
            return true;
        }

        static string Show(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Float => p.floatValue.ToString("0.####", Inv),
            SerializedPropertyType.Integer => p.intValue.ToString(Inv),
            SerializedPropertyType.Boolean => p.boolValue ? "TRUE" : "FALSE",
            SerializedPropertyType.String => $"\"{p.stringValue}\"",
            SerializedPropertyType.Enum => p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length ? p.enumNames[p.enumValueIndex] : p.enumValueIndex.ToString(),
            SerializedPropertyType.Vector2 => $"({p.vector2Value.x.ToString(Inv)}, {p.vector2Value.y.ToString(Inv)})",
            SerializedPropertyType.Vector3 => $"({p.vector3Value.x.ToString(Inv)}, {p.vector3Value.y.ToString(Inv)}, {p.vector3Value.z.ToString(Inv)})",
            SerializedPropertyType.Color => "#" + ColorUtility.ToHtmlStringRGBA(p.colorValue),
            _ => p.propertyType.ToString(),
        };

        // ---------- 시트로 관리되는 에셋 Inspector 상단 안내 ----------

        static SheetSyncSettings cachedSettings;

        static void DrawSyncedHeader(Editor editor)
        {
            if (editor.targets.Length != 1 || !(editor.target is ScriptableObject so) || so is SheetSyncSettings) return;
            if (cachedSettings == null) cachedSettings = FindSettings();
            var t = cachedSettings?.tables.FirstOrDefault(x => x != null && x.target == so);
            if (t == null) return;

            EditorGUILayout.HelpBox(
                $"이 값은 구글 시트 '{t.name}' 탭에서 관리합니다. 여기서 바꾸면 다음 Play 때 시트 값으로 덮어써집니다." +
                (AutoOnPlay ? "" : "\n(이 PC는 자동 동기화가 꺼져 있음: Tools/Data/Auto Sync On Play)"),
                MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("시트 열기")) Application.OpenURL(cachedSettings.EditUrl(t));
                if (GUILayout.Button("지금 동기화")) SyncAll(fromPlay: false);
            }
        }
    }
}
