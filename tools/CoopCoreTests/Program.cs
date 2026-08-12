using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CoopCore;

namespace CoopCoreTests
{
    /// <summary>
    /// CoopCore 的自动化测试。
    ///
    /// 为什么值得单独做:游戏侧的同步逻辑必须开着游戏才能验证,而且要求存档里
    /// 恰好有箱子、地上恰好有掉落物 —— 环境依赖重、反馈慢。
    /// 但协议编解码、指纹去重、握手这些**纯逻辑**完全可以脱离游戏跑,
    /// 而我反复踩的坑(枚举值撞车、空指纹哨兵、握手重复触发)恰恰全在这一层。
    ///
    /// 跑法:dotnet run --project tools/CoopCoreTests
    /// 失败会以非零退出码结束,方便挂到 CI 或提交前检查。
    /// </summary>
    internal static class Program
    {
        private static int _passed, _failed;

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== CoopCore 测试 ===\n");

            MsgTypeValuesAreUnique();
            ProtocolVersionIsPositive();
            PlayerStateRoundTrip();
            WorldSyncRoundTrip();
            ContainerRoundTrip();
            DropItemRoundTrip();
            MissionRoundTrip();
            ActionRoundTrip();
            EmptyCollectionsRoundTrip();
            HandshakeRaisesJoinOnce();
            VersionMismatchIsRejected();
            PeerLeftClearsState();
            LoopbackDeliversMessages();

            Console.WriteLine($"\n通过 {_passed} · 失败 {_failed}");
            return _failed == 0 ? 0 : 1;
        }

        // ---------------- 测试用例 ----------------

        /// <summary>
        /// 消息类型的枚举值必须互不重复。
        /// C# 允许重复值并静默变成别名 —— 曾经 SceneChange 和 ContainerSync 都等于 21,
        /// 发前者实际发出去的是后者,这种 bug 在运行时极难定位。
        /// </summary>
        private static void MsgTypeValuesAreUnique()
        {
            var byValue = new Dictionary<byte, List<string>>();
            foreach (var name in Enum.GetNames(typeof(MsgType)))
            {
                byte v = (byte)(MsgType)Enum.Parse(typeof(MsgType), name);
                if (!byValue.TryGetValue(v, out var list)) byValue[v] = list = new List<string>();
                list.Add(name);
            }
            var dupes = byValue.Where(kv => kv.Value.Count > 1)
                               .Select(kv => $"{kv.Key} = {string.Join("/", kv.Value)}").ToList();
            Check("MsgType 枚举值唯一", dupes.Count == 0,
                  dupes.Count == 0 ? "" : "重复: " + string.Join(", ", dupes));
        }

        private static void ProtocolVersionIsPositive()
        {
            Check("协议版本号有效", Protocol.Version > 0, $"当前 {Protocol.Version}");
        }

        private static void PlayerStateRoundTrip()
        {
            var (session, transport, received) = NewPair();
            session.SendLocalState(12.5f, -3.25f, true, 987654, 0.5f);
            transport.DeliverToSelf();

            var peer = session.Peers.Values.FirstOrDefault();
            Check("PlayerState 编解码", peer != null
                && Math.Abs(peer.X - 12.5f) < 1e-4
                && Math.Abs(peer.Y - (-3.25f)) < 1e-4
                && peer.FacingLeft
                && peer.AnimHash == 987654,
                peer == null ? "没收到" : $"得到 ({peer.X},{peer.Y}) faceLeft={peer.FacingLeft} hash={peer.AnimHash}");
            GC.KeepAlive(received);
        }

        private static void WorldSyncRoundTrip()
        {
            var (session, transport, _) = NewPair();
            int gotTime = -1; List<WeatherEntry> gotWeather = null;
            session.WorldSyncReceived += (t, w) => { gotTime = t; gotWeather = w; };

            session.SendWorldSync(4242, new List<WeatherEntry>
            {
                new WeatherEntry { RegionId = "default", WeatherType = 1 },
                new WeatherEntry { RegionId = "wetland", WeatherType = 3 },
            });
            transport.DeliverToSelf();

            Check("WorldSync 编解码", gotTime == 4242 && gotWeather != null && gotWeather.Count == 2
                  && gotWeather[1].RegionId == "wetland" && gotWeather[1].WeatherType == 3,
                  $"time={gotTime} regions={gotWeather?.Count}");
        }

