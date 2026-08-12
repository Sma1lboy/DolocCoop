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
        public const ushort Version = 5;
    }

    public enum MsgType : byte
    {
        // 会话
        Hello = 1,           // 客机→主机: mod 版本/协议版本/昵称
        HelloAck = 2,        // 主机→客机: 接受/拒绝(版本不符)
        Disconnect = 3,

        // v0: 玩家同步
        PlayerState = 10,    // 不可靠,高频: 位置/朝向/动画状态
        PlayerProfile = 11,  // 可靠,低频: 外观/帽子/名字
        Chat = 12,
        PlayerAction = 13,   // 双向: 玩家动作状态变化(砍树/浇水/钓鱼…),只在切换时发

        WorldSync = 20,      // 主机→客机: 游戏内绝对时间 + 各区域天气
        ContainerSync = 21,  // 主机→客机: 若干箱子的整箱内容
        MissionSync = 22,    // 主机→客机: 已完成任务 id 列表
        // 预留
        SceneChange = 21,
        SleepVote = 22,
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
