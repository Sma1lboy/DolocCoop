using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DolocDevTools
{
    /// <summary>
    /// 把游戏运行时状态 dump 成文本文件,供开发者(和 AI 助手)离线分析。
    /// 输出目录:  %USERPROFILE%\AppData\LocalLow\RedSawGames\DolocTown\DolocCoop-debug\
    /// 每次 dump 写两份:带时间戳的存档 + latest-*.txt(方便直接读最新的)。
    /// </summary>
    public static class DebugDump
    {
        public static string Root
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "DolocCoop-debug");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static void Write(string kind, string content)
        {
            try
            {
                File.WriteAllText(Path.Combine(Root, $"latest-{kind}.txt"), content, Encoding.UTF8);
                Plugin.Log.LogInfo($"[Dump] {kind} 已写入 {Root}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Dump] 写入失败: {e.Message}");
            }
        }

        // ---------- UI 层级树 ----------

        /// <summary>Dump 所有 Canvas 的完整层级,用于定位可克隆的原生 UI 组件。</summary>
        /// <param name="tag">文件名标签(通常是场景名),便于区分标题界面 / 游戏内。</param>
        public static void DumpUiTree(string tag = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Doloc Town UI 层级 dump ===");
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"活动场景: {SceneManager.GetActiveScene().name}");
            sb.AppendLine($"已加载场景数: {SceneManager.sceneCount}");
            sb.AppendLine();

            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
            sb.AppendLine($"找到 {canvases.Length} 个 Canvas");
            sb.AppendLine();

            // 只 dump 根 Canvas(嵌套 Canvas 会随父级一起打印)
            var roots = new List<Canvas>();
            foreach (var c in canvases)
            {
                if (c.transform.parent == null || c.transform.parent.GetComponentInParent<Canvas>() == null)
                    roots.Add(c);
            }

            foreach (var c in roots)
            {
                sb.AppendLine(new string('─', 72));
                sb.AppendLine($"CANVAS: {c.name}   renderMode={c.renderMode}  sortingOrder={c.sortingOrder}  active={c.gameObject.activeInHierarchy}");
                sb.AppendLine(new string('─', 72));
                DumpTransform(c.transform, sb, 0);
                sb.AppendLine();
            }

            string kind = string.IsNullOrEmpty(tag) ? "ui-tree" : "ui-tree-" + Sanitize(tag);
            Write(kind, sb.ToString());
        }

        private static string Sanitize(string s)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s;
        }

        private const int MaxDepth = 14;

        private static void DumpTransform(Transform t, StringBuilder sb, int depth)
        {
            if (depth > MaxDepth) { sb.AppendLine(new string(' ', depth * 2) + "…(超过最大深度)"); return; }

            string indent = new string(' ', depth * 2);
            string act = t.gameObject.activeSelf ? "" : " [inactive]";
            sb.Append($"{indent}{t.name}{act}");

            // 组件列表(跳过 Transform/RectTransform 本身)
            var comps = t.GetComponents<Component>();
            var names = new List<string>();
            foreach (var c in comps)
            {
                if (c == null) { names.Add("<missing>"); continue; }
                var type = c.GetType();
                if (type == typeof(RectTransform) || type == typeof(Transform)) continue;
                names.Add(type.FullName);
            }
            if (names.Count > 0) sb.Append("   {" + string.Join(", ", names.ToArray()) + "}");

            // RectTransform 尺寸位置
            var rt = t as RectTransform;
            if (rt != null)
                sb.Append($"   pos={rt.anchoredPosition} size={rt.sizeDelta}");

            sb.AppendLine();

            // 文本内容(定位按钮靠这个)
            var uiText = t.GetComponent<Text>();
            if (uiText != null && !string.IsNullOrEmpty(uiText.text))
                sb.AppendLine($"{indent}  ↳ Text = \"{uiText.text}\"  font={(uiText.font ? uiText.font.name : "null")} size={uiText.fontSize}");

            var tmp = t.GetComponent<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                sb.AppendLine($"{indent}  ↳ TMP  = \"{tmp.text}\"  font={(tmp.font ? tmp.font.name : "null")} size={tmp.fontSize}");

            var btn = t.GetComponent<Button>();
            if (btn != null)
                sb.AppendLine($"{indent}  ↳ Button interactable={btn.interactable} listeners={btn.onClick.GetPersistentEventCount()}");

            var img = t.GetComponent<Image>();
            if (img != null && img.sprite != null)
                sb.AppendLine($"{indent}  ↳ Image sprite={img.sprite.name} type={img.type}");

            for (int i = 0; i < t.childCount; i++)
                DumpTransform(t.GetChild(i), sb, depth + 1);
        }

        // ---------- 游戏状态 ----------

        /// <summary>Dump 与联机开发相关的运行时状态:玩家、时间、存档、Mod。</summary>
        public static void DumpGameState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Doloc Town 运行时状态 dump ===");
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"游戏版本: {Application.version}");
            sb.AppendLine($"活动场景: {SceneManager.GetActiveScene().name}");
            sb.AppendLine();

            Section(sb, "DolocAPI 静态状态", () =>
            {
                sb.AppendLine($"  IsGameInitialized = {SafeGet(() => DolocAPI.IsGameInitialized.ToString())}");
                sb.AppendLine($"  IsDataLoaded      = {SafeGet(() => DolocAPI.IsDataLoaded.ToString())}");
                sb.AppendLine($"  UseMods           = {SafeGet(() => DolocAPI.UseMods.ToString())}");
                sb.AppendLine($"  gameManager       = {SafeGet(() => DolocAPI.gameManager ? "存在" : "null")}");
            });

            Section(sb, "玩家 (DolocAPI.agent / BodyController)", () =>
            {
                var agent = DolocAPI.agent;
                if (agent == null) { sb.AppendLine("  agent = null(未进存档)"); return; }
                sb.AppendLine($"  transform.position = {agent.transform.position}");
                sb.AppendLine($"  PositionCenter     = {SafeGet(() => agent.PositionCenter.ToString())}");
                sb.AppendLine($"  Velocity           = {SafeGet(() => agent.Velocity.ToString())}");
                sb.AppendLine($"  IsFaceRight        = {agent.IsFaceRight}");
                sb.AppendLine($"  当前状态           = {SafeGet(() => agent.StateManager?.current?.GetType().Name)}");
                sb.AppendLine($"  当前帽子           = {SafeGet(() => agent.CurrentHatRenderInfo?.ToString())}");
                sb.AppendLine();
                sb.AppendLine("  -- 玩家 GameObject 层级(化身克隆的模板)--");
                DumpTransform(agent.transform, sb, 1);
            });

            Section(sb, "Animator 状态(动画同步用)", () =>
            {
                var agent = DolocAPI.agent;
                if (agent == null) { sb.AppendLine("  n/a"); return; }
                var animators = agent.GetComponentsInChildren<Animator>(true);
                sb.AppendLine($"  找到 {animators.Length} 个 Animator");
                foreach (var a in animators)
                {
                    sb.AppendLine($"  · {a.gameObject.name}  controller={(a.runtimeAnimatorController ? a.runtimeAnimatorController.name : "null")}");
                    if (a.runtimeAnimatorController != null)
                    {
                        var info = a.GetCurrentAnimatorStateInfo(0);
                        sb.AppendLine($"      当前状态 hash={info.shortNameHash} normalizedTime={info.normalizedTime:F2} length={info.length:F2}");
                        var clips = a.runtimeAnimatorController.animationClips;
                        sb.AppendLine($"      剪辑数={clips.Length}: {string.Join(", ", Names(clips, 40))}");
                    }
                }
            });

            Section(sb, "Mod 列表", () =>
            {
                var mm = DolocAPI.modManager;
                if (mm == null) { sb.AppendLine("  ModManager = null"); return; }
                sb.AppendLine($"  MODS 根目录: {mm.ModsRoot}");
                foreach (var info in mm.GetAllValidModInfos())
                    sb.AppendLine($"  [{(info.enabled ? "启用" : "禁用")}] {info.title}  by {info.author}  <{info.source}>  {info.rootPath}");
            });

            Section(sb, "BepInEx 插件", () =>
            {
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                    sb.AppendLine($"  {kv.Value.Metadata.Name} v{kv.Value.Metadata.Version}  ({kv.Key})");
            });

            Write("game-state", sb.ToString());
        }

        private static string[] Names(AnimationClip[] clips, int max)
        {
            int n = Mathf.Min(clips.Length, max);
            var result = new string[n];
            for (int i = 0; i < n; i++) result[i] = clips[i] != null ? clips[i].name : "null";
            return result;
        }

        private static void Section(StringBuilder sb, string title, Action body)
        {
            sb.AppendLine(new string('─', 72));
            sb.AppendLine("## " + title);
            sb.AppendLine(new string('─', 72));
            try { body(); }
            catch (Exception e) { sb.AppendLine($"  !! 读取失败: {e.GetType().Name}: {e.Message}"); }
            sb.AppendLine();
        }

        private static string SafeGet(Func<string> f)
        {
            try { return f() ?? "null"; }
            catch (Exception e) { return $"<{e.GetType().Name}>"; }
        }

        // ---------- 联机运行时滚动日志 ----------

        private static StreamWriter _netLog;

        /// <summary>联机调试用的滚动日志(位置同步、握手等高频事件)。</summary>
        public static void Net(string line)
        {
            try
            {
                if (_netLog == null)
                {
                    _netLog = new StreamWriter(Path.Combine(Root, "net-session.log"), append: true, Encoding.UTF8);
                    _netLog.AutoFlush = true;
                    _netLog.WriteLine($"\n===== 会话开始 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
                }
                _netLog.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {line}");
            }
            catch { /* 日志失败不影响游戏 */ }
        }
    }
}
