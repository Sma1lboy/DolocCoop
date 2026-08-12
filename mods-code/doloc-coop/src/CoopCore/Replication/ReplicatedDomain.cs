using System;
using System.Collections.Generic;
using System.IO;

namespace CoopCore.Replication
{
    /// <summary>
    /// 一个"主机权威、按 id 对账"的同步域。
    ///
    /// 抽这层的原因:箱子、作物、任务、掉落物各写了一遍**一模一样的骨架** ——
    /// 计时 → 扫描本地 → 算指纹 → 和上次比 → 只发变化的;
    /// 对端收到后按 id 匹配 → 指纹一致就跳过 → 否则应用。
    /// 骨架抄四遍的代价不只是重复代码:
    /// 之前"指纹初值用空串导致永不广播"这个 bug,就得在每个类里各修一次
    /// (实际上 DropItemSync 和 MissionSync 都中了同一枪)。
    ///
    /// 现在骨架只有这一份,子类只描述三件事:**怎么读、怎么编解码、怎么写回**。
    /// </summary>
    public abstract class ReplicatedDomain<T> : ISyncDomain where T : class
    {
        /// <summary>频道号。必须全局唯一,注册时会校验。</summary>
        public abstract byte Channel { get; }

        /// <summary>人类可读的名字,只用于日志。</summary>
        public abstract string Name { get; }

        /// <summary>多久扫一次本地状态(秒)。变化慢的域可以放宽。</summary>
        protected virtual float ScanInterval => 2f;

        /// <summary>单条消息最多带多少条目,避免一帧塞爆网络。</summary>
        protected virtual int MaxPerMessage => 32;

        // ---- 子类必须实现的三件事 ----

        /// <summary>主机侧:读出当前本地状态。</summary>
        protected abstract IEnumerable<T> ReadAll();

        /// <summary>跨端稳定标识。</summary>
        protected abstract string IdOf(T item);

        /// <summary>内容指纹。相同即认为无变化。</summary>
        protected abstract string SignatureOf(T item);

        protected abstract void Encode(BinaryWriter w, T item);
        protected abstract T Decode(BinaryReader r);

        /// <summary>客机侧:把远端状态写回本地。返回是否真的改动了。</summary>
        protected abstract bool ApplyOne(T remote);

        /// <summary>可选:每轮应用结束后的收尾(比如打一条汇总日志)。</summary>
        protected virtual void AfterApply(int received, int applied) { }

        // ---- 以下是共享骨架,子类不用碰 ----

        private readonly Dictionary<string, string> _signatures = new Dictionary<string, string>();
        private float _timer;

        /// <summary>已跟踪的条目数,给面板显示用。</summary>
        public int TrackedCount => _signatures.Count;

        /// <summary>日志出口,由宿主注入。</summary>
        public Action<string> Log { get; set; }

        public void HostTick(float deltaSeconds, Action<byte, byte[], int> send)
        {
            _timer += deltaSeconds;
            if (_timer < ScanInterval) return;
            _timer = 0f;

            var changed = new List<T>();
            try
            {
                foreach (var item in ReadAll())
                {
                    if (item == null) continue;
                    string id = IdOf(item);
                    if (string.IsNullOrEmpty(id)) continue;

                    string sig = SignatureOf(item);
                    if (_signatures.TryGetValue(id, out string prev) && prev == sig) continue;

                    _signatures[id] = sig;
                    changed.Add(item);
                    if (changed.Count >= MaxPerMessage) break;
                }
            }
            catch (Exception e)
            {
                Log?.Invoke($"[{Name}] 扫描失败: {e.Message}");
                return;
            }

            if (changed.Count == 0) return;

            try
            {
                using (var ms = new MemoryStream())
                using (var bw = new BinaryWriter(ms))
                {
                    bw.Write((ushort)changed.Count);
                    foreach (var item in changed) Encode(bw, item);
                    var payload = ms.ToArray();
                    send(Channel, payload, payload.Length);
                }
                Log?.Invoke($"[{Name}] 已广播 {changed.Count} 条变化");
            }
            catch (Exception e)
            {
                Log?.Invoke($"[{Name}] 编码失败: {e.Message}");
            }
        }

        public void OnPayload(BinaryReader r)
        {
            int applied = 0, count = 0;
            try
            {
                count = r.ReadUInt16();
                for (int i = 0; i < count; i++)
                {
                    var item = Decode(r);
                    if (item == null) continue;
                    if (ApplyOne(item)) applied++;
                }
            }
            catch (Exception e)
            {
                Log?.Invoke($"[{Name}] 应用失败: {e.Message}");
            }
            AfterApply(count, applied);
        }

        /// <summary>
        /// 清空指纹缓存,让下一轮把所有条目重发一遍。新人进房时用。
        ///
        /// 注意这里清的是"上次发过什么"的记忆,而不是把指纹设成某个初值 ——
        /// 空集合的指纹也是合法值,拿它当"没发过"的哨兵会导致
        /// "本来就没有东西"的房间永远不广播(这个坑踩过两次)。
        /// </summary>
        public void ResetSignatures()
        {
            _signatures.Clear();
            _timer = ScanInterval;   // 让下一帧立刻触发
        }
    }

    /// <summary>非泛型入口,给注册表统一持有。</summary>
    public interface ISyncDomain
    {
        byte Channel { get; }
        string Name { get; }
        int TrackedCount { get; }
        Action<string> Log { get; set; }

        void HostTick(float deltaSeconds, Action<byte, byte[], int> send);
        void OnPayload(BinaryReader r);
        void ResetSignatures();
    }
}
