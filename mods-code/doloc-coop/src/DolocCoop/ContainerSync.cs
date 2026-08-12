using System;
using System.Collections.Generic;
using System.Text;
using CoopCore;
using DolocTown;
using UnityEngine;

namespace DolocCoop
{
    /// <summary>
    /// 箱子同步(主机权威)。
    ///
    /// 做法:主机每 2 秒扫一遍当前已渲染的容器,给每个箱子算一个内容指纹,
    /// **只广播指纹变了的**;客机收到后整箱覆盖。
    ///
    /// 为什么整箱覆盖而不是增量:箱子只有几十格,整箱几百字节,带宽完全够;
    /// 而增量要处理"拿走/放入/交换/堆叠/半堆"一堆边界,任何一处漏掉都会
    /// 让两端货不对板,进而变成刷物品或吞物品的严重 bug。
    ///
    /// 已知边界(留给交互同步那一步):客机自己动箱子是本地生效的,
    /// 要等主机下一次广播才会被纠正回来。真正的做法是客机只发意图、主机结算,
    /// 那需要拦截交互,属于「行为同步」的范畴。
    /// </summary>
    internal static class ContainerSync
    {
        private const float ScanInterval = 2f;

        /// <summary>单次最多广播多少个箱子,避免一帧塞爆网络。</summary>
        private const int MaxPerMessage = 16;

        private static float _timer;
        private static readonly Dictionary<string, string> LastSignature = new Dictionary<string, string>();
        private static int _applied;

        // ---------- 主机侧 ----------

        public static void TickHost(CoopSession session)
        {
            _timer += Time.unscaledDeltaTime;
            if (_timer < ScanInterval) return;
            _timer = 0f;

            try
            {
                var found = FindContainers();
                // 记一条扫描计数:0 个既可能是"这局还没箱子",也可能是扫描坏了,
                // 没这条日志就分不清(实测时被这个问题卡过)
                NetLog.Sample("container-scan", 15, $"CONTAINER_SCAN found={found.Count}");

                var changed = new List<ContainerState>();
                foreach (var c in found)
                {
                    string id = IdOf(c);
                    if (string.IsNullOrEmpty(id)) continue;

                    var state = Read(c, id);
                    string sig = Signature(state);
                    if (LastSignature.TryGetValue(id, out string prev) && prev == sig) continue;

                    LastSignature[id] = sig;
                    changed.Add(state);
                    if (changed.Count >= MaxPerMessage) break;
                }

                if (changed.Count > 0)
                {
                    session.SendContainers(changed);
                    NetLog.Sample("container-send", 3, $"CONTAINER_SEND count={changed.Count} first={changed[0].Id}");
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[ContainerSync] 扫描失败: " + e.Message);
            }
        }

        /// <summary>新人进房时清掉指纹缓存,让下一轮把所有箱子重发一遍。</summary>
        public static void ResendAll()
        {
            LastSignature.Clear();
            _timer = ScanInterval;
        }

        // ---------- 客机侧 ----------

        public static void ApplyRemote(List<ContainerState> states)
        {
            if (states == null || states.Count == 0) return;
            try
            {
                // 收到多少、匹配上多少要分开记:场景里没有对应容器时会静默跳过,
                // 没有这条日志就分不清"没收到"和"收到了但对不上"
                int matched = 0, skipped = 0;
                var byId = new Dictionary<string, IContainer>();
                foreach (var c in FindContainers())
                {
                    string id = IdOf(c);
                    if (!string.IsNullOrEmpty(id)) byId[id] = c;
                }

                foreach (var st in states)
                {
                    if (st == null || string.IsNullOrEmpty(st.Id)) continue;
                    if (!byId.TryGetValue(st.Id, out var target)) { skipped++; continue; }   // 不在当前场景
                    matched++;

                    // 本地已经一致就别覆盖 —— OverwriteInventory 会触发 UI 重绘
                    string localSig = Signature(Read(target, st.Id));
                    string remoteSig = Signature(st);
                    if (localSig == remoteSig) continue;

                    var items = new List<CountItem>();
                    foreach (var s in st.Slots)
                    {
                        if (string.IsNullOrEmpty(s.ItemName) || s.Count <= 0) continue;
                        items.Add(new CountItem(s.ItemName, s.Count));
                    }
                    target.OverwriteInventory(items);
                    _applied++;
                    NetLog.Log($"CONTAINER_APPLY id={st.Id} slots={items.Count} total={_applied}");
                }

                NetLog.Log($"CONTAINER_RECV got={states.Count} matched={matched} skipped={skipped} 场景容器={byId.Count}");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[ContainerSync] 应用失败: " + e.Message);
            }
        }

        // ---------- 工具 ----------

        /// <summary>
        /// 扫描场景里所有容器。范围不止 ContainerObject(储物箱/祭坛/宝箱),
        /// 还包括另外实现 IContainer 的农场设备(鱼缸、孵化器、粉碎机、合成器…)
        /// —— 这些同样存物品,不同步的话两人看到的产出会对不上。
        ///
        /// 注意排除 *UiState:界面类也实现了 IContainer,但它们不是世界里的容器。
        /// </summary>
        private static List<IContainer> FindContainers()
        {
            var result = new List<IContainer>();
            try
            {
                var all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in all)
                {
                    if (!(mb is IContainer c)) continue;
                    if (mb.GetType().Name.EndsWith("UiState")) continue;   // 界面不是世界容器
                    result.Add(c);
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// 跨端稳定标识。优先用物体自带的 guid(InteractableObject.guid);
        /// 没有 guid 的用「房间id@网格坐标」兜底 —— 同一份存档里位置是固定的。
        /// </summary>
        private static string IdOf(IContainer c)
        {
            try
            {
                var mb = c as MonoBehaviour;
                if (mb == null) return null;

                // InteractableObject 自带 guid,是最稳的跨端标识
                if (c is InteractableObject io && !string.IsNullOrEmpty(io.guid))
                    return io.guid;

                // 其余(设备类)用「类型@网格坐标」兜底 —— 同一份存档里位置固定
                var p = mb.transform.position;
                return $"{mb.GetType().Name}@{Mathf.RoundToInt(p.x)},{Mathf.RoundToInt(p.y)}";
            }
            catch { return null; }
        }

        private static ContainerState Read(IContainer c, string id)
        {
            var state = new ContainerState { Id = id, Slots = new List<SlotItem>() };
            try
            {
                c.inventory.ForEach((item, idx) =>
                {
                    state.Slots.Add(item == null
                        ? new SlotItem { ItemName = "", Count = 0 }
                        : new SlotItem { ItemName = item.name, Count = item.count });
                });
            }
            catch { }
            return state;
        }

        // 指纹计算抽到 CoopCore.SyncMath(纯函数,有自动化测试守着)
        private static string Signature(ContainerState s) => SyncMath.ContainerSignature(s);

        /// <summary>面板用:当前场景有多少个容器在同步。</summary>
        public static int TrackedCount => LastSignature.Count;
    }
}


