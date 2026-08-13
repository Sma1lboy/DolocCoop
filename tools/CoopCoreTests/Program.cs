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

            Console.WriteLine("\n-- 断线检测 --");
            TimeoutDropsSilentPeer();
            ActivityKeepsPeerAlive();

            Console.WriteLine("\n-- 握手校验 --");
            RejectsModVersionMismatch();
            RejectsGameVersionMismatch();
            AcceptsWhenVersionsMatch();
            ToleratesMissingGameVersion();

            Console.WriteLine("\n-- 房间 Mod 清单 --");
            ModListRoundTrip();
            AcceptsWhenClientHasAllHostMods();
            AcceptsClientExtraMods();
            RejectsMissingMod();
            RejectsVersionMismatch();
            TolerantWhenEitherVersionBlank();
            EmptyHostListAcceptsAnything();
            RejectReasonNamesWorkshopId();
            RejectReasonTruncatesLongList();

            Console.WriteLine("\n-- 同步计算(SyncMath)--");
            DropKeyQuantizesCoordinates();
            DropSignatureIsOrderIndependent();
            EmptySignatureIsNotNull();
            ContainerSignatureRespectsSlotOrder();
            MissionSignatureIsOrderIndependent();
            ReconcileFindsAddAndRemove();
            ReconcileIdenticalIsNoop();
            FindVanishedDetectsPickup();
            FindVanishedIgnoresUnknown();

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

        // ---------------- 断线检测 ----------------

        /// <summary>长时间没消息的 peer 必须被判掉线,否则对方化身会永远僵在原地。</summary>
        private static void TimeoutDropsSilentPeer()
        {
            var transport = new FakeTransport();
            var session = new CoopSession(transport, BuildInfo.ModVersion) { PeerTimeoutSeconds = 0.2 };
            bool left = false;
            session.PeerLeft += _ => left = true;

            transport.RaisePeerConnected(11);
            transport.DeliverToSelf();               // 完成握手,peer 入表
            bool joined = session.Peers.Count == 1;

            Thread.Sleep(300);                        // 静默超过超时时间
            session.Pump();

            Check("超时判定掉线", joined && left && session.Peers.Count == 0,
                  $"joined={joined} left={left} 剩余={session.Peers.Count}");
        }

        /// <summary>有消息往来的 peer 不能被误判掉线。</summary>
        private static void ActivityKeepsPeerAlive()
        {
            var transport = new FakeTransport();
            var session = new CoopSession(transport, BuildInfo.ModVersion) { PeerTimeoutSeconds = 0.4 };
            bool left = false;
            session.PeerLeft += _ => left = true;

            transport.RaisePeerConnected(12);
            transport.DeliverToSelf();

            // 持续有消息:每次 Pump 前投递一个心跳
            for (int i = 0; i < 6; i++)
            {
                Thread.Sleep(100);
                transport.Inject(12, MsgWriter.Frame(MsgType.Heartbeat, null));
                session.Pump();
            }

            Check("活跃 peer 不被误判", !left && session.Peers.Count == 1,
                  $"left={left} 剩余={session.Peers.Count}");
        }

        // ---------------- 握手校验 ----------------

        /// <summary>构造一个 Hello 包投给 session,返回拒绝原因(null 表示放行)。</summary>
        private static string TryHandshake(string localMod, string localGame,
                                           string remoteMod, string remoteGame,
                                           ushort remoteProtocol)
        {
            CoopSession.GameVersion = localGame;
            var transport = new FakeTransport();
            var session = new CoopSession(transport, localMod);
            string reason = null;
            session.Rejected += (_, why) => reason = why;

            var hello = MsgWriter.Frame(MsgType.Hello, bw =>
            {
                bw.Write(remoteProtocol);
                bw.Write(remoteMod);
                bw.Write(remoteGame);
            });
            transport.Inject(5, hello);
            return reason;
        }

        private static void RejectsModVersionMismatch()
        {
            string why = TryHandshake("0.3.0", "1.00.03", "0.2.0", "1.00.03", Protocol.Version);
            Check("拒绝 Mod 版本不同", why != null && why.Contains("Mod 版本"), $"原因: {why ?? "(放行了)"}");
        }

        private static void RejectsGameVersionMismatch()
        {
            string why = TryHandshake("0.3.0", "1.00.03", "0.3.0", "1.01.00", Protocol.Version);
            Check("拒绝游戏版本不同", why != null && why.Contains("游戏版本"), $"原因: {why ?? "(放行了)"}");
        }

        private static void AcceptsWhenVersionsMatch()
        {
            string why = TryHandshake("0.3.0", "1.00.03", "0.3.0", "1.00.03", Protocol.Version);
            Check("版本一致时放行", why == null, $"却被拒: {why}");
        }

        /// <summary>
        /// 老版本客户端发来的包没有"游戏版本"这个字段,不能因此崩溃或误拒 ——
        /// 协议字段是逐版本追加的,读取必须容忍缺失。
        /// </summary>
        private static void ToleratesMissingGameVersion()
        {
            CoopSession.GameVersion = "1.00.03";
            var transport = new FakeTransport();
            var session = new CoopSession(transport, "0.3.0");
            string reason = null;
            bool joined = false;
            session.Rejected += (_, why) => reason = why;
            session.PeerJoined += _ => joined = true;

            // 只写协议版本和 mod 版本,不写游戏版本(模拟旧端)
            var oldHello = MsgWriter.Frame(MsgType.Hello, bw =>
            {
                bw.Write(Protocol.Version);
                bw.Write("0.3.0");
            });
            transport.Inject(6, oldHello);

            Check("容忍缺失的新增字段", reason == null && joined, $"reason={reason} joined={joined}");
        }

        // ---------------- SyncMath ----------------

        /// <summary>量化要能吸收浮点抖动:同一格里的微小差异必须算成同一个 key。</summary>
        private static void DropKeyQuantizesCoordinates()
        {
            string a = SyncMath.DropKey("wood", 1.50f, 2.50f);
            string b = SyncMath.DropKey("wood", 1.52f, 2.48f);   // 抖动,同一格
            string c = SyncMath.DropKey("wood", 3.50f, 2.50f);   // 明显不同的位置
            Check("量化坐标吸收抖动", a == b && a != c, $"a={a} b={b} c={c}");
        }

        /// <summary>遍历顺序变化不该被当成"地面变了",否则会无谓地反复重广播。</summary>
        private static void DropSignatureIsOrderIndependent()
        {
            var one = new List<DropEntry>
            {
                new DropEntry { ItemName = "wood", X = 1, Y = 1 },
                new DropEntry { ItemName = "stone", X = 5, Y = 5 },
            };
            var two = new List<DropEntry>
            {
                new DropEntry { ItemName = "stone", X = 5, Y = 5 },
                new DropEntry { ItemName = "wood", X = 1, Y = 1 },
            };
            Check("掉落物指纹顺序无关", SyncMath.DropSignature(one) == SyncMath.DropSignature(two));
        }

        /// <summary>
        /// 回归用例:空集合的指纹是空串而不是 null,null 只留给"还没算过"。
        /// 曾经拿空串当"还没发过"的哨兵,导致地上没东西的存档永远不广播。
        /// </summary>
        private static void EmptySignatureIsNotNull()
        {
            string empty = SyncMath.DropSignature(new List<DropEntry>());
            string ofNull = SyncMath.DropSignature(null);
            Check("空集合指纹 ≠ null", empty == "" && ofNull == null,
                  $"empty=[{empty}] ofNull={(ofNull == null ? "null" : ofNull)}");
        }

        /// <summary>箱子的格位顺序有意义(第 1 格和第 3 格不是一回事)。</summary>
        private static void ContainerSignatureRespectsSlotOrder()
        {
            var a = new ContainerState { Slots = new List<SlotItem>
            {
                new SlotItem { ItemName = "wood", Count = 1 },
                new SlotItem { ItemName = "stone", Count = 2 },
            }};
            var b = new ContainerState { Slots = new List<SlotItem>
            {
                new SlotItem { ItemName = "stone", Count = 2 },
                new SlotItem { ItemName = "wood", Count = 1 },
            }};
            Check("箱子指纹区分格位顺序", SyncMath.ContainerSignature(a) != SyncMath.ContainerSignature(b));
        }

        private static void MissionSignatureIsOrderIndependent()
        {
            var a = new List<string> { "m2", "m1" };
            var b = new List<string> { "m1", "m2" };
            Check("任务指纹顺序无关", SyncMath.MissionSignature(a) == SyncMath.MissionSignature(b));
        }

        private static void ReconcileFindsAddAndRemove()
        {
            var local = new List<DropEntry>
            {
                new DropEntry { ItemName = "wood", X = 1, Y = 1 },     // 主机没有 → 该删(被人捡了)
                new DropEntry { ItemName = "stone", X = 2, Y = 2 },    // 两边都有 → 不动
            };
            var remote = new List<DropEntry>
            {
                new DropEntry { ItemName = "stone", X = 2, Y = 2 },
                new DropEntry { ItemName = "fish", X = 9, Y = 9 },     // 本地没有 → 该补(别人打掉的)
            };
            var r = SyncMath.ReconcileDrops(local, remote);
            Check("对账找出增删", r.RemoveKeys.Count == 1 && r.RemoveKeys[0].StartsWith("wood@")
                  && r.Add.Count == 1 && r.Add[0].ItemName == "fish",
                  $"删{r.RemoveKeys.Count} 增{r.Add.Count}");
        }

        private static void ReconcileIdenticalIsNoop()
        {
            var list = new List<DropEntry> { new DropEntry { ItemName = "wood", X = 1, Y = 1 } };
            var r = SyncMath.ReconcileDrops(list, list);
            Check("两边一致时不动", r.RemoveKeys.Count == 0 && r.Add.Count == 0);
        }

        private static void FindVanishedDetectsPickup()
        {
            var known = new Dictionary<string, DropEntry>
            {
                [SyncMath.DropKey("wood", 1, 1)] = new DropEntry { ItemName = "wood", X = 1, Y = 1 },
                [SyncMath.DropKey("stone", 2, 2)] = new DropEntry { ItemName = "stone", X = 2, Y = 2 },
            };
            var current = new List<DropEntry> { new DropEntry { ItemName = "stone", X = 2, Y = 2 } };
            var gone = SyncMath.FindVanished(known, current);
            Check("发现本地消失的物品", gone.Count == 1 && gone[0].ItemName == "wood", $"消失 {gone.Count} 个");
        }

        /// <summary>没记录过的东西消失了不该上报 —— 避免刚进房时误报一堆。</summary>
        private static void FindVanishedIgnoresUnknown()
        {
            var gone = SyncMath.FindVanished(new Dictionary<string, DropEntry>(),
                                             new List<DropEntry>());
            Check("已知为空时不误报", gone.Count == 0, $"报了 {gone.Count} 个");
        }

        // ---------------- 测试脚手架 ----------------

        private static (CoopSession, FakeTransport, List<string>) NewPair()
        {
            var transport = new FakeTransport();
            var session = new CoopSession(transport, "test");
            return (session, transport, new List<string>());
        }

        // ---------------- 房间 Mod 清单 ----------------

        private static ModEntry Mod(string id, string ver = "1.0.0", ulong workshop = 0, string title = null)
            => new ModEntry { Id = id, Title = title ?? id, Version = ver, WorkshopId = workshop };

        private static void ModListRoundTrip()
        {
            var src = new List<ModEntry>
            {
                Mod("lan-character", "0.1.0", 3712345678UL, "小澜 Lan"),
                Mod("red-beret", "", 0UL),
            };
            List<ModEntry> back;
            using (var ms = new System.IO.MemoryStream())
            {
                using (var bw = new System.IO.BinaryWriter(ms, System.Text.Encoding.UTF8, true))
                    RoomMods.Write(bw, src);
                ms.Position = 0;
                using (var br = new System.IO.BinaryReader(ms))
                    back = RoomMods.Read(br);
            }
            Check("清单编解码往返", back.Count == 2
                && back[0].Id == "lan-character" && back[0].Title == "小澜 Lan"
                && back[0].Version == "0.1.0" && back[0].WorkshopId == 3712345678UL
                && back[1].Version == "" && back[1].WorkshopId == 0UL,
                $"实得 {back.Count} 项");
        }

        private static void AcceptsWhenClientHasAllHostMods()
        {
            var host = new List<ModEntry> { Mod("a"), Mod("b") };
            var client = new List<ModEntry> { Mod("b"), Mod("a") };   // 顺序不同也该放行
            Check("客机齐备则放行", RoomMods.Validate(host, client) == null);
        }

        private static void AcceptsClientExtraMods()
        {
            // 规矩是房主定的:客机自己多装的不影响房主看到的世界,不该拦
            var host = new List<ModEntry> { Mod("a") };
            var client = new List<ModEntry> { Mod("a"), Mod("extra") };
            Check("客机多装不拦", RoomMods.Validate(host, client) == null);
        }

        private static void RejectsMissingMod()
        {
            var host = new List<ModEntry> { Mod("a"), Mod("lan-character", "0.1.0", 0, "小澜 Lan") };
            var client = new List<ModEntry> { Mod("a") };
            string why = RoomMods.Validate(host, client);
            Check("缺 Mod 被拒且点名", why != null && why.Contains("小澜 Lan"), why ?? "(放行了)");
        }

        private static void RejectsVersionMismatch()
        {
            var host = new List<ModEntry> { Mod("a", "2.0.0") };
            var client = new List<ModEntry> { Mod("a", "1.0.0") };
            string why = RoomMods.Validate(host, client);
            Check("版本不同被拒", why != null && why.Contains("2.0.0") && why.Contains("1.0.0"), why ?? "(放行了)");
        }

        private static void TolerantWhenEitherVersionBlank()
        {
            // Mod 没填 version 时硬卡只会把人挡在门外却给不出解决办法
            Check("一方没写版本号则不卡",
                RoomMods.Validate(new List<ModEntry> { Mod("a", "") },
                                  new List<ModEntry> { Mod("a", "1.0.0") }) == null
                && RoomMods.Validate(new List<ModEntry> { Mod("a", "1.0.0") },
                                     new List<ModEntry> { Mod("a", "") }) == null);
        }

        private static void EmptyHostListAcceptsAnything()
        {
            Check("房主没开 Mod 则谁都能进",
                RoomMods.Validate(new List<ModEntry>(), new List<ModEntry> { Mod("x") }) == null
                && RoomMods.Validate(null, null) == null);
        }

        private static void RejectReasonNamesWorkshopId()
        {
            // "方便查找"是这个功能的重点:得让人知道去哪订阅
            var host = new List<ModEntry> { Mod("a", "1.0.0", 998877UL, "某 Mod") };
            string why = RoomMods.Validate(host, new List<ModEntry>());
            Check("拒绝理由带创意工坊 id", why != null && why.Contains("998877"), why ?? "(放行了)");
            Check("能拼出订阅地址",
                RoomMods.WorkshopUrl(998877UL) == "https://steamcommunity.com/sharedfiles/filedetails/?id=998877"
                && RoomMods.WorkshopUrl(0UL) == null);
        }

        private static void RejectReasonTruncatesLongList()
        {
            var host = new List<ModEntry>();
            for (int i = 0; i < 20; i++) host.Add(Mod("m" + i));
            string why = RoomMods.Validate(host, new List<ModEntry>());
            int lines = why.Split('\n').Length;
            Check("清单过长时截断", why.Contains("还有") && lines < 12, $"{lines} 行");
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
