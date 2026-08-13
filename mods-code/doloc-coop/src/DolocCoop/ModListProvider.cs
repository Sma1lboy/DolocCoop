using System;
using System.Collections.Generic;
using CoopCore;

namespace DolocCoop
{
    /// <summary>
    /// 从游戏的 ModManager 读出本机启用的 Mod,喂给 CoopSession 做进房校验。
    ///
    /// 只报**内容 Mod**(MODS 目录下那种 JSON+贴图的)。代码 Mod 是 BepInEx 加载的 DLL,
    /// 游戏的 ModManager 根本看不见它们 —— 除非它们像 DTMAPI 那样另放一个只有 info.json
    /// 的壳文件夹来在 Mod 菜单里露面。那种壳会被算进来,这是对的:
    /// 壳在不在,恰好说明对应的代码 Mod 装没装。
    /// </summary>
    internal static class ModListProvider
    {
        /// <summary>
        /// 这些不参与校验。
        ///
        /// 开发工具纯粹是本机调试用的,不影响任何人看到的世界;
        /// 拿它卡人只会让"我开着 DevTools 就没人能进我的房"。
        /// 联机 Mod 自己也不用列 —— 双方能握上手就已经证明都装了,
        /// 而且它的版本已经由 Hello 里的 modVersion 单独校验过了。
        /// </summary>
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "doloc-devtools",
            "doloc-coop",
        };

        public static List<ModEntry> Read()
        {
            var list = new List<ModEntry>();
            try
            {
                var mm = DolocAPI.modManager;
                if (mm == null) return list;

                foreach (var m in mm.GetAllEnabledModInfos())
                {
                    if (m == null || string.IsNullOrEmpty(m.id)) continue;
                    if (Excluded.Contains(m.id)) continue;
                    list.Add(new ModEntry
                    {
                        Id = m.id,
                        // title 是本地化后的显示名;拒绝理由要给玩家看,用它比用 id 友好
                        Title = m.title ?? m.id,
                        Version = m.manifest?.version ?? "",
                        WorkshopId = m.workshopId,
                        Priority = m.priority,
                        // 游戏自己算好的:一套贴图每个动作每一帧都齐全才为真
                        OverridesPlayer = m.HasPlayerOverride,
                        OverridesHair = m.HasPlayerHairOverride,
                        OverridesBody = m.HasPlayerBodyOverride,
                    });
                }
                // 排序让日志和 UI 稳定,也让两端的清单顺序无关
                list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[ModList] 读取启用的 Mod 失败: " + e.Message);
            }
            return list;
        }

        /// <summary>刷新 CoopSession 用的清单。建立会话前、以及热重载 Mod 之后都要调。</summary>
        public static void Refresh()
        {
            var mods = Read();
            CoopSession.LocalMods = mods;
            NetLog.Log($"MODLIST 本机启用 {mods.Count} 个内容 Mod");
            foreach (var m in mods)
                NetLog.Log($"MODLIST_ITEM id={m.Id} ver={(string.IsNullOrEmpty(m.Version) ? "(无)" : m.Version)} workshop={m.WorkshopId}");
        }
    }
}
