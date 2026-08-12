using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DolocShared;

namespace DolocCoop
{
    /// <summary>
    /// 联机管理页面:房间状态 + 成员列表 + 好友列表(每人一个「邀请」按钮)。
    ///
    /// 设计要点:邀请**在页面内完成**,点某个好友的邀请按钮才通过
    /// SteamMatchmaking.InviteUserToLobby 直接发出去 —— 不弹 Steam 覆盖层,
    /// 因此覆盖层被禁用的玩家也能正常邀请。
    ///
    /// 自建 Canvas + PlayerLoop 驱动(游戏会销毁 Mod 的 GameObject,不能用 MonoBehaviour)。
    /// </summary>
    internal static class CoopPanel
    {
        private static GameObject _root;
        private static Canvas _canvas;
        private static TextMeshProUGUI _status;
        private static RectTransform _friendList;
        private static TextMeshProUGUI _friendsTitle;
        private static string _friendSignature = "";
        private static TMP_FontAsset _font;
        private static readonly List<GameObject> FriendRows = new List<GameObject>();
        private static bool _visible;
        private static float _refresh;
        private static string _toast = "";
        private static float _toastAt;

        // 由 CoopRuntime 注入
        public static Func<string> StatusProvider;
        public static Func<List<SteamTransport.FriendInfo>> FriendProvider;
        public static Action OnCreateLobby;
        public static Action<ulong> OnInviteFriend;
        public static Action OnOpenSteamOverlay;

        public static void Toggle()
        {
            _visible = !_visible;
            if (_canvas != null) _canvas.enabled = _visible;
            if (_visible) { Refresh(); RebuildFriendRows(force: true); }
        }

        /// <summary>直接显示面板(进入房间时自动弹出)。</summary>
        public static void Show()
        {
            _visible = true;
            if (_canvas != null) _canvas.enabled = true;
            Refresh();
            RebuildFriendRows(force: true);
        }

        public static void Toast(string msg)
        {
            _toast = msg;
            _toastAt = Time.unscaledTime;
            Refresh();
        }

        public static void Tick()
        {
            if (_root == null)
            {
                if (_visible) Build();
                return;
            }
            if (!_visible) return;

            _refresh -= Time.unscaledDeltaTime;
            if (_refresh <= 0f)
            {
                _refresh = 1f;
                Refresh();
                RebuildFriendRows();
            }
        }

