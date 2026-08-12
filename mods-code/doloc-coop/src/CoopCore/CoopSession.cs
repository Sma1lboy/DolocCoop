using System;
using System.Collections.Generic;
using System.IO;

namespace CoopCore
{
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

