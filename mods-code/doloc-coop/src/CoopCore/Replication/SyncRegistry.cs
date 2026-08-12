using System;
using System.Collections.Generic;
using System.IO;

namespace CoopCore.Replication
{
    /// <summary>
    /// 同步域的统一入口。
    ///
    /// 加一个新同步域,过去要动四处:写一个类、在 Tick 里调 TickHost、
    /// 在新人进房时调 ResendAll、在会话上订阅接收事件 —— 漏一处就是"功能写了但不生效",
    /// 而且症状是静默的。现在只需要 `registry.Register(new XxxDomain())` 一行。
    ///
    /// 消息格式:[DomainSync][channel:1][payload...],
    /// 路由靠 channel,注册时会拒绝重复的频道号 ——
    /// 枚举值撞车曾经让"发 A 实际发出 B",这类错误必须在注册那一刻就炸掉,
    /// 而不是等到运行时表现成灵异现象。
    /// </summary>
    public sealed class SyncRegistry
    {
        private readonly Dictionary<byte, ISyncDomain> _domains = new Dictionary<byte, ISyncDomain>();
        private readonly CoopSession _session;
        private readonly Action<string> _log;

        public SyncRegistry(CoopSession session, Action<string> log = null)
        {
            _session = session;
            _log = log ?? (_ => { });
            _session.DomainPayloadReceived += OnPayload;
        }

        public IEnumerable<ISyncDomain> Domains => _domains.Values;

        public void Register(ISyncDomain domain)
        {
            if (domain == null) throw new ArgumentNullException(nameof(domain));
            if (_domains.TryGetValue(domain.Channel, out var existing))
                throw new InvalidOperationException(
                    $"同步频道 {domain.Channel} 重复:{domain.Name} 与 {existing.Name} 撞车");

            domain.Log = _log;
            _domains[domain.Channel] = domain;
            _log($"[Sync] 已注册同步域 {domain.Name}(频道 {domain.Channel})");
        }

        /// <summary>主机每帧调用:驱动所有域的扫描与广播。客机不发。</summary>
        public void HostTick(float deltaSeconds)
        {
            foreach (var d in _domains.Values)
                d.HostTick(deltaSeconds, _session.SendDomainPayload);
        }

        /// <summary>新人进房:让所有域重发一遍完整状态。</summary>
        public void ResendAll()
        {
            foreach (var d in _domains.Values) d.ResetSignatures();
            _log($"[Sync] 已要求 {_domains.Count} 个同步域重发全量状态");
        }

        private void OnPayload(byte channel, BinaryReader r)
        {
            if (_domains.TryGetValue(channel, out var d)) { d.OnPayload(r); return; }

            // 对方有我们没有的同步域 —— 多半是版本不同。握手已经拦了大部分情况,
            // 这里再兜一层:忽略而不是崩,并且只提示一次免得刷屏。
            if (_unknownWarned.Add(channel))
                _log($"[Sync] 收到未知同步频道 {channel},已忽略(对方版本可能更新)");
        }

        private readonly HashSet<byte> _unknownWarned = new HashSet<byte>();
    }
}
