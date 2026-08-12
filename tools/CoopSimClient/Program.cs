using System;
using System.Threading;
using CoopCore;

namespace CoopSimClient
{
    /// <summary>
    /// 模拟客机 —— 假扮第二个玩家连进游戏,用来在**单机**上测试联机同步。
    ///
    /// 为什么需要它:多洛可小镇有单实例限制,同一台机器开不了第二个游戏,
    /// 而 CoopCore 是零游戏依赖的,所以可以在控制台里跑一个"玩家"。
    ///
    /// 用法:
    ///   1. 游戏内按 F6 (回环主机)
    ///   2. 运行本程序
    ///   3. 游戏里应出现一个绕着你转圈的化身
    ///
    /// 参数: --name 显示名   --radius 绕圈半径   --speed 角速度(弧度/秒)
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            string name = Arg(args, "--name", "模拟客机");
            float radius = float.Parse(Arg(args, "--radius", "3"));
            float speed = float.Parse(Arg(args, "--speed", "1.2"));
            string host = Arg(args, "--host", "127.0.0.1");
            int port = int.Parse(Arg(args, "--port", LoopbackTransport.DefaultPort.ToString()));

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== DolocCoop 模拟客机 ===");
            Console.WriteLine($"目标 {host}:{port}   名字 {name}   绕圈半径 {radius}   角速度 {speed}");
            Console.WriteLine("确认游戏内已按 F6 开启回环主机,然后等待握手…");
            Console.WriteLine("(Ctrl+C 退出)");
            Console.WriteLine();

            bool asHost = Array.IndexOf(args, "--host-mode") >= 0;
            int fakeTime = int.Parse(Arg(args, "--time", "0"));

            var transport = new LoopbackTransport(s => Console.WriteLine("[Net] " + s));
            var session = new CoopSession(transport, "sim-0.1");

            session.ContainersReceived += list =>
            {
                Console.WriteLine($"[箱子] 收到 {list.Count} 个箱子");
                foreach (var c in list)
                {
                    int nonEmpty = c.Slots.FindAll(s => !string.IsNullOrEmpty(s.ItemName)).Count;
                    Console.WriteLine($"    {c.Id}  共 {c.Slots.Count} 格,有物品 {nonEmpty} 格");
                }
            };

            session.WorldSyncReceived += (t, weather) =>
                Console.WriteLine($"[世界] 主机时间 {t} 秒 (第 {t / 86400 + 1} 天 {(t % 86400) / 3600:00}:{(t % 3600) / 60:00})，天气区域 {weather.Count} 个: {string.Join(", ", weather.ConvertAll(w => w.RegionId + "=" + w.WeatherType))}");

            // 跟随主机位置:收到对方状态后绕着他转,这样化身一定出现在画面里
            float hostX = 0f, hostY = 0f;
            bool sawHost = false;

            session.Log += s => Console.WriteLine("[Session] " + s);
            session.PeerJoined += p => Console.WriteLine($"[+] 玩家加入: {p.Id}");
            // 事件注册必须在主循环之外 —— 放循环里会每轮重复挂一个,回调被调用几十次
            session.DropPickupReceived += (from, picked) =>
                Console.WriteLine($"[捡拾] 客机 {from} 报告捡走 {picked.Count} 个: {string.Join(", ", picked.ConvertAll(d => d.ItemName))}");
            session.PeerLeft += p => Console.WriteLine($"[-] 玩家离开: {p.Id}");
            session.PeerStateUpdated += p =>
            {
                hostX = p.X; hostY = p.Y;
                if (!sawHost)
                {
                    sawHost = true;
                    Console.WriteLine($"[✔] 已收到主机玩家状态,位置 ({p.X:F2}, {p.Y:F2}) —— 双向通信正常");
                }
            };

            if (asHost)
            {
                transport.StartHost(port);
                Console.WriteLine($"[模式] 主机 —— 游戏应按 Ctrl+F6 接入。将广播假时间 {fakeTime} 秒。");
            }
            else
            {
                transport.Connect(host, port);
                Console.WriteLine("[模式] 客机 —— 游戏应已按 F6 开主机。");
            }
            session.SendProfile(name, "");

            float angle = 0f;
            var last = DateTime.UtcNow;
            int tick = 0;

            while (true)
            {
                var now = DateTime.UtcNow;
                float dt = (float)(now - last).TotalSeconds;
                last = now;

                session.Pump();

                angle += speed * dt;
                float x = hostX + radius * (float)Math.Cos(angle);
                float y = hostY + radius * (float)Math.Sin(angle) * 0.35f;   // 压扁一点,像在地面走
                bool faceLeft = Math.Sin(angle) < 0;

                // 动画哈希传 0 = 让对端用默认状态;真实同步时这里是游戏的 Animator 状态哈希
                session.SendLocalState(x, y, faceLeft, 0, 0f);

                // 客机模式:定期上报一个假捡拾,验证主机侧的接收与处理路径
                // (地上没有对应物品时应记 DROP_PICKUP_MISS 并优雅忽略)
                if (!asHost && tick % 75 == 0 && session.Peers.Count > 0)
                {
                    session.SendDropPickup(new System.Collections.Generic.List<DropEntry>
                    {
                        new DropEntry { ItemName = "sim_fake_drop", X = 10f, Y = 20f }
                    });
                    Console.WriteLine("    已上报一个假捡拾 sim_fake_drop@(10,20)");
                }

                // 主机模式:定期广播一个"假时间",用来验证客机(游戏)会不会跟着校时
                if (asHost && tick % 30 == 0 && session.Peers.Count > 0)
                {
                    session.SendWorldSync(fakeTime, new System.Collections.Generic.List<CoopCore.WeatherEntry>());
                    // 发一个假箱子,验证消息编解码与客机侧接收路径(场景里没有对应容器时应优雅跳过)
                    session.SendContainers(new System.Collections.Generic.List<ContainerState>
                    {
                        new ContainerState
                        {
                            Id = "SIM_FAKE_BOX",
                            Slots = new System.Collections.Generic.List<SlotItem>
                            {
                                new SlotItem { ItemName = "wood", Count = 7 },
                                new SlotItem { ItemName = "", Count = 0 },
                                new SlotItem { ItemName = "stone", Count = 3 },
                            }
                        }
                    });
                    if (tick % 150 == 0) Console.WriteLine($"    广播时间 {fakeTime} 秒");
                }

                if (++tick % 150 == 0)
                    Console.WriteLine($"    发送中… 位置 ({x:F2}, {y:F2})  已知主机 ({hostX:F2}, {hostY:F2})  对端数 {session.Peers.Count}");

                Thread.Sleep(1000 / 15);   // 15 Hz,和游戏侧一致
            }
        }

        private static string Arg(string[] args, string key, string fallback)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return fallback;
        }
    }
}




