# DolocCoop 架构设计 v0

目标:多洛可小镇 Steam 联机 Mod。设计完全借鉴鸭科夫 COOP Mod
(`references\duckov-coop-mod`),但架构上做一个关键升级:**核心与游戏解耦**,
为"给不同的单机游戏提供联机能力"留出复用空间。

## 分层

```
┌─────────────────────────────────────────────┐
│ DolocCoop(BepInEx 插件,游戏相关)          │
│  Plugin 入口 / Harmony 补丁(按域分目录)    │
│  GameBridge: 读写 DolocTown 的玩家/时间/存档 │
│  SteamTransport: Steamworks.NET 大厅+消息    │
├─────────────────────────────────────────────┤
│ CoopCore(纯 C#,零游戏依赖,可跨游戏复用)  │
│  消息协议(帧头/序列化/可靠与不可靠通道语义)│
│  Snapshot/Delta 同步框架、Tick 调度、插值    │
│  Host/Client 会话状态机、版本校验            │
└─────────────────────────────────────────────┘
```

鸭科夫对应关系:他们的 `Net\NetPack + Rpc` ≈ 我们的 CoopCore;
`Net\Steam`(SteamLobbyManager/SteamP2PManager/SteamEndPointMapper)≈ SteamTransport;
`Patch\*` + `Main\HostService/ClientService` ≈ DolocCoop 的补丁与桥接层。

## 关键决策

1. **主机权威**:房主的存档与模拟为准。客机本地存档只读、退出不回写
   (吸取 XvX 联机 mod"客机拆家"教训)。
2. **传输只走 Steam**:游戏自带 `com.rlabrecque.steamworks.net.dll`,
   SteamMatchmaking 建大厅 + 好友邀请,SteamNetworkingMessages 双通道
   (可靠:事件/交互;不可靠:位置/动画)。不引入 LiteNetLib,少一个分发依赖;
   CoopCore 的 ITransport 抽象保留换传输层的口子。
3. **利用游戏现成的存档结构做快照**:`DolocTown.GameData.FarmArchiveData /
   TimeArchiveData / CityArchiveData …` 已把世界状态模块化,主机入场全量快照
   = 序列化这些对象;后续增量走事件。
4. **DTMAPI 共存不依赖**:网络泵挂在自己的 MonoBehaviour/Harmony 上,
   不吃 DTMAPI 的 UpdateTicked,避免强绑;但保持兼容(同 BepInEx 5 生态)。

## 同步域优先级(v0 → v3)

| 阶段 | 内容 | 备注 |
|------|------|------|
| **v0 幽灵同行** | 大厅/邀请/加入 + 玩家位置/朝向/动画/外观(帽子!)广播 + 聊天 | 互相可见即里程碑 |
| v1 世界一致 | 时间/天气/日期(睡觉跳日投票)、场景切换同步 | 鸭科夫的投票机制 |
| v2 交互 | 拾取/放置/砍树采集(主机结算)、商店、建筑增删 | 库存各自独立 |
| v3 农场 | 设备/机器进度(仅主机跑模拟,客机收状态)、作物 | 自动化是难点 |
| v4+ | NPC/动物同步 | 鸭科夫和 XvX 都栽在这,最后做 |

## 反编译摸底记录(Assembly-CSharp,2717 类型可反射)

- 玩家:`DolocTown.BodyController`(含 PlayerAudioType 嵌套枚举)→ 待确认移动/动画字段
- 时间:`DolocTown._TimeInvoker`、`CalendarManager`、`WeatherAffectorManager`、`WeatherHistory`
- 存档:`DolocTown.GameData.LocalSave`、`ArchiveDataHandle`、`ArchiveOperation*`(City/Dungeon/Email/Farm/Mission)
- 控制台:`RedSaw.CommandLineInterface.*`(游戏内置 CLI,可挂调试命令)
- 待办:游戏关闭后用 ILSpy 全量反编译,确认玩家 Transform 访问路径与场景管理器

## 风险与对策

- 游戏更新击穿补丁 → Harmony 补丁集中列清单,启动时逐个 self-check(学 DTMAPI 的 Hook 状态页)
- 版本不匹配联机 → 大厅 metadata 写 mod 版本 + 游戏 build 号,进房校验(鸭科夫 v1.3 的做法)
- 工坊不能投放 DLL 到游戏目录 → 发布期用 Doloc Auto Mod Loader(3780218737)或自带安装 bat(学 DTMAPI)