        private static void ContainerRoundTrip()
        {
            var (session, transport, _) = NewPair();
            List<ContainerState> got = null;
            session.ContainersReceived += list => got = list;

            session.SendContainers(new List<ContainerState>
            {
                new ContainerState { Id = "box-a", Slots = new List<SlotItem>
                {
                    new SlotItem { ItemName = "wood", Count = 7 },
                    new SlotItem { ItemName = "", Count = 0 },
                }},
            });
            transport.DeliverToSelf();

            Check("ContainerSync 编解码", got != null && got.Count == 1 && got[0].Id == "box-a"
                  && got[0].Slots.Count == 2 && got[0].Slots[0].ItemName == "wood" && got[0].Slots[0].Count == 7
                  && got[0].Slots[1].ItemName == "",
                  got == null ? "没收到" : $"箱子数={got.Count}");
        }

        private static void DropItemRoundTrip()
        {
            var (session, transport, _) = NewPair();
            List<DropEntry> got = null;
            session.DropItemsReceived += d => got = d;

            session.SendDropItems(new List<DropEntry>
            {
                new DropEntry { ItemName = "stone", X = 1.5f, Y = 2.5f },
            });
            transport.DeliverToSelf();

            Check("DropItemSync 编解码", got != null && got.Count == 1
                  && got[0].ItemName == "stone" && Math.Abs(got[0].X - 1.5f) < 1e-4,
                  got == null ? "没收到" : $"数量={got.Count}");
        }

        private static void MissionRoundTrip()
        {
            var (session, transport, _) = NewPair();
            List<string> got = null;
            session.MissionsReceived += m => got = m;

            session.SendMissions(new List<string> { "m1", "m2", "m3" });
            transport.DeliverToSelf();

            Check("MissionSync 编解码", got != null && got.Count == 3 && got[2] == "m3",
                  got == null ? "没收到" : $"数量={got.Count}");
        }

        private static void ActionRoundTrip()
        {
            var (session, transport, _) = NewPair();
            string gotState = null;
            session.PeerActionReceived += (p, s, x, y) => gotState = s;

            session.SendAction("AgentStateFishing", 5f, 6f);
            transport.DeliverToSelf();

            Check("PlayerAction 编解码", gotState == "AgentStateFishing", $"得到 {gotState}");
        }

        /// <summary>
        /// 空集合也必须能正确往返。
        /// 曾经因为"空 = 没变化"的假设,导致地上没东西 / 一个任务都没完成的存档
        /// 永远不广播,客机的残留状态得不到清理。
        /// </summary>
        private static void EmptyCollectionsRoundTrip()
        {
            var (session, transport, _) = NewPair();
            List<DropEntry> drops = null; List<string> missions = null;
            session.DropItemsReceived += d => drops = d;
            session.MissionsReceived += m => missions = m;

            session.SendDropItems(new List<DropEntry>());
            transport.DeliverToSelf();
            session.SendMissions(new List<string>());
            transport.DeliverToSelf();

            Check("空集合编解码", drops != null && drops.Count == 0 && missions != null && missions.Count == 0,
                  $"drops={drops?.Count.ToString() ?? "null"} missions={missions?.Count.ToString() ?? "null"}");
        }

        /// <summary>
        /// 握手是双向的(Hello 与 HelloAck 各触发一次),PeerJoined 必须只抛一次。
        /// </summary>
        private static void HandshakeRaisesJoinOnce()
        {
            var transport = new FakeTransport();
            var session = new CoopSession(transport, "test");
            int joins = 0;
            session.PeerJoined += _ => joins++;

            transport.RaisePeerConnected(42);   // 触发本端发 Hello
            transport.DeliverToSelf();          // 自己收到自己的 Hello → 回 HelloAck + PeerJoined
            transport.DeliverToSelf();          // 收到 HelloAck → 不应再抛一次

            Check("握手只抛一次 PeerJoined", joins == 1, $"抛了 {joins} 次");
        }

