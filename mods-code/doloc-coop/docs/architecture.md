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

## 各类同步的接口摸底(2026-08-12)

反编译确认的入口,供后续实现参考:

| 同步项 | 读 | 写 | 状态 |
|--------|-----|-----|------|
| 时间 | `archiveHandle.timeData.totalSeconds` | `PassTimeNoControl` / `TrackBackTime` | ✅ 已实现并实测 |
| 天气 | `timeData.climateManager.GetCurrentWeather(区域id)`,区域从 `weatherSystems` 枚举 | `archiveHandle.SetWeather(区域id, WeatherType, shouldRender)` | ✅ 已实现并实测(default/wetland/ruined_city 三区) |
| 箱子 | `IContainer.inventory.ForEach` | `IContainer.OverwriteInventory(...)` | 🟡 已实现,**未端到端验证**(见下) |
| 行为 | `BodyController.StateManager.current`(AgentState*) | 广播状态名,切换时才发 | ✅ 已实现并实测(`ACTION_SEND state=AgentStateIdle`) |
| 任务 | `farmData.missionManager.FinishMissions` | `CompleteMission(id)`(需先 `IsMissionListening`) | ✅ 已实现并实测(`MISSION_SEND count=13`) |

### 箱子同步的设计要点(待实现)

- 容器要有稳定标识:同一个箱子在两端必须能对上。候选是"房间 id + 网格坐标"。
- 主机权威:客机开箱只发意图,主机结算后广播整箱内容(`OverwriteInventory`)。
  整箱覆盖比做增量简单得多,而箱子容量有限,带宽可接受。
- 冲突场景:两人同时拿同一格。主机串行处理请求即可自然解决。

### 箱子同步的验证进度(2026-08-12)

**已验证**:消息编解码、事件接线、id 匹配、未命中时优雅跳过。
方法是让模拟客机当主机发一个假箱子(`SIM_FAKE_BOX`),游戏客机侧日志:
```
CONTAINER_RECV got=1 matched=0 skipped=1 场景容器=0
```

**仍未验证**:真正的 `OverwriteInventory` 写入路径。
两个存档都是开局阶段,场景里一个容器都没有(`CONTAINER_SCAN found=0`);
扫描范围已放宽到所有 `IContainer`(储物箱/祭坛/宝箱 + 鱼缸/孵化器/粉碎机等
农场设备)仍是 0 —— 是存档里确实没有,不是扫描坏了。

**注意**:用 `room.CreateEquipment` 程序化放一个箱子技术上可行,
但那会改动玩家的真实存档,所以没有这么做。

**要补上这条验证,需要在游戏里手动放一个储物箱**(或任意带存储的设备),然后:
```powershell
.\dev.ps1 -NoLaunch
Set-Content "...\DolocCoop-debug\autotest.flag" "<存档位>" -NoNewline
Start-Process "steam://rungameid/2285550"
.\sim.ps1     # 看模拟客机是否打印 [箱子] 收到 N 个箱子
```
预期日志:`CONTAINER_SCAN found>0` → `CONTAINER_SEND` → 客机 `[箱子]`。

### 任务同步只做单向"完成"

不做回退。任务完成往往伴随奖励发放、剧情推进、解锁标记,这些没有对应的逆操作,
强行回滚只会把存档搞坏。客机比主机多完成的任务就留着 —— 顶多是它提前做了。

### 行为同步当前只到"看得见对方在干什么"

位置和动画哈希跟着 15Hz 状态包走;动作状态名单独在**切换时**发(可靠通道),
因为动画哈希只在同一套 AnimatorController 下有意义,而状态名是稳定的语义标识,
将来按动作触发音效/特效/世界结算靠的是它。

动作的**世界后果**(树被砍倒、作物被浇)属于主机权威结算,依赖交互拦截,是下一步。

