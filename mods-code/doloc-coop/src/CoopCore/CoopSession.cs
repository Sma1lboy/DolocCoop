using System;
using System.Collections.Generic;
using System.IO;

namespace CoopCore
{
    /// <summary>一个种植槽上的作物长势。BasinId 需跨端稳定(游戏侧用「类型#id」)。</summary>
    public sealed class CropState
    {
        public string BasinId;
        public string SeedId = "";
        public int CurrentLevel;
        public int Lifespan;
        public int HarvestTimes;
        public float GrowthValue;
        public float HealthValue;
        public bool IsMature, IsDead, IsMoist, IsPolluted;
    }

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
        /// <summary>对方启用的 Mod 清单(握手时拿到)。用于 UI 展示,不参与逻辑。</summary>
        public List<ModEntry> Mods = new List<ModEntry>();
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
        /// <summary>收到主机的作物长势。</summary>
        public event Action<List<CropState>> CropsReceived;
        /// <summary>收到某个同步域的负载:(频道号, 读取器)。由 SyncRegistry 消费。</summary>
        public event Action<byte, System.IO.BinaryReader> DomainPayloadReceived;
        /// <summary>收到主机的已完成任务列表。</summary>
        public event Action<List<string>> MissionsReceived;
        /// <summary>收到主机的掉落物列表(全量对账)。</summary>
        public event Action<List<DropEntry>> DropItemsReceived;
        /// <summary>某个客机报告它捡走了这些掉落物(主机侧处理)。</summary>
        public event Action<ulong, List<DropEntry>> DropPickupReceived;
        public event Action<string> Log;
        /// <summary>握手被拒(自己拒别人,或被别人拒):(对方id, 原因)。</summary>
        public event Action<ulong, string> Rejected;

        public CoopSession(ITransport transport, string modVersion)
        {
            _transport = transport;
            _modVersion = modVersion;
            _transport.PeerConnected += OnPeerConnected;
            _transport.PeerDisconnected += OnPeerDisconnected;
            _transport.MessageReceived += OnMessage;
        }

        /// <summary>多久没收到任何消息就判定对方掉线(秒)。</summary>
        public double PeerTimeoutSeconds { get; set; } = 10;

        /// <summary>心跳间隔(秒)。必须明显小于超时,否则会误判。</summary>
        public double HeartbeatSeconds { get; set; } = 2;

        private DateTime _lastHeartbeatAt = DateTime.MinValue;

        public void Pump()
        {
            _transport.Pump();
            var now = DateTime.UtcNow;
            SendHeartbeatIfDue(now);
            DropTimedOutPeers(now);
        }

        /// <summary>
        /// 定时发一个空包。
        /// 不能只靠位置包来判断存活:玩家在标题界面、加载中、或者干脆没进存档时
        /// 根本不发位置,那会被误判成掉线。心跳与游戏状态无关,永远在跳。
        /// </summary>
        private void SendHeartbeatIfDue(DateTime now)
        {
            if ((now - _lastHeartbeatAt).TotalSeconds < HeartbeatSeconds) return;
            _lastHeartbeatAt = now;
            var data = MsgWriter.Frame(MsgType.Heartbeat, null);
            _transport.Broadcast(data, data.Length, SendMode.Unreliable);
        }

        /// <summary>
        /// 清掉长时间没消息的 peer。
        /// 回环传输没有底层断线感知,Steam 的 P2P 也不保证一定回调,
        /// 所以超时判定是唯一可靠的兜底 —— 否则对方的化身会永远僵在原地。
        /// </summary>
        private void DropTimedOutPeers(DateTime now)
        {
            List<ulong> dead = null;
            foreach (var kv in Peers)
            {
                if ((now - kv.Value.LastSeenUtc).TotalSeconds <= PeerTimeoutSeconds) continue;
                (dead ??= new List<ulong>()).Add(kv.Key);
            }
            if (dead == null) return;

            foreach (var id in dead)
            {
                var peer = Peers[id];
                Peers.Remove(id);
                _announced.Remove(id);
                Log?.Invoke($"peer {id} 超过 {PeerTimeoutSeconds:F0} 秒无消息,判定掉线");
                PeerLeft?.Invoke(peer);
            }
        }

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

