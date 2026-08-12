using System;
using System.Collections.Generic;
using System.Text;

namespace CoopCore
{
    /// <summary>
    /// 同步的纯计算部分:指纹、标识、对账 diff。
    ///
    /// 为什么单独抽出来:这些判断原本埋在游戏侧,验证它们必须开着游戏,
    /// 还要求存档里恰好有箱子、地上恰好有掉落物 —— 环境依赖重、反馈慢。
    /// 但它们本身完全不依赖游戏:输入是数据,输出还是数据。
    /// 抽到 CoopCore 之后,`.\test.ps1` 秒级就能守住,
    /// 游戏侧只剩"读游戏状态 / 写游戏状态"这层薄壳。
    /// </summary>
    public static class SyncMath
    {
        /// <summary>掉落物坐标的量化精度(格)。太细会因浮点抖动把同一个物品判成两个。</summary>
        public const float DropQuantize = 0.5f;

        // ---------- 标识与指纹 ----------

        /// <summary>掉落物的跨端标识。掉落物没有持久 id,靠「名字 + 量化坐标」区分。</summary>
        public static string DropKey(string itemName, float x, float y, float quantize = DropQuantize)
        {
            int qx = (int)Math.Round(x / quantize, MidpointRounding.AwayFromZero);
            int qy = (int)Math.Round(y / quantize, MidpointRounding.AwayFromZero);
            return (itemName ?? "") + "@" + qx + "," + qy;
        }

        /// <summary>
        /// 掉落物集合的指纹。**顺序无关** —— 遍历顺序变化不该被当成"地面变了"。
        /// 返回 null 表示输入为 null;空集合返回空串,调用方要用 null 而不是空串
        /// 当"还没发过"的哨兵(空串是"地上没东西"的合法指纹)。
        /// </summary>
        public static string DropSignature(IList<DropEntry> drops, float quantize = DropQuantize)
        {
            if (drops == null) return null;
            var keys = new List<string>(drops.Count);
            foreach (var d in drops) keys.Add(DropKey(d.ItemName, d.X, d.Y, quantize));
            keys.Sort(StringComparer.Ordinal);
            var sb = new StringBuilder();
            foreach (var k in keys) { sb.Append(k); sb.Append('|'); }
            return sb.ToString();
        }

        /// <summary>箱子内容的指纹。格位顺序有意义,所以不排序。</summary>
        public static string ContainerSignature(ContainerState state)
        {
            if (state?.Slots == null) return null;
            var sb = new StringBuilder();
            foreach (var slot in state.Slots)
            {
                sb.Append(slot.ItemName ?? "");
                sb.Append('#');
                sb.Append(slot.Count);
                sb.Append('|');
            }
            return sb.ToString();
        }

        /// <summary>任务列表指纹。顺序无关。</summary>
        public static string MissionSignature(IList<string> missionIds)
        {
            if (missionIds == null) return null;
            var ids = new List<string>(missionIds);
            ids.Sort(StringComparer.Ordinal);
            return string.Join(",", ids.ToArray());
        }

        // ---------- 对账 ----------

        /// <summary>掉落物对账结果。</summary>
        public struct DropReconcile
        {
            /// <summary>本地多出来的(主机没有)→ 应当删除,它们的 key。</summary>
            public List<string> RemoveKeys;
            /// <summary>本地缺少的(主机有)→ 应当补建。</summary>
            public List<DropEntry> Add;
        }

        /// <summary>
        /// 计算客机该怎么把本地地面对齐到主机版本。
        /// 纯函数:输入两份列表,输出该删哪些、该补哪些。
        /// </summary>
        public static DropReconcile ReconcileDrops(IList<DropEntry> local, IList<DropEntry> remote,
                                                   float quantize = DropQuantize)
        {
            var result = new DropReconcile { RemoveKeys = new List<string>(), Add = new List<DropEntry>() };

            var remoteKeys = new HashSet<string>();
            if (remote != null)
                foreach (var r in remote) remoteKeys.Add(DropKey(r.ItemName, r.X, r.Y, quantize));

            var localKeys = new HashSet<string>();
            if (local != null)
            {
                foreach (var l in local)
                {
                    string k = DropKey(l.ItemName, l.X, l.Y, quantize);
                    localKeys.Add(k);
                    if (!remoteKeys.Contains(k)) result.RemoveKeys.Add(k);
                }
            }

            if (remote != null)
            {
                foreach (var r in remote)
                {
                    string k = DropKey(r.ItemName, r.X, r.Y, quantize);
                    if (!localKeys.Contains(k)) result.Add.Add(r);
                }
            }

            return result;
        }

        /// <summary>
        /// 找出"从已知地面上消失了"的物品 —— 客机据此判断是不是本地玩家捡走了。
        ///
        /// 调用方必须在**应用完主机广播之后**把 known 更新成主机的版本,
        /// 否则主机删掉的东西会被误判成本地捡拾并反过来上报,两边互相纠正停不下来。
        /// </summary>
        public static List<DropEntry> FindVanished(IDictionary<string, DropEntry> known,
                                                   IList<DropEntry> current,
                                                   float quantize = DropQuantize)
        {
            var result = new List<DropEntry>();
            if (known == null || known.Count == 0) return result;

            var currentKeys = new HashSet<string>();
            if (current != null)
                foreach (var c in current) currentKeys.Add(DropKey(c.ItemName, c.X, c.Y, quantize));

            foreach (var kv in known)
                if (!currentKeys.Contains(kv.Key)) result.Add(kv.Value);

            return result;
        }
    }
}
