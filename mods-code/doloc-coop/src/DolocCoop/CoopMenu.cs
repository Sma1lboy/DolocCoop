using System;
using System.Collections.Generic;
using DolocTown.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace DolocCoop
{
    /// <summary>
    /// 标题界面的「联机大厅」入口:克隆游戏自己的按钮,所以字体/边框/音效全部原生一致。
    ///
    /// 目标层级(由 UI dump 实测):
    ///   CanvasOverlay_Panels → Container → HomePage(Clone) → TextMenuEx {HomePageTextMenu, GridLayoutGroup}
    ///     └ ui_element_homepage_button {TextButton, DolocButtonComponent, Image=ui_panel_5px}  240x48
    ///         └ text {TextMeshProUGUI, FontHandle}   ← "开启旅程" 等
    /// GridLayoutGroup 会自动排版,不用算坐标。
    /// </summary>
    internal static class CoopMenu
    {
        private const string ButtonName = "CoopLobbyButton";
        private static GameObject _button;
        private static float _checkTimer;

        public static void Tick()
        {
            // 每 1 秒检查一次:回到标题界面/被游戏重建菜单时自动补上按钮
            _checkTimer -= Time.unscaledDeltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = 1f;

            if (_button != null) return;
            TryInject();
        }

        private static void TryInject()
        {
            try
            {
                var menu = FindActiveHomeMenu();
                if (menu == null) return;

                // 已存在就不重复加(游戏可能自己重建过)
                var existing = menu.Find(ButtonName);
                if (existing != null) { _button = existing.gameObject; return; }

                GameObject template = null;
                foreach (Transform child in menu)
                {
                    if (child.GetComponent<Button>() != null) { template = child.gameObject; break; }
                }
                if (template == null)
                {
                    Plugin.Log.LogWarning("[CoopMenu] 标题菜单里没找到可克隆的按钮模板");
                    return;
                }

                _button = UnityEngine.Object.Instantiate(template, menu);
                _button.name = ButtonName;
                _button.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

                SetLabel(_button, "联机大厅");

                var btn = _button.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(OnClick);
                    btn.interactable = true;
                }

                SetupHover(_button, menu);

                Plugin.Log.LogInfo("[CoopMenu] ✔ 已在标题界面插入「联机大厅」按钮(克隆自 " + template.name + ")");
                NetLog.Log("MENU 联机大厅按钮已插入");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("[CoopMenu] 插入按钮失败: " + e);
            }
        }

        /// <summary>找到标题界面那个正在显示的文字菜单容器。</summary>
        private static Transform FindActiveHomeMenu()
        {
            HomePageTextMenu[] menus;
            try { menus = Resources.FindObjectsOfTypeAll<HomePageTextMenu>(); }
            catch { return null; }

            foreach (var m in menus)
            {
                if (m == null) continue;
                if (!m.gameObject.activeInHierarchy) continue;   // 只认当前显示的那个
                if (m.transform.childCount == 0) continue;
                return m.transform;
            }
            return null;
        }

        /// <summary>
        /// 补上悬停效果。游戏的 TextMenu 靠 slots 列表驱动:
        ///   OnSlotPointerEnter → slot.Select() → textColor=selectedColor + 显示 ▶ 箭头
        /// 我们的克隆按钮不在 slots 里,所以自己挂 EventTrigger 复刻同样的表现,
        /// 颜色用反射从 TextMenu 的私有字段读,保证和原生按钮完全一致。
        /// </summary>
        private static void SetupHover(GameObject go, Transform menu)
        {
            try
            {
                var textButton = go.GetComponent<TextButton>();
                if (textButton == null) return;

                Color normal = ReadColor(menu, "normalColor", new Color(0.85f, 0.85f, 0.88f));
                Color selected = ReadColor(menu, "selectedColor", new Color(0.45f, 0.90f, 0.62f));

                var trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                // 注意顺序:先上色再动箭头。箭头 API(GetItemBorder)在某些时机会抛空引用,
                // 放在后面并单独 try,避免它把高亮一起带崩(0.2.0 就栽在这)。
                AddTrigger(trigger, EventTriggerType.PointerEnter, () =>
                {
                    try
                    {
                        ClearOtherSlots(menu, textButton, normal);   // 清掉官方按钮残留的绿字
                        textButton.textColor = selected;
                    }
                    catch (Exception e) { Plugin.Log.LogWarning("[CoopMenu] hover 上色失败: " + e.Message); }

                    try { textButton.GetItemBorder(BorderType.Arrow); }   // ▶ 全局箭头挪过来
                    catch { /* 箭头拿不到就只保留变色,不影响主要反馈 */ }
                });
                AddTrigger(trigger, EventTriggerType.PointerExit, () =>
                {
                    try { textButton.textColor = normal; }
                    catch { }
                    try { DolocAPI.HideItemBorder(); } catch { }
                });

                textButton.textColor = normal;
                Plugin.Log.LogInfo($"[CoopMenu] 悬停效果已挂载(normal={normal}, selected={selected})");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[CoopMenu] 悬停效果挂载失败: " + e.Message);
            }
        }

        /// <summary>把菜单里其它按钮的文字恢复成普通色,保证同一时刻只有一个高亮。</summary>
        private static void ClearOtherSlots(Transform menu, TextButton self, Color normal)
        {
            foreach (Transform child in menu)
            {
                if (child == self.transform) continue;
                var tb = child.GetComponent<TextButton>();
                if (tb != null) tb.textColor = normal;
            }
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, Action action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        /// <summary>反射读取 TextMenu 的私有配色,拿不到就用给定默认值。</summary>
        private static Color ReadColor(Transform menu, string fieldName, Color fallback)
        {
            try
            {
                var comp = menu.GetComponent<TextMenu>();
                if (comp == null) return fallback;
                var f = typeof(TextMenu).GetField(fieldName,
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public);
                if (f == null) return fallback;
                var v = f.GetValue(comp);
                if (v is Color c) return c;
            }
            catch { }
            return fallback;
        }

        private static void SetLabel(GameObject go, string label)
        {
            var tmp = go.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) { tmp.text = label; return; }
            var legacy = go.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label;
        }

        private static void OnClick()
        {
            Plugin.Log.LogInfo("[CoopMenu] 点击「联机大厅」");
            NetLog.Log("MENU 点击联机大厅");
            CoopPanel.Toggle();
        }
    }
}

