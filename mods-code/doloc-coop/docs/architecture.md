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
| 掉落物 | `room.DM_dropitem.AllDatas` | `CreateDropItem` / `RemoveDropItem` | ✅ 已实现并实测(`DROP_SEND count=0`) |

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


### 掉落物同步为什么用全量对账

一个人把地上的东西捡了、另一个人还看得见并且能再捡一次 —— 直接变成刷物品,
是两人同场景时最刺眼的不同步。

用全量而不是增量事件:增量一旦丢包或乱序,地上就会留下永远捡不掉的幽灵物品。
掉落物通常只有几个到几十个,整包对账的代价完全可以接受。

标识用「物品名@量化坐标」(0.5 格):掉落物没有持久 id,而同一物品不会精确
重叠在同一格。量化精度太细会因浮点抖动误判成"不同的物品"。

### 三个反复踩到的坑(已在代码里加注释固化)

1. **指纹初值不能用空串**:"地上没东西"算出来的指纹也是空串,
   两者相等就永远不广播,客机的残留物品永远得不到清理。改用 null 当哨兵。
2. **枚举预留值撞车**:`SceneChange = 21` 和 `ContainerSync = 21` 重复,
   C# 会静默变成别名 —— 发 SceneChange 实际发出去的是 ContainerSync,极难排查。
3. **日志采样吃掉首次事件**:纯取模采样下,只发生一次的事件永远达不到阈值,
   日志一片空白,看起来就像功能没跑。这个坑连踩三次(WorldSync/ActionSync/DropItemSync),
   最后改成 `NetLog.Sample` **首次必记、之后才采样**。

### 捡拾意图上报(2026-08-12,协议 v7)

**这是补一个我自己引入的刷物品漏洞**:客机把地上的东西捡走后,主机并不知情,
下一轮全量广播又会把它重新生成 —— 客机等于白得一份。

设计:背包是各人各自的,不需要同步;但地上的物件属于共享世界。
客机检测到"某个掉落物从本地消失了"就上报主机,主机从世界里移除。

**用差分检测而不是 Harmony 拦截拾取**:游戏里能让物件消失的路径不止一条
(走过自动吸附、工具收集、掉落物过期…),逐个打补丁既容易漏又容易被版本更新击穿。
盯住"结果"比盯住"每一种原因"稳得多。

一个必须注意的回环:应用主机广播删掉物品后,**必须同步更新"已知地面"**,
否则捡拾检测会把这些删除误判成本地玩家捡的,反过来上报主机,两边互相纠正停不下来。

两人同时捡同一个物件时,主机侧先到先得,后到的记 `DROP_PICKUP_MISS` 静默忽略。

**验证状态**:主机侧接收与处理路径已验证(模拟客机发假上报 →
`DROP_PICKUP_MISS from=52576 请求=1`);客机侧的差分检测因存档地上没有掉落物,
尚未实际触发过。

### 客机存档保护(2026-08-12,已端到端验证)

**这是整个 Mod 最重要的安全措施。** 社区里已有的联机 mod(XvX 那个)让客机
进房时被主机世界覆盖,把人家自己存档里的建筑全拆平了 —— 从架构第一版起
我们就承诺"客机存档只读、退出不回写",现在落实了。

实现:Harmony 前缀拦 `DataPersistenceManager.SaveGame(int)`。
所有保存路径(睡觉、退出、手动存、控制台命令)都收口到这一个方法,拦这里就够。

**返回 true 而不是 false**:返回 false 会触发游戏的"存档失败"错误提示,
但我们是**有意**不写盘,不该表现成故障。

验证方式:自测流程在客机身份下主动调 `DolocAPI.SaveGame`,同时比对存档文件哈希:
```
SAVEGUARD client=True
SAVEGUARD_BLOCK count=1
AUTOTEST 存档保护验证通过:尝试存盘被拦截(1 次)
存档哈希 未变 ✔
```

### 断线检测(2026-08-12,已端到端验证)

一方掉线后,另一方的化身会永远僵在原地 —— 回环传输没有底层断线感知,
Steam 的 P2P 回调也不保证一定触发,所以**超时判定是唯一可靠的兜底**。

做法:每 2 秒发一个空心跳包,收到任何消息都刷新对方的"最后出现时间",
超过 10 秒没消息就判掉线,移除 peer 并触发 PeerLeft(化身随之销毁)。

**为什么要单独的心跳,不能只看位置包**:玩家在标题界面、加载中、
或者干脆没进存档时根本不发位置,只看位置包会把他们全判成掉线。
心跳与游戏状态无关,永远在跳。

实测:强杀模拟客机进程后
```
SESSION peer 62763 超过 10 秒无消息,判定掉线
PEER_LEFT id=62763
```

### Steam 分支代码走查(2026-08-12)

Steam 那条路径没法本地端到端测,只能靠走查。查出四个**只在真实联机时才会炸**的问题:

1. **P2P 握手竞态(最严重)**:原来只接受"本地 _peers 里已登记"的会话请求。
   但对方进大厅后可能立刻发包,他的会话请求会**早于**我们处理大厅成员列表到达,
   这时 _peers 还是空的 → 拒绝 → 而 Steam 不会重试,连接永远建不起来。
   改成直接问 Steam "他是不是我大厅的成员",并顺手补登记。

2. **收包缓冲区固定 16KB,超了静默丢弃**:箱子同步一次可发 16 个箱子、
   每箱几十格带物品名,大基地完全可能超过。丢了可靠传输也救不回来 ——
   那是我们自己扔的,Steam 认为已送达。改成按需扩容。

3. **不可靠通道超 MTU 会被 Steam 直接丢**:加了 1100 字节保护,
   超限自动降级为可靠发送并报警(正常情况不会触发,触发说明协议改坏了)。

4. **发送失败静默**:SendP2PPacket 的返回值原来没看。
   静默丢包会让"对方状态卡住"变成无从下手的怪现象,现在会记日志。

另外 Broadcast 改成倒序遍历副本 —— 发送过程中回调可能改动 _peers(有人进/退大厅)。
