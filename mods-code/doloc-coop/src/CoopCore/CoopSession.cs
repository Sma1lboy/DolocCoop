using System;
using System.Collections.Generic;
using System.IO;

namespace CoopCore
{
    /// <summary>地上的一个掉落物。没有持久 id,靠「名字 + 位置」区分。</summary>
    public struct DropEntry
    {
        public string ItemName;
        public float X, Y;
    }

    /// <summary>箱子里的一格(空格用 ItemName="" 表示)。</summary>
    public struct SlotItem
    {
        public string ItemName;
        public int Count;
    }

    /// <summary>一个箱子的完整内容。Id 需要跨端稳定(游戏侧用物体 guid)。</summary>
    public sealed class ContainerState
    {
        public string Id;
        public List<SlotItem> Slots;
    }

    /// <summary>一个天气区域的当前状态。</summary>
    public struct WeatherEntry
    {
        public string RegionId;
        public int WeatherType;
    }

    /// <summary>远端玩家的最新已知状态(游戏无关的通用字段)。</summary>
    public sealed class RemotePeer
    {
        public ulong Id;
        public string Name = "";
        public float X, Y;
        public bool FacingLeft;
        public int AnimHash;        // Animator 状态 shortNameHash(同一 controller 跨端一致)
        public float AnimTime;      // normalizedTime,用于新状态起播对齐
        public string HatId = "";
        public string ActionState = "";   // 当前动作(砍树/浇水/钓鱼…),仅在切换时更新
        public DateTime LastSeenUtc;
    }

    /// <summary>
    /// 主机权威会话:维护 peer 表、处理 Hello 握手与版本校验、
    /// 分发消息。游戏侧只需要喂 ITransport 和自己每帧的本地玩家状态。
    /// </summary>
    public sealed class CoopSession : IDisposable
    {
        private readonly ITransport _transport;
        private readonly string _modVersion;
        public readonly Dictionary<ulong, RemotePeer> Peers = new Dictionary<ulong, RemotePeer>();

        public event Action<RemotePeer> PeerJoined;
        public event Action<RemotePeer> PeerLeft;
        public event Action<RemotePeer> PeerStateUpdated;
        public event Action<ulong, string> ChatReceived;
        /// <summary>收到主机的世界状态:(游戏内累计秒数, 各区域天气)。</summary>
        public event Action<int, List<WeatherEntry>> WorldSyncReceived;
        /// <summary>收到主机的箱子内容(整箱覆盖)。</summary>
        public event Action<List<ContainerState>> ContainersReceived;
        /// <summary>某个玩家的动作状态变了:(玩家, 状态名, x, y)。</summary>
        public event Action<RemotePeer, string, float, float> PeerActionReceived;
        /// <summary>收到主机的已完成任务列表。</summary>
        public event Action<List<string>> MissionsReceived;
        /// <summary>收到主机的掉落物列表(全量对账)。</summary>
        public event Action<List<DropEntry>> DropItemsReceived;
        /// <summary>某个客机报告它捡走了这些掉落物(主机侧处理)。</summary>
        public event Action<ulong, List<DropEntry>> DropPickupReceived;
        public event Action<string> Log;

        public CoopSession(ITransport transport, string modVersion)
        {
            _transport = transport;
            _modVersion = modVersion;
            _transport.PeerConnected += OnPeerConnected;
            _transport.PeerDisconnected += OnPeerDisconnected;
            _transport.MessageReceived += OnMessage;
        }

        public void Pump() => _transport.Pump();

        // ---- 发送 ----

        public void SendLocalState(float x, float y, bool facingLeft, int animHash, float animTime)
        {
            var data = MsgWriter.Frame(MsgType.PlayerState, bw =>
            {
                bw.Write(x); bw.Write(y); bw.Write(facingLeft); bw.Write(animHash); bw.Write(animTime);
            });
            _transport.Broadcast(data, data.Length, SendMode.Unreliable);
        }