        /// <summary>主机广播作物长势。只发指纹变了的槽。</summary>
        public void SendCrops(IList<CropState> crops)
        {
            if (crops == null || crops.Count == 0) return;
            var data = MsgWriter.Frame(MsgType.CropSync, bw =>
            {
                bw.Write((ushort)Math.Min(crops.Count, ushort.MaxValue));
                foreach (var c in crops)
                {
                    bw.Write(c.BasinId ?? ""); bw.Write(c.SeedId ?? "");
                    bw.Write(c.CurrentLevel); bw.Write(c.Lifespan); bw.Write(c.HarvestTimes);
                    bw.Write(c.GrowthValue); bw.Write(c.HealthValue);
                    bw.Write(c.IsMature); bw.Write(c.IsDead); bw.Write(c.IsMoist); bw.Write(c.IsPolluted);
                }
            });
            _transport.Broadcast(data, data.Length, SendMode.Reliable);
        }

        /// <summary>
        /// 发一个同步域的负载。由 SyncRegistry 调用,业务代码不直接用。
        /// 帧格式:[DomainSync][channel:1][payload…]
        /// </summary>
        public void SendDomainPayload(byte channel, byte[] payload, int length)
        {
            var data = MsgWriter.Frame(MsgType.DomainSync, bw =>
            {
                bw.Write(channel);
                bw.Write(payload, 0, length);
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
                bw.Write(Protocol.Version);
                bw.Write(_modVersion);
                bw.Write(GameVersion ?? "");
                RoomMods.Write(bw, LocalMods);
            });
            _transport.Send(id, data, data.Length, SendMode.Reliable);
        }

        /// <summary>
        /// 本机启用的 Mod 清单。由游戏侧在建立会话前赋值,换 Mod 后重新赋值即可。
        /// 房主的这份就是房间规矩,客机必须全都有。
        /// </summary>
        public static List<ModEntry> LocalMods { get; set; } = new List<ModEntry>();

        /// <summary>
        /// 本机的游戏版本,进房时双方比对。
        /// 游戏版本不同会让存档结构、物品表、任务链都对不上,
        /// 与其让两人连上后世界状态互相打架,不如在握手阶段就明确拒绝。
        /// 由游戏侧在建立会话前赋值。
        /// </summary>
        public static string GameVersion { get; set; } = "";

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
            // 收到任何消息都算"对方还活着" —— 超时判定靠这个时间戳。
            // 只在已握手的 peer 上刷新,免得没通过校验的连接也被当成活着。
            if (Peers.TryGetValue(from, out var alive)) alive.LastSeenUtc = DateTime.UtcNow;

            switch (MsgWriter.ReadType(data))
            {
                case MsgType.Heartbeat:
                    break;   // 时间戳已在上面刷新,没有别的事要做

                case MsgType.Hello:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        ushort ver = br.ReadUInt16();
                        string modVer = br.ReadString();
                        string gameVer = SafeReadString(br);
                        var peerMods = SafeReadMods(br);       // v8 新增,兼容旧端

                        string reject = Validate(ver, modVer, gameVer);

                        // Mod 清单只由房主来卡。握手是双向的,两边都会走到这里;
                        // 若两边都按自己的清单要求对方,客机多装一个 Mod 就会导致互相拒绝。
                        if (reject == null && _transport.IsHost)
                            reject = RoomMods.Validate(LocalMods, peerMods);

                        if (reject != null)
                        {
                            Log?.Invoke($"拒绝 peer {from}: {reject}");
                            var nack = MsgWriter.Frame(MsgType.HelloAck, bw =>
                            {
                                bw.Write(false); bw.Write(Protocol.Version); bw.Write(reject);
                            });
                            _transport.Send(from, nack, nack.Length, SendMode.Reliable);
                            Rejected?.Invoke(from, reject);
                            return;
                        }

                        var peer = GetOrAdd(from);
                        peer.Mods = peerMods;
                        var ack = MsgWriter.Frame(MsgType.HelloAck, bw =>
                        {
                            bw.Write(true); bw.Write(Protocol.Version); bw.Write("");
                        });
                        _transport.Send(from, ack, ack.Length, SendMode.Reliable);
                        RaiseJoinedOnce(peer);
                    }
                    break;