        private static void VersionMismatchIsRejected()
        {
            var transport = new FakeTransport();
            var session = new CoopSession(transport, "test");
            string log = null;
            session.Log += s => log = s;

            // 手工构造一个版本号不对的 Hello
            var bad = MsgWriter.Frame(MsgType.Hello, bw =>
            {
                bw.Write((ushort)(Protocol.Version + 99));
                bw.Write("wrong-version");
            });
            transport.Inject(7, bad);

            Check("版本不匹配被拒绝", log != null && log.Contains("版本"), $"日志: {log ?? "(无)"}");
        }

        private static void PeerLeftClearsState()
        {
            var transport = new FakeTransport();
            var session = new CoopSession(transport, "test");
            transport.RaisePeerConnected(9);
            transport.DeliverToSelf();
            bool hadPeer = session.Peers.Count > 0;

            transport.RaisePeerDisconnected(9);
            Check("离开后清理 peer", hadPeer && session.Peers.Count == 0,
                  $"离开前 {hadPeer}, 离开后 {session.Peers.Count}");
        }

        /// <summary>回环传输真的能在两个实例间送到消息(用真实 UDP,不是假对象)。</summary>
        private static void LoopbackDeliversMessages()
        {
            const int port = 27999;
            var host = new LoopbackTransport(_ => { });
            var client = new LoopbackTransport(_ => { });
            try
            {
                host.StartHost(port);
                client.Connect("127.0.0.1", port);

                var hostSession = new CoopSession(host, "test");
                var clientSession = new CoopSession(client, "test");

                bool hostSawPeer = false;
                hostSession.PeerJoined += _ => hostSawPeer = true;

                // 给 UDP 一点时间,并反复泵
                for (int i = 0; i < 40 && !hostSawPeer; i++)
                {
                    host.Pump(); client.Pump();
                    Thread.Sleep(25);
                }

                Check("回环传输握手", hostSawPeer, hostSawPeer ? "" : "主机没等到客机");
            }
            finally
            {
                host.Dispose(); client.Dispose();
            }
        }

        // ---------------- 测试脚手架 ----------------

        private static (CoopSession, FakeTransport, List<string>) NewPair()
        {
            var transport = new FakeTransport();
            var session = new CoopSession(transport, "test");
            return (session, transport, new List<string>());
        }

        private static void Check(string name, bool ok, string detail = "")
        {
            if (ok) { _passed++; Console.WriteLine($"  ✔ {name}"); }
            else { _failed++; Console.WriteLine($"  ✘ {name}   {detail}"); }
        }
    }

    /// <summary>
    /// 测试用传输:把发出去的包原样回送给自己,于是单个 session 就能验证
    /// "编码 → 解码 → 事件"这条完整链路,不需要起两个进程。
    /// </summary>
    internal sealed class FakeTransport : ITransport
    {
        private readonly Queue<(ulong from, byte[] data, int len)> _outbox = new();

        public bool IsHost { get; set; } = true;
        public ulong SelfId => 1;

        /// <summary>回送时假装成哪个 peer 发来的。RaisePeerConnected 会同步更新它,
        /// 否则"连上的是 9、回送说是 2"会让测试对不上号(第一版脚手架就栽在这)。</summary>
        public ulong RemoteId { get; set; } = 2;

        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;
        public event Action<ulong, byte[], int> MessageReceived;

        public void Send(ulong peerId, byte[] data, int length, SendMode mode) => Enqueue(data, length);
        public void Broadcast(byte[] data, int length, SendMode mode) => Enqueue(data, length);
        public void Pump() { }

        private void Enqueue(byte[] data, int length)
        {
            var copy = new byte[length];
            Array.Copy(data, copy, length);
            _outbox.Enqueue((RemoteId, copy, length));
        }

        /// <summary>把队首的一个包投递回自己。</summary>
        public void DeliverToSelf()
        {
            if (_outbox.Count == 0) return;
            var (from, data, len) = _outbox.Dequeue();
            MessageReceived?.Invoke(from, data, len);
        }

        public void Inject(ulong from, byte[] data) => MessageReceived?.Invoke(from, data, data.Length);
        public void RaisePeerConnected(ulong id) { RemoteId = id; PeerConnected?.Invoke(id); }
        public void RaisePeerDisconnected(ulong id) => PeerDisconnected?.Invoke(id);
    }
}