        private static void Build()
        {
            try
            {
                _root = new GameObject("DolocCoop_Panel");
                UnityEngine.Object.DontDestroyOnLoad(_root);

                _canvas = _root.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 30000;
                _canvas.enabled = _visible;
                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                _root.AddComponent<GraphicRaycaster>();

                _font = UiFont.Smooth;

                // 半透明遮罩,挡住底下的点击
                var dim = NewUi("dim", _root.transform);
                var dimImg = dim.gameObject.AddComponent<Image>();
                dimImg.color = new Color(0f, 0f, 0f, 0.55f);
                dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
                dim.offsetMin = Vector2.zero; dim.offsetMax = Vector2.zero;

                // 主面板
                var panel = NewUi("panel", _root.transform);
                var img = panel.gameObject.AddComponent<Image>();
                img.color = new Color(0.09f, 0.08f, 0.12f, 0.98f);
                panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
                panel.pivot = new Vector2(0.5f, 0.5f);
                panel.anchoredPosition = Vector2.zero;
                panel.sizeDelta = new Vector2(760f, 560f);

                var title = NewText("title", panel, 28f);
                title.text = "联机大厅";
                title.alignment = TextAlignmentOptions.Center;
                Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                       new Vector2(0f, -16f), new Vector2(0f, 40f));

                // 左栏:房间状态
                _status = NewText("status", panel, 17f);
                _status.alignment = TextAlignmentOptions.TopLeft;
                var st = _status.rectTransform;
                st.anchorMin = new Vector2(0f, 0f); st.anchorMax = new Vector2(0.5f, 1f);
                st.offsetMin = new Vector2(24f, 80f);
                st.offsetMax = new Vector2(-12f, -64f);

                // 右栏标题
                var flTitle = NewText("friendsTitle", panel, 18f);
                _friendsTitle = flTitle;
                flTitle.text = "<b>好友</b>";
                flTitle.alignment = TextAlignmentOptions.TopLeft;
                var ft = flTitle.rectTransform;
                ft.anchorMin = new Vector2(0.5f, 1f); ft.anchorMax = new Vector2(1f, 1f);
                ft.pivot = new Vector2(0f, 1f);
                ft.offsetMin = new Vector2(12f, -92f);
                ft.offsetMax = new Vector2(-24f, -64f);

                // 右栏:好友列表(可滚动)
                // 结构 viewport(裁剪+接收滚轮) → content(自动撑高),ScrollRect 挂在 viewport 上
                var viewport = NewUi("friendsViewport", panel);
                viewport.anchorMin = new Vector2(0.5f, 0f); viewport.anchorMax = new Vector2(1f, 1f);
                viewport.offsetMin = new Vector2(12f, 84f);
                viewport.offsetMax = new Vector2(-24f, -96f);
                var vpImg = viewport.gameObject.AddComponent<Image>();
                vpImg.color = new Color(0f, 0f, 0f, 0.18f);
                vpImg.raycastTarget = true;                       // 必须能接收射线,否则滚轮无效
                viewport.gameObject.AddComponent<RectMask2D>();    // 超出部分裁掉

                _friendList = NewUi("friendsContent", viewport);
                _friendList.anchorMin = new Vector2(0f, 1f);
                _friendList.anchorMax = new Vector2(1f, 1f);
                _friendList.pivot = new Vector2(0.5f, 1f);
                _friendList.anchoredPosition = Vector2.zero;
                _friendList.sizeDelta = new Vector2(0f, 0f);
                var vlg = _friendList.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 4f;
                vlg.padding = new RectOffset(4, 4, 4, 4);
                vlg.childForceExpandHeight = false;
                vlg.childControlHeight = false;
                vlg.childControlWidth = true;
                vlg.childForceExpandWidth = true;
                var fitter = _friendList.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // 不放滚动条:手工搭的 Scrollbar 手柄尺寸算不对会溢出成大白块,
                // 而鼠标滚轮已经够用(ScrollRect 自带),干脆省掉这个视觉噪音。
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.content = _friendList;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 28f;

                // 底部按钮
                MakeButton(panel, "创建房间", new Vector2(-240f, 24f), 200f, () => OnCreateLobby?.Invoke());
                MakeButton(panel, "用 Steam 界面邀请", new Vector2(0f, 24f), 240f, () => OnOpenSteamOverlay?.Invoke());
                MakeButton(panel, "关闭", new Vector2(240f, 24f), 200f, Toggle);

                Refresh();
                RebuildFriendRows();
                Plugin.Log.LogInfo("[CoopPanel] ✔ 管理页面已构建");
                NetLog.Log("PANEL 已构建");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[CoopPanel] 构建失败: " + e);
                _root = null; _canvas = null;
            }
        }