                case MsgType.HelloAck:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        bool ok = br.ReadBoolean();
                        br.ReadUInt16();                        // 对方协议版本,目前只用于日志
                        string reason = SafeReadString(br);
                        if (ok)
                        {
                            Log?.Invoke($"peer {from} 握手成功");
                            RaiseJoinedOnce(GetOrAdd(from));
                        }
                        else
                        {
                            string why = string.IsNullOrEmpty(reason) ? "版本不匹配" : reason;
                            Log?.Invoke($"被 peer {from} 拒绝: {why}");
                            Rejected?.Invoke(from, why);
                        }
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

                case MsgType.CropSync:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        int n = br.ReadUInt16();
                        var crops = new List<CropState>(n);
                        for (int i = 0; i < n; i++)
                            crops.Add(new CropState
                            {
                                BasinId = br.ReadString(), SeedId = br.ReadString(),
                                CurrentLevel = br.ReadInt32(), Lifespan = br.ReadInt32(),
                                HarvestTimes = br.ReadInt32(),
                                GrowthValue = br.ReadSingle(), HealthValue = br.ReadSingle(),
                                IsMature = br.ReadBoolean(), IsDead = br.ReadBoolean(),
                                IsMoist = br.ReadBoolean(), IsPolluted = br.ReadBoolean(),
                            });
                        CropsReceived?.Invoke(crops);
                    }
                    break;

                case MsgType.DomainSync:
                    using (var br = MsgWriter.Payload(data, length))
                    {
                        byte channel = br.ReadByte();
                        DomainPayloadReceived?.Invoke(channel, br);
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

        /// <summary>
        /// 握手校验。返回 null 表示放行,否则返回**给玩家看的**拒绝原因。
        ///
        /// 三项都查:协议版本决定消息能不能解开;mod 版本决定同步行为是否一致;
        /// 游戏版本决定物品表/任务链/存档结构是否对得上。
        /// 任何一项不同都会让两人连上后世界状态互相打架,不如在门口就说清楚。
        /// </summary>
        private string Validate(ushort protocolVersion, string modVersion, string gameVersion)
        {
            if (protocolVersion != Protocol.Version)
                return $"联机协议版本不同(对方 {protocolVersion},本机 {Protocol.Version}),请双方更新到同一版本的联机 Mod";

            if (!string.IsNullOrEmpty(modVersion) && !string.IsNullOrEmpty(_modVersion)
                && modVersion != _modVersion)
                return $"联机 Mod 版本不同(对方 {modVersion},本机 {_modVersion})";

            if (!string.IsNullOrEmpty(gameVersion) && !string.IsNullOrEmpty(GameVersion)
                && gameVersion != GameVersion)
                return $"游戏版本不同(对方 {gameVersion},本机 {GameVersion}),物品与任务数据可能对不上";

            return null;
        }

        /// <summary>读一个可能不存在的字符串 —— 老版本发来的包没有后加的字段。</summary>
        private static string SafeReadString(BinaryReader br)
        {
            try { return br.BaseStream.Position < br.BaseStream.Length ? br.ReadString() : ""; }
            catch { return ""; }
        }

        /// <summary>
        /// 读 Mod 清单;字段不存在(旧端)或读坏了都返回空表。
        /// 返回空表意味着"对方一个 Mod 都没有",房主那边会照常拦 —— 这是对的:
        /// 旧版客机本来就没法保证装了房主要求的 Mod。
        /// </summary>
        private static List<ModEntry> SafeReadMods(BinaryReader br)
        {
            try
            {
                return br.BaseStream.Position < br.BaseStream.Length
                    ? RoomMods.Read(br)
                    : new List<ModEntry>();
            }
            catch { return new List<ModEntry>(); }
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



