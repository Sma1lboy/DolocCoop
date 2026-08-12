using System;
using System.Collections.Generic;
using System.Text;
using DolocShared;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DolocCoop
{
    /// <summary>
    /// 聊天界面。协议里的 Chat 消息从第一版就在,但一直没有入口 ——
    /// 真联机时"能说话"是马上就需要的东西。
    ///
    /// 交互:回车打开输入框,再回车发送,Esc 取消。
    ///
    /// 打字时必须**接管游戏输入**,否则打字会同时操作角色(WASD 走路、E 交互)。
    /// 用的是游戏自己那套 `DolocAPI.UserInput.DisableAllInput / ResumeCurrentInput`,
    /// 和官方控制台聚焦时的做法一致,行为可预期。
    /// </summary>
    internal static class ChatUi
    {
        /// <summary>消息在屏幕上停留多久(秒)。</summary>
        private const float MessageTtl = 12f;
        private const int MaxLines = 8;
        private const int MaxLength = 120;

        private sealed class Line
        {
            public string Text;
            public float At;
        }

        private static readonly List<Line> Lines = new List<Line>();

        private static GameObject _root;
        private static Canvas _canvas;
        private static TextMeshProUGUI _log;
        private static TMP_InputField _input;
        private static GameObject _inputRow;
        private static bool _typing;
        private static bool _inputDisabled;

        /// <summary>由 CoopRuntime 注入:把一句话发出去。</summary>
        public static Action<string> Send;

        /// <summary>只有在会话中才响应回车,免得单机玩着玩着弹出输入框。</summary>
        public static Func<bool> IsInSession;

        public static void Tick()
        {
            if (_root == null)
            {
                if (IsInSession != null && IsInSession()) Build();
                return;
            }

            PollKeys();
            ExpireOldLines();
        }

        public static void AddIncoming(string who, string text)
        {
            Push($"<color=#8fd6ff>{Escape(who)}</color>: {Escape(text)}");
        }

        public static void AddSystem(string text)
        {
            Push($"<color=#c8c8c8><i>{Escape(text)}</i></color>");
        }

        // ---------- 内部 ----------

        private static void PollKeys()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (!_typing)
            {
                bool inSession = IsInSession == null || IsInSession();
                if (inSession && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    OpenInput();
                return;
            }

            // 打字中
            if (kb.escapeKey.wasPressedThisFrame) { CloseInput(send: false); return; }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) CloseInput(send: true);
        }

        private static void OpenInput()
        {
            _typing = true;
            _inputRow.SetActive(true);
            _input.text = "";
            _input.ActivateInputField();
            _input.Select();

            // 接管游戏输入,否则打字会同时驱动角色
            try
            {
                DolocAPI.UserInput?.DisableAllInput(true);
                _inputDisabled = true;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[Chat] 接管输入失败: " + e.Message); }
        }

        private static void CloseInput(bool send)
        {
            string text = _input != null ? (_input.text ?? "").Trim() : "";
            _typing = false;
            if (_inputRow != null) _inputRow.SetActive(false);
            if (_input != null) { _input.DeactivateInputField(); _input.text = ""; }

            if (_inputDisabled)
            {
                _inputDisabled = false;
                try { DolocAPI.UserInput?.ResumeCurrentInput(); }
                catch (Exception e) { Plugin.Log.LogWarning("[Chat] 恢复输入失败: " + e.Message); }
            }

            if (!send || string.IsNullOrEmpty(text)) return;
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);

            try { Send?.Invoke(text); }
            catch (Exception e) { Plugin.Log.LogWarning("[Chat] 发送失败: " + e.Message); }

            Push($"<color=#a8e6a3>我</color>: {Escape(text)}");
        }

        private static void Push(string line)
        {
            Lines.Add(new Line { Text = line, At = Time.unscaledTime });
            while (Lines.Count > MaxLines) Lines.RemoveAt(0);
            Redraw();
        }

        private static void ExpireOldLines()
        {
            bool changed = false;
            for (int i = Lines.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime - Lines[i].At <= MessageTtl) continue;
                Lines.RemoveAt(i);
                changed = true;
            }
            if (changed) Redraw();
        }

        private static void Redraw()
        {
            if (_log == null) return;
            var sb = new StringBuilder();
            foreach (var l in Lines) sb.AppendLine(l.Text);
            _log.text = sb.ToString();
        }

        /// <summary>玩家名和聊天内容都可能含尖括号,不转义会把 TMP 富文本标签打乱。</summary>
        private static string Escape(string s)
        {
            return string.IsNullOrEmpty(s) ? "" : s.Replace("<", "<​");
        }

        private static void Build()
        {
            try
            {
                _root = new GameObject("DolocCoop_Chat");
                UnityEngine.Object.DontDestroyOnLoad(_root);

                _canvas = _root.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 29000;      // 在联机面板(30000)之下,游戏 UI 之上
                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 1f;
                _root.AddComponent<GraphicRaycaster>();

                var font = UiFont.Smooth;

                // 消息区:左下角,不挡视野
                var logGo = new GameObject("log", typeof(RectTransform));
                logGo.transform.SetParent(_root.transform, false);
                _log = logGo.AddComponent<TextMeshProUGUI>();
                if (font != null) _log.font = font;
                _log.fontSize = 16f;
                _log.color = new Color(1f, 1f, 1f, 0.95f);
                _log.alignment = TextAlignmentOptions.BottomLeft;
                _log.richText = true;
                _log.raycastTarget = false;
                var lrt = _log.rectTransform;
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(0f, 0f);
                lrt.pivot = new Vector2(0f, 0f);
                lrt.anchoredPosition = new Vector2(24f, 96f);
                lrt.sizeDelta = new Vector2(640f, 200f);

                // 输入行:默认隐藏
                _inputRow = new GameObject("inputRow", typeof(RectTransform));
                _inputRow.transform.SetParent(_root.transform, false);
                var rowRt = (RectTransform)_inputRow.transform;
                rowRt.anchorMin = new Vector2(0f, 0f);
                rowRt.anchorMax = new Vector2(0f, 0f);
                rowRt.pivot = new Vector2(0f, 0f);
                rowRt.anchoredPosition = new Vector2(24f, 48f);
                rowRt.sizeDelta = new Vector2(640f, 40f);
                var bg = _inputRow.AddComponent<Image>();
                bg.color = new Color(0.06f, 0.06f, 0.09f, 0.92f);

                var textGo = new GameObject("text", typeof(RectTransform));
                textGo.transform.SetParent(_inputRow.transform, false);
                var inputText = textGo.AddComponent<TextMeshProUGUI>();
                if (font != null) inputText.font = font;
                inputText.fontSize = 16f;
                inputText.color = Color.white;
                inputText.alignment = TextAlignmentOptions.Left;
                var trt = inputText.rectTransform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(10f, 4f); trt.offsetMax = new Vector2(-10f, -4f);

                _input = _inputRow.AddComponent<TMP_InputField>();
                _input.textComponent = inputText;
                _input.textViewport = trt;
                _input.characterLimit = MaxLength;
                _input.lineType = TMP_InputField.LineType.SingleLine;
                _inputRow.SetActive(false);

                AddSystem("联机聊天已就绪 —— 回车说话,Esc 取消");
                Plugin.Log.LogInfo("[Chat] 聊天界面已构建");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[Chat] 构建失败: " + e);
                _root = null; _canvas = null;
            }
        }
    }
}
