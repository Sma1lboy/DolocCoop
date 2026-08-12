using System;
using System.Text;
using DolocTown.Config;
using DolocShared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DolocDevTools
{
    /// <summary>
    /// 开发状态面板。踩过的四个坑,都写在这:
    ///  1. IMGUI(OnGUI)会被游戏的 Screen Space Overlay Canvas 盖住 → 自建 Canvas,sortingOrder 拉满
    ///  2. Resources.GetBuiltinResource&lt;Font&gt;("Arial.ttf") 在打包版返回 null → 借用游戏自己的 TMP 字体
    ///  3. 游戏会销毁 Mod 建的 GameObject → 不用 MonoBehaviour,由 LoopDriver 驱动;每帧自愈重建
    ///  4. 每一步都记日志,出问题能从日志直接定位
    /// </summary>
    public static class StatusOverlay
    {
        private static Canvas _canvas;
        private static TextMeshProUGUI _text;
        private static RectTransform _panel;
        private static GameObject _root;
        private static float _refreshTimer;
        private static string _note = "";
        private static float _noteTime;
        private static bool _visible = true;
        private static int _rebuildCount;
        private static float _rebuildCooldown;

        public static bool Visible
        {
            get { return _visible; }
            set
            {
                _visible = value;
                if (_canvas != null) _canvas.enabled = value;
            }
        }

        /// <summary>由 LoopDriver 每帧调用:被销毁就重建,否则定期刷新内容。</summary>
        public static void Tick()
        {
            if (_root == null || _canvas == null)
            {
                // 重建有冷却,避免游戏持续销毁时每帧疯狂重建
                _rebuildCooldown -= Time.unscaledDeltaTime;
                if (_rebuildCooldown <= 0f)
                {
                    _rebuildCooldown = 2f;
                    Build();
                }
                return;
            }

            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer >= 1f)
            {
                _refreshTimer = 0f;
                Refresh();
                // 平滑字体可能晚一点才生成好,一旦拿到就换上
                if (_text != null && UiFont.HasSmooth && _text.font != UiFont.Smooth)
                {
                    _text.font = UiFont.Smooth;
                    Plugin.Log.LogInfo("[HUD] 已换用平滑字体 " + _text.font.name);
                }
            }
        }

        private static void Build()
        {
            try
            {
                _rebuildCount++;
                _root = new GameObject("DolocDevTools_HUD");
                UnityEngine.Object.DontDestroyOnLoad(_root);
                _root.hideFlags = HideFlags.HideAndDontSave;   // 尽量躲开游戏的清理逻辑

                _canvas = _root.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 32767;
                _canvas.enabled = _visible;
                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;

                var panelGo = new GameObject("panel");
                panelGo.transform.SetParent(_root.transform, false);
                var img = panelGo.AddComponent<Image>();
                img.color = new Color(0.06f, 0.05f, 0.09f, 0.88f);
                img.raycastTarget = false;
                _panel = panelGo.GetComponent<RectTransform>();
                _panel.anchorMin = new Vector2(0f, 1f);
                _panel.anchorMax = new Vector2(0f, 1f);
                _panel.pivot = new Vector2(0f, 1f);
                _panel.anchoredPosition = new Vector2(16f, -16f);
                _panel.sizeDelta = new Vector2(470f, 300f);

                var font = UiFont.Smooth;

                var textGo = new GameObject("text");
                textGo.transform.SetParent(panelGo.transform, false);
                _text = textGo.AddComponent<TextMeshProUGUI>();
                if (font != null) _text.font = font;
                _text.fontSize = 16f;
                _text.color = new Color(0.93f, 0.93f, 0.96f);
                _text.alignment = TextAlignmentOptions.TopLeft;
                _text.richText = true;
                _text.raycastTarget = false;
                var tr = textGo.GetComponent<RectTransform>();
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(12f, 12f);
                tr.offsetMax = new Vector2(-12f, -12f);

                Refresh();
                Plugin.Log.LogInfo($"[HUD] ✔ 面板已构建(第 {_rebuildCount} 次),字体={(font != null ? font.name : "TMP默认")}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[HUD] 构建失败: " + e);
                _root = null; _canvas = null;
            }
        }

        /// <summary>借用游戏里已有的 TMP 字体(支持中文,风格也一致)。</summary>
        private static TMP_FontAsset FindGameFont()
        {
            try
            {
                var texts = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
                foreach (var t in texts)
                    if (t != null && t.font != null) return t.font;
            }
            catch { }
            try { return TMP_Settings.defaultFontAsset; } catch { return null; }
        }

        public static void Note(string msg)
        {
            _note = msg;
            _noteTime = Time.unscaledTime;
            Refresh();
        }

        private static void Refresh()
        {
            if (_text == null) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("<b>DolocCoop 开发面板</b>  <size=13>(F8 开关)</size>");
                sb.AppendLine();

                sb.AppendLine("<b>代码插件 (BepInEx)</b>");
                foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                    sb.AppendLine("  · " + kv.Value.Metadata.Name + "  v" + kv.Value.Metadata.Version);

                sb.AppendLine();
                sb.AppendLine("<b>内容 Mod (MODS 文件夹)</b>");
                try
                {
                    var mm = DolocAPI.modManager;
                    if (mm == null) sb.AppendLine("  (未初始化)");
                    else
                    {
                        var all = mm.GetAllValidModInfos();
                        if (all.Count == 0) sb.AppendLine("  (无)");
                        foreach (ModInfo info in all)
                            sb.AppendLine("  [" + (info.enabled ? "启用" : "禁用") + "] " + info.title);
                    }
                }
                catch { sb.AppendLine("  (游戏尚未就绪)"); }

                sb.AppendLine();
                sb.AppendLine("<b>热键</b>");
                sb.AppendLine("  F8 面板   F7 dump UI   Ctrl+F7 dump 状态");
                sb.AppendLine("  F5 热重载Mod   F1 官方控制台");
                sb.AppendLine("  F9 建大厅   F4 邀请   F6/Ctrl+F6 回环主机/客机");

                if (!string.IsNullOrEmpty(_note) && Time.unscaledTime - _noteTime < 6f)
                {
                    sb.AppendLine();
                    sb.AppendLine("<color=#88ddaa>▶ " + _note + "</color>");
                }

                _text.text = sb.ToString();
                _panel.sizeDelta = new Vector2(470f, Mathf.Max(_text.preferredHeight + 24f, 120f));
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[HUD] 刷新失败: " + e.Message);
            }
        }
    }
}

