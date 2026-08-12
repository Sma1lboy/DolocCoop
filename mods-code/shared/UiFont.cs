using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DolocShared
{
    /// <summary>
    /// 给 Mod 的 UI 提供平滑字体。
    ///
    /// 游戏自带的 TMP 字体是像素字体(zpix SDF),复用它会让我们的面板出现"像素点"。
    /// 这里从系统字体(微软雅黑等)运行时生成一个 SDF 字体资产,
    /// 中文可用且边缘平滑;失败时回退到游戏字体。
    /// </summary>
    public static class UiFont
    {
        private static TMP_FontAsset _smooth;
        private static int _attempts;
        private const int MaxAttempts = 8;
        private static Action<string> _log = _ => { };

        public static void SetLogger(Action<string> log) { if (log != null) _log = log; }

        /// <summary>是否已经拿到平滑字体(供 UI 判断要不要换字体重画)。</summary>
        public static bool HasSmooth => _smooth != null;

        /// <summary>
        /// 平滑字体(优先);拿不到时回退游戏内已有的 TMP 字体。
        /// 插件加载很早,TMP 可能还没就绪,所以失败会重试若干次。
        /// </summary>
        public static TMP_FontAsset Smooth
        {
            get
            {
                if (_smooth != null) return _smooth;
                if (_attempts < MaxAttempts)
                {
                    _attempts++;
                    _smooth = TryCreateFromOsFont();
                    if (_smooth == null)
                        _log($"[UiFont] 第 {_attempts}/{MaxAttempts} 次尝试失败,稍后重试");
                }
                return _smooth != null ? _smooth : FindGameFont();
            }
        }

        private static TMP_FontAsset TryCreateFromOsFont()
        {
            // 首选:用游戏自己已经加载的非像素字体(如 SourceHanSansCN)生成 SDF 资产。
            // 打包版里 Font.CreateDynamicFontFromOSFont 常常返回 null,所以这条路更可靠。
            var loaded = TryCreateFromLoadedFont();
            if (loaded != null) return loaded;

            string[] candidates =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
                "SimHei", "黑体", "Noto Sans CJK SC", "Segoe UI", "Arial"
            };

            foreach (var name in candidates)
            {
                try
                {
                    var osFont = Font.CreateDynamicFontFromOSFont(name, 42);
                    if (osFont == null) continue;

                    var asset = TMP_FontAsset.CreateFontAsset(
                        osFont,
                        samplingPointSize: 42,
                        atlasPadding: 5,
                        renderMode: GlyphRenderMode.SDFAA,
                        atlasWidth: 1024,
                        atlasHeight: 1024,
                        atlasPopulationMode: AtlasPopulationMode.Dynamic,
                        enableMultiAtlasSupport: true);

                    if (asset == null) continue;
                    asset.name = "DolocMod_" + name;
                    UnityEngine.Object.DontDestroyOnLoad(asset);
                    _log($"[UiFont] 已生成平滑字体: {name}");
                    return asset;
                }
                catch (Exception e)
                {
                    _log($"[UiFont] {name} 生成失败: {e.Message}");
                }
            }
            _log("[UiFont] 所有系统字体都失败,回退游戏字体(会是像素风)");
            return null;
        }

        /// <summary>
        /// 从游戏已加载的字体里挑一个「非像素 且 有中文」的,生成 TMP SDF 资产。
        /// 必须验证中文:LiberationSans 之类的拉丁字体会让中文显示成方块(0.2.0 踩过)。
        /// </summary>
        private static TMP_FontAsset TryCreateFromLoadedFont()
        {
            string[] pixelish = { "zpix", "dinkie", "bitmap", "pixel", "9px" };
            // 优先级关键字:命中越靠前越优先
            string[] preferred = { "sourcehan", "notosans", "noto", "yahei", "msyh", "simhei", "simsun", "cjk", "hei", "song" };

            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<Font>();
                var candidates = new List<Font>();

                foreach (var f in fonts)
                {
                    if (f == null || string.IsNullOrEmpty(f.name)) continue;
                    string lower = f.name.ToLowerInvariant();

                    bool isPixel = false;
                    foreach (var kw in pixelish)
                        if (lower.Contains(kw)) { isPixel = true; break; }
                    if (isPixel) continue;

                    if (!HasChinese(f))
                    {
                        _log($"[UiFont] 跳过 {f.name}(没有中文字形)");
                        continue;
                    }
                    candidates.Add(f);
                }

                if (candidates.Count == 0)
                {
                    _log("[UiFont] 游戏里没有「非像素且带中文」的字体");
                    return null;
                }

                candidates.Sort((a, b) => Rank(a, preferred).CompareTo(Rank(b, preferred)));

                foreach (var f in candidates)
                {
                    try
                    {
                        var asset = TMP_FontAsset.CreateFontAsset(
                            f, 42, 5, GlyphRenderMode.SDFAA, 1024, 1024,
                            AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
                        if (asset == null) continue;
                        asset.name = "DolocMod_" + f.name;
                        UnityEngine.Object.DontDestroyOnLoad(asset);

                        // 兜底:缺字时回退到游戏字体,宁可像素也不要方块
                        var fallback = FindGameFont();
                        if (fallback != null)
                            asset.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };

                        _log($"[UiFont] 已用游戏字体生成平滑资产: {f.name}(中文已验证)");
                        return asset;
                    }
                    catch (Exception e)
                    {
                        _log($"[UiFont] 用 {f.name} 生成失败: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                _log("[UiFont] 扫描已加载字体失败: " + e.Message);
            }
            return null;
        }

        private static int Rank(Font f, string[] preferred)
        {
            string lower = f.name.ToLowerInvariant();
            for (int i = 0; i < preferred.Length; i++)
                if (lower.Contains(preferred[i])) return i;
            return preferred.Length;
        }

        /// <summary>验证字体是否真的带中文字形(用几个常用字抽查)。</summary>
        private static bool HasChinese(Font f)
        {
            try
            {
                foreach (char c in new[] { '联', '机', '大', '厅' })
                    if (!f.HasCharacter(c)) return false;
                return true;
            }
            catch { return false; }
        }

        /// <summary>游戏内已有的 TMP 字体(像素风,兜底用)。</summary>
        public static TMP_FontAsset FindGameFont()
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
    }
}

