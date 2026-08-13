using System;
using System.IO;

namespace CoopCore
{
    /// <summary>
    /// 消息类型注册表。协议版本随任何消息结构变更而 +1,
    /// 进房时校验(参考鸭科夫把版本写进 Steam 大厅 tag 的做法)。
    /// </summary>
    public static class Protocol
    {
        // v2: TimeSync(主机权威时间)
        // v3: 扩展为 WorldSync,时间 + 各区域天气一起下发
        // v4: 新增 ContainerSync(箱子内容)
        // v5: 新增 PlayerAction(行为)与 MissionSync(任务)
        // v6: 新增 DropItemSync(地上掉落物)
        // v7: 新增 DropPickup(客机捡拾上报)—— 没有它会刷物品,见 DropItemSync 注释
        // v8: Hello 增加游戏版本 + 房间 Mod 清单。
        //     其中游戏版本是**补记**:它其实在上一次改动里就加进 Hello 了,
        //     但当时忘了 +1,于是两个结构不同的 Hello 都自称 v7 —— 校验在说谎。
        //     靠 SafeReadString 才没崩,这次一并修正。
        // v9: 清单条目补 Priority 与三个"是否替换主角外观"标志,
        //     接收方靠它认出对方在用哪套皮肤。改了线格式就必须 +1 —— 见上面 v8 的教训。
        public const ushort Version = 9;
    }

    public enum MsgType : byte
    {
        // 会话
        Hello = 1,           // 客机→主机: mod 版本/协议版本/昵称
        HelloAck = 2,        // 主机→客机: 接受/拒绝(版本不符)
        Disconnect = 3,
        Heartbeat = 4,       // 双向: 空包,只为证明"我还在"

        // v0: 玩家同步
        PlayerState = 10,    // 不可靠,高频: 位置/朝向/动画状态
        PlayerProfile = 11,  // 可靠,低频: 外观/帽子/名字
        Chat = 12,
        PlayerAction = 13,   // 双向: 玩家动作状态变化(砍树/浇水/钓鱼…),只在切换时发

        WorldSync = 20,      // 主机→客机: 游戏内绝对时间 + 各区域天气
        ContainerSync = 21,  // 主机→客机: 若干箱子的整箱内容
        MissionSync = 22,    // 主机→客机: 已完成任务 id 列表
        DropItemSync = 23,   // 主机→客机: 当前房间掉落物全量列表
        DropPickup = 24,     // 客机→主机: 我捡走了这个掉落物,请从世界里移除
        CropSync = 25,       // 主机→客机: 作物长势(旧,迁移中)
        DomainSync = 26,     // 主机→客机: 通用同步域负载 [channel:1][payload]

        // 预留(注意别和上面的值撞:C# 允许重复值,会静默变成别名,
        // 到时候发 SceneChange 实际发出去的是 ContainerSync,极难排查)
        SceneChange = 30,
        SleepVote = 31,
    }

    /// <summary>简单二进制消息帧: [MsgType:1][payload...]。</summary>
    public static class MsgWriter
    {
        public static byte[] Frame(MsgType type, Action<BinaryWriter> writePayload)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write((byte)type);
                writePayload?.Invoke(bw);
                return ms.ToArray();
            }
        }

        public static MsgType ReadType(byte[] data) => (MsgType)data[0];

        public static BinaryReader Payload(byte[] data, int length)
        {
            return new BinaryReader(new MemoryStream(data, 1, length - 1, false));
        }
    }
}