        private static void RebuildFriendRows(bool force = false)
        {
            if (_friendList == null) return;
            try
            {
                var friends = FriendProvider != null ? FriendProvider() : null;

                // 好友没变化就不重建 —— 否则每秒销毁重建会把滚动位置弹回顶部
                string sig = BuildSignature(friends);
                if (!force && sig == _friendSignature) return;
                _friendSignature = sig;

                foreach (var go in FriendRows) if (go != null) UnityEngine.Object.Destroy(go);
                FriendRows.Clear();

                if (_friendsTitle != null)
                {
                    int inThis = 0, inOther = 0;
                    if (friends != null)
                        foreach (var x in friends) { if (x.InThisGame) inThis++; else if (x.InOtherGame) inOther++; }
                    _friendsTitle.text = friends == null || friends.Count == 0
                        ? "<b>好友</b>"
                        : $"<b>好友</b>  <size=13>在玩本游戏 <color=#7fe08f>{inThis}</color> · 玩别的 {inOther} · 在线 {friends.Count}</size>";
                }

                if (friends == null || friends.Count == 0)
                {
                    var empty = NewText("empty", _friendList, 15f);
                    empty.text = "<color=#999>没有在线好友,或 Steam 未就绪</color>";
                    FriendRows.Add(empty.gameObject);
                    return;
                }

                foreach (var f in friends)
                {
                    var row = NewUi("friend_" + f.Id, _friendList);
                    row.sizeDelta = new Vector2(0f, 34f);
                    var le = row.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = 34f; le.preferredHeight = 34f;

                    var bg = row.gameObject.AddComponent<Image>();
                    bg.color = f.InThisGame ? new Color(0.16f, 0.28f, 0.20f, 1f)
                             : f.InOtherGame ? new Color(0.20f, 0.19f, 0.14f, 1f)
                             : new Color(0.15f, 0.15f, 0.19f, 1f);

                    var name = NewText("name", row, 15f);
                    // ● 绿 = 在玩本游戏   ● 黄 = 在玩别的游戏   ○ 灰 = 仅在线
                    string dot = f.InThisGame ? "<color=#7fe08f>● </color>"
                               : f.InOtherGame ? "<color=#d8c471>● </color>"
                               : "<color=#8899aa>○ </color>";
                    name.text = dot + f.Name + (f.InThisGame ? "  <size=12><color=#7fe08f>本游戏</color></size>" : "");
                    name.alignment = TextAlignmentOptions.Left;
                    name.raycastTarget = false;
                    Anchor(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                           Vector2.zero, Vector2.zero);
                    name.rectTransform.offsetMin = new Vector2(10f, 0f);
                    name.rectTransform.offsetMax = new Vector2(-96f, 0f);

                    ulong fid = f.Id;
                    var btn = NewUi("invite", row);
                    var bImg = btn.gameObject.AddComponent<Image>();
                    bImg.color = new Color(0.26f, 0.34f, 0.48f, 1f);
                    btn.anchorMin = new Vector2(1f, 0.5f); btn.anchorMax = new Vector2(1f, 0.5f);
                    btn.pivot = new Vector2(1f, 0.5f);
                    btn.anchoredPosition = new Vector2(-6f, 0f);
                    btn.sizeDelta = new Vector2(80f, 26f);
                    var b = btn.gameObject.AddComponent<Button>();
                    b.targetGraphic = bImg;
                    b.onClick.AddListener(() => OnInviteFriend?.Invoke(fid));

                    var bt = NewText("label", btn, 14f);
                    bt.text = "邀请";
                    bt.alignment = TextAlignmentOptions.Center;
                    bt.raycastTarget = false;
                    Anchor(bt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                    bt.rectTransform.offsetMin = Vector2.zero;
                    bt.rectTransform.offsetMax = Vector2.zero;

                    FriendRows.Add(row.gameObject);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[CoopPanel] 好友列表刷新失败: " + e.Message);
            }
        }

        /// <summary>好友集合的指纹:id + 是否在游戏中。变了才重建列表。</summary>
        private static string BuildSignature(List<SteamTransport.FriendInfo> friends)
        {
            if (friends == null) return "null";
            var sb = new System.Text.StringBuilder(friends.Count * 12);
            foreach (var f in friends) { sb.Append(f.Id); sb.Append(f.InThisGame ? '+' : (f.InOtherGame ? '~' : '-')); }
            return sb.ToString();
        }

        private static void MakeButton(RectTransform parent, string label, Vector2 pos, float width, Action onClick)
        {
            var rt = NewUi("btn_" + label, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.22f, 0.26f, 0.36f, 1f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 44f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick());

            var t = NewText("label", rt, 17f);
            t.text = label;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            t.rectTransform.offsetMin = Vector2.zero;
            t.rectTransform.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static RectTransform NewUi(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.color = new Color(0.94f, 0.94f, 0.97f);
            t.richText = true;
            return t;
        }

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

        private static void Refresh()
        {
            if (_status == null) return;
            try
            {
                string body = StatusProvider != null ? StatusProvider() : "(无状态)";
                if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime - _toastAt < 5f)
                    body += "\n\n<color=#88ddaa>▶ " + _toast + "</color>";
                _status.text = body;
            }
            catch (Exception e) { _status.text = "状态读取失败: " + e.Message; }
        }
    }
}