        public void SendProfile(string name, string hatId)
        {
            var data = MsgWriter.Frame(MsgType.PlayerProfile, bw =>
            {
                bw.Write(name ?? ""); bw.Write(hatId ?? "");
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        /// <summary>
        /// 主机广播世界状态:游戏内累计秒数 + 各区域当前天气。
        /// 天气用 (区域id, 天气枚举值) 列表表示,区域数量少,整包下发比做增量简单可靠。
        /// </summary>
        public void SendWorldSync(int totalSeconds, IList<WeatherEntry> weather)
        {
            var data = MsgWriter.Frame(MsgType.WorldSync, bw =>
            {
                bw.Write(totalSeconds);
                int n = weather?.Count ?? 0;
                bw.Write((byte)Math.Min(n, 255));
                for (int i = 0; i < n && i < 255; i++)
                {
                    bw.Write(weather[i].RegionId ?? "");
                    bw.Write(weather[i].WeatherType);
                }
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        /// <summary>
        /// 主机广播若干箱子的**整箱**内容。
        /// 整箱覆盖而不是做增量:箱子格数有限(几十格),带宽完全够用,
        /// 而增量同步要处理"拿走/放入/交换/堆叠"一堆边界,极易出现两端货不对板。
        /// </summary>
        public void SendContainers(IList<ContainerState> containers)
        {
            if (containers == null || containers.Count == 0) return;
            var data = MsgWriter.Frame(MsgType.ContainerSync, bw =>
            {
                bw.Write((byte)Math.Min(containers.Count, 255));
                for (int i = 0; i < containers.Count && i < 255; i++)
                {
                    var c = containers[i];
                    bw.Write(c.Id ?? "");
                    var slots = c.Slots ?? new List<SlotItem>();
                    bw.Write((ushort)slots.Count);
                    foreach (var s in slots)
                    {
                        bw.Write(s.ItemName ?? "");
                        bw.Write(s.Count);
                    }
                }
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        /// <summary>
        /// 广播自己的动作状态变化(砍树/浇水/钓鱼…)。
        /// 只在**状态切换时**发,不跟着 15Hz 的位置包走 —— 动作切换本来就是低频事件,
        /// 每帧带一个字符串纯属浪费带宽。
        /// </summary>
        public void SendAction(string actionState, float x, float y)
        {
            var data = MsgWriter.Frame(MsgType.PlayerAction, bw =>
            {
                bw.Write(actionState ?? ""); bw.Write(x); bw.Write(y);
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        /// <summary>
        /// 主机广播当前房间的掉落物列表(全量)。
        /// 全量而非增量:增量一旦丢包或乱序,地上就会留下永远捡不掉的幽灵物品。
        /// </summary>
        public void SendDropItems(IList<DropEntry> drops)
        {
            var data = MsgWriter.Frame(MsgType.DropItemSync, bw =>
            {
                int n = drops?.Count ?? 0;
                bw.Write((ushort)Math.Min(n, ushort.MaxValue));
                for (int i = 0; i < n; i++)
                {
                    bw.Write(drops[i].ItemName ?? "");
                    bw.Write(drops[i].X);
                    bw.Write(drops[i].Y);
                }
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        /// <summary>
        /// 客机上报"我捡走了这些掉落物",请主机从世界里移除。
        ///
        /// 为什么需要:背包是各人各自的,不用同步;但地上的物件是共享世界的一部分。
        /// 客机捡了却不告诉主机,主机下一轮全量广播又会把它重新生成 —— 客机白得一份,
        /// 直接变成刷物品。这条消息就是补上这个缺口。
        /// </summary>
        public void SendDropPickup(IList<DropEntry> picked)
        {
            if (picked == null || picked.Count == 0) return;
            var data = MsgWriter.Frame(MsgType.DropPickup, bw =>
            {
                bw.Write((ushort)Math.Min(picked.Count, ushort.MaxValue));
                for (int i = 0; i < picked.Count; i++)
                {
                    bw.Write(picked[i].ItemName ?? "");
                    bw.Write(picked[i].X);
                    bw.Write(picked[i].Y);
                }
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        /// <summary>主机广播已完成的任务 id 列表。</summary>
        public void SendMissions(IList<string> finishedMissionIds)
        {
            var data = MsgWriter.Frame(MsgType.MissionSync, bw =>
            {
                int n = finishedMissionIds?.Count ?? 0;
                bw.Write((ushort)Math.Min(n, ushort.MaxValue));
                for (int i = 0; i < n; i++) bw.Write(finishedMissionIds[i] ?? "");
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        public void SendChat(string text)
        {
            var data = MsgWriter.Frame(MsgType.Chat, bw => bw.Write(text ?? ""));
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        // ---- 接收 ----

        private void OnPeerConnected(ulong id)
        {
            var data = MsgWriter.Frame(MsgType.Hello, bw =>
            {
                bw.Write(Protocol.Version); bw.Write(_modVersion);
            });
            _transport.Send(id, data, data.Length, SendMode.Reliable);
        }

        private void OnPeerDisconnected(ulong id)
        {
            if (Peers.TryGetValue(id, out var peer))
            {
                Peers.Remove(id);
                _announced.Remove(id);
                PeerLeft?.Invoke(peer);
            }
        }

        private void OnMessage(ulong from, byte[] data, int length)
        {
            switch (MsgWriter.ReadType(data))
            {
                case MsgType.Hello:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        ushort ver = br.ReadUInt16();
                        string modVer = br.ReadString();
                        if (ver != Protocol.Version)
                        {
                            Log?.Invoke($"peer {from} 协议版本不匹配 (本地 {Protocol.Version} vs {ver}/{modVer})");
                            var nack = MsgWriter.Frame(MsgType.HelloAck, bw => { bw.Write(false); bw.Write(Protocol.Version); });
                            _transport.Send(from, nack, nack.Length, SendMode.Reliable);
                            return;
                        }
                        var peer = GetOrAdd(from);
                        var ack = MsgWriter.Frame(MsgType.HelloAck, bw => { bw.Write(true); bw.Write(Protocol.Version); });
                        _transport.Send(from, ack, ack.Length, SendMode.Reliable);
                        RaiseJoinedOnce(peer);
                    }
                    break;

                case MsgType.HelloAck:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        bool ok = br.ReadBoolean();
                        Log?.Invoke(ok ? $"peer {from} 握手成功" : $"peer {from} 拒绝: 版本不匹配");
                        if (ok) RaiseJoinedOnce(GetOrAdd(from));
                    }
                    break;

                case MsgType.PlayerState:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        var peer = GetOrAdd(from);
                        peer.X = br.ReadSingle();
                        peer.Y = br.ReadSingle();
                        peer.FacingLeft = br.ReadBoolean();
                        peer.AnimHash = br.ReadInt32();
                        peer.AnimTime = br.ReadSingle();
                        peer.LastSeenUtc = DateTime.UtcNow;
                        PeerStateUpdated?.Invoke(peer);
                    }
                    break;

                case MsgType.PlayerProfile:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        var peer = GetOrAdd(from);
                        peer.Name = br.ReadString();
                        peer.HatId = br.ReadString();
                        PeerStateUpdated?.Invoke(peer);
                    }
                    break;

                case MsgType.WorldSync:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        int seconds = br.ReadInt32();
                        int n = br.ReadByte();
                        var weather = new List<WeatherEntry>(n);
                        for (int i = 0; i < n; i++)
                            weather.Add(new WeatherEntry { RegionId = br.ReadString(), WeatherType = br.ReadInt32() });
                        WorldSyncReceived?.Invoke(seconds, weather);
                    }
                    break;

                case MsgType.ContainerSync:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        int n = br.ReadByte();
                        var list = new List<ContainerState>(n);
                        for (int i = 0; i < n; i++)
                        {
                            var c = new ContainerState { Id = br.ReadString(), Slots = new List<SlotItem>() };
                            int slotCount = br.ReadUInt16();
                            for (int s = 0; s < slotCount; s++)
                                c.Slots.Add(new SlotItem { ItemName = br.ReadString(), Count = br.ReadInt32() });
                            list.Add(c);
                        }
                        ContainersReceived?.Invoke(list);
                    }
                    break;

                case MsgType.PlayerAction:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        var peer = GetOrAdd(from);
                        peer.ActionState = br.ReadString();
                        float ax = br.ReadSingle(), ay = br.ReadSingle();
                        PeerActionReceived?.Invoke(peer, peer.ActionState, ax, ay);
                    }
                    break;

                case MsgType.DropItemSync:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        int n = br.ReadUInt16();
                        var drops = new List<DropEntry>(n);
                        for (int i = 0; i < n; i++)
                            drops.Add(new DropEntry { ItemName = br.ReadString(), X = br.ReadSingle(), Y = br.ReadSingle() });
                        DropItemsReceived?.Invoke(drops);
                    }
                    break;

                case MsgType.DropPickup:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        int n = br.ReadUInt16();
                        var picked = new List<DropEntry>(n);
                        for (int i = 0; i < n; i++)
                            picked.Add(new DropEntry { ItemName = br.ReadString(), X = br.ReadSingle(), Y = br.ReadSingle() });
                        DropPickupReceived?.Invoke(from, picked);
                    }
                    break;

                case MsgType.MissionSync:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        int n = br.ReadUInt16();
                        var ids = new List<string>(n);
                        for (int i = 0; i < n; i++) ids.Add(br.ReadString());
                        MissionsReceived?.Invoke(ids);
                    }
                    break;

                case MsgType.Chat:
                    using (var br = MsgWriter.Payload(data, length))
                        ChatReceived?.Invoke(from, br.ReadString());
                    break;
            }
        }

        private RemotePeer GetOrAdd(ulong id)
        {
            if (!Peers.TryGetValue(id, out var peer))
                Peers[id] = peer = new RemotePeer { Id = id, LastSeenUtc = DateTime.UtcNow };
            return peer;
        }

        /// <summary>
        /// 只在第一次认识这个 peer 时抛 PeerJoined。
        /// 握手是双向的(Hello 和 HelloAck 各触发一次),不去重会重复通知。
        /// </summary>
        private void RaiseJoinedOnce(RemotePeer peer)
        {
            if (_announced.Contains(peer.Id)) return;
            _announced.Add(peer.Id);
            PeerJoined?.Invoke(peer);
        }

        private readonly HashSet<ulong> _announced = new HashSet<ulong>();

        public void Dispose()
        {
            _transport.PeerConnected -= OnPeerConnected;
            _transport.PeerDisconnected -= OnPeerDisconnected;
            _transport.MessageReceived -= OnMessage;
        }
    }
}

