using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CoopCore
{
    /// <summary>房间 Mod 清单里的一项。</summary>
    public sealed class ModEntry
    {
        /// <summary>Mod 文件夹 id,跨端比对就靠它。</summary>
        public string Id = "";
        /// <summary>给玩家看的名字(已本地化)。</summary>
        public string Title = "";
        /// <summary>info.json 里的 version。空串表示 Mod 没写版本号。</summary>
        public string Version = "";
        /// <summary>创意工坊 id;0 表示是本地 Mod,别人没法自己订阅。</summary>
        public ulong WorkshopId;

        /// <summary>Mod 优先级。多个皮肤同时启用时,游戏按这个决定谁盖住谁。</summary>
        public int Priority;

        /// <summary>
        /// 这个 Mod 是否替换了主角外观。三个标志是游戏自己算好的
        /// (ModInfo.HasPlayerOverride / HasPlayerHairOverride / HasPlayerBodyOverride),
        /// 只有当一套贴图**每个动作每一帧都齐全**时才为真 —— 缺一帧就整个不算,
        /// 所以拿它认皮肤比按文件名猜靠谱。
        /// </summary>
        public bool OverridesPlayer, OverridesHair, OverridesBody;

        /// <summary>是不是一套角色皮肤(三个维度沾一个就算)。</summary>
        public bool IsPlayerSkin => OverridesPlayer || OverridesHair || OverridesBody;
    }

    /// <summary>
    /// 房间 Mod 清单:进房时比对双方启用的 Mod。
    ///
    /// 为什么不传素材。角色皮肤这类东西要让别人也看得见,直觉方案是把贴图发过去,
    /// 实测整套(头发+身体)才 141 KB,带宽根本不是问题。但那样等于让联机 Mod
    /// 变成游戏美术素材的分发通道,而且接收方得对来路不明的字节调 LoadImage。
    /// 改成「只同步身份、各自本地加载」之后,这两个问题一起消失 ——
    /// 传的是一串 id,不是一堆像素。
    ///
    /// 规矩是**房主定**:房主启用了什么,进来的人就必须也有。反过来不要求 ——
    /// 客机自己多装的 Mod 不影响房主看到的世界,拦它没有道理。
    ///
    /// 校验只在房主侧做。握手是双向的(两边都会发 Hello),
    /// 如果两边都按自己的清单卡对方,那"客机多装一个 Mod"就会变成互相拒绝。
    /// </summary>
    public static class RoomMods
    {
        /// <summary>拒绝理由里最多列几个,免得清单一长就糊成一屏。</summary>
        private const int MaxListed = 6;

        public static void Write(BinaryWriter bw, IList<ModEntry> mods)
        {
            int n = mods?.Count ?? 0;
            bw.Write(n);
            for (int i = 0; i < n; i++)
            {
                var m = mods[i];
                bw.Write(m.Id ?? "");
                bw.Write(m.Title ?? "");
                bw.Write(m.Version ?? "");
                bw.Write(m.WorkshopId);
                bw.Write(m.Priority);
                bw.Write(m.OverridesPlayer);
                bw.Write(m.OverridesHair);
                bw.Write(m.OverridesBody);
            }
        }

        public static List<ModEntry> Read(BinaryReader br)
        {
            int n = br.ReadInt32();
            var list = new List<ModEntry>(Math.Max(0, n));
            for (int i = 0; i < n; i++)
            {
                list.Add(new ModEntry
                {
                    Id = br.ReadString(),
                    Title = br.ReadString(),
                    Version = br.ReadString(),
                    WorkshopId = br.ReadUInt64(),
                    Priority = br.ReadInt32(),
                    OverridesPlayer = br.ReadBoolean(),
                    OverridesHair = br.ReadBoolean(),
                    OverridesBody = br.ReadBoolean(),
                });
            }
            return list;
        }

        /// <summary>
        /// 从一份清单里挑出这个人正在用的角色皮肤。
        /// 多套同时启用时按 Priority 取最高的 —— 和游戏自己的覆盖顺序一致
        /// (ModManager 就是按 priority 降序排的)。没有则返回 null。
        /// </summary>
        public static ModEntry FindSkin(IList<ModEntry> mods)
        {
            if (mods == null) return null;
            ModEntry best = null;
            foreach (var m in mods)
            {
                if (m == null || !m.IsPlayerSkin) continue;
                if (best == null || m.Priority > best.Priority) best = m;
            }
            return best;
        }

        /// <summary>
        /// 拿房主的清单去卡客机。返回 null 表示放行,否则返回**给玩家看的**拒绝理由。
        /// </summary>
        public static string Validate(IList<ModEntry> hostMods, IList<ModEntry> clientMods)
        {
            if (hostMods == null || hostMods.Count == 0) return null;

            var have = new Dictionary<string, ModEntry>(StringComparer.OrdinalIgnoreCase);
            if (clientMods != null)
                foreach (var m in clientMods)
                    if (!string.IsNullOrEmpty(m.Id)) have[m.Id] = m;

            var missing = new List<ModEntry>();
            var wrongVersion = new List<KeyValuePair<ModEntry, string>>();   // (房主的, 客机的版本)

            foreach (var need in hostMods)
            {
                if (string.IsNullOrEmpty(need.Id)) continue;
                if (!have.TryGetValue(need.Id, out var mine)) { missing.Add(need); continue; }

                // 双方都写了版本号才比。有一边空着说明那个 Mod 根本没填 version,
                // 这时候硬卡只会把人挡在门外却给不出可执行的解决办法。
                if (!string.IsNullOrEmpty(need.Version) && !string.IsNullOrEmpty(mine.Version)
                    && !string.Equals(need.Version, mine.Version, StringComparison.Ordinal))
                    wrongVersion.Add(new KeyValuePair<ModEntry, string>(need, mine.Version));
            }

            if (missing.Count == 0 && wrongVersion.Count == 0) return null;

            var sb = new StringBuilder();
            if (missing.Count > 0)
            {
                sb.Append("缺少房主启用的 Mod(").Append(missing.Count).Append(" 个):");
                AppendList(sb, missing, m =>
                    m.WorkshopId != 0
                        ? $"{Display(m)}(创意工坊 {m.WorkshopId})"
                        : $"{Display(m)}(房主的本地 Mod,需要向房主索取)");
            }
            if (wrongVersion.Count > 0)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("版本对不上(").Append(wrongVersion.Count).Append(" 个):");
                var items = new List<KeyValuePair<ModEntry, string>>(wrongVersion);
                AppendList(sb, items, kv =>
                    $"{Display(kv.Key)} 房主 {kv.Key.Version} / 本机 {kv.Value}");
            }
            return sb.ToString();
        }

        /// <summary>拿创意工坊 id 拼出可以直接打开的页面地址;本地 Mod 返回 null。</summary>
        public static string WorkshopUrl(ulong workshopId)
        {
            return workshopId == 0
                ? null
                : "https://steamcommunity.com/sharedfiles/filedetails/?id=" + workshopId;
        }

        private static string Display(ModEntry m)
        {
            return string.IsNullOrEmpty(m.Title) ? m.Id : m.Title;
        }

        private static void AppendList<T>(StringBuilder sb, IList<T> items, Func<T, string> render)
        {
            int shown = Math.Min(items.Count, MaxListed);
            for (int i = 0; i < shown; i++) sb.Append("\n · ").Append(render(items[i]));
            if (items.Count > shown) sb.Append("\n · …还有 ").Append(items.Count - shown).Append(" 个");
        }
    }
}
