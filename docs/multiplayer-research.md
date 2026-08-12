# Doloc Town 联机 Mod 调研与技术路线(2026-08-12)

目标:给多洛可小镇做基于 Steam 的联机 Mod,参考鸭科夫(Escape From Duckov)COOP Mod 的成熟实现。

## 参考一:鸭科夫 COOP Mod(最重要的参考,已克隆源码)

- 工坊页:https://steamcommunity.com/sharedfiles/filedetails/?id=3591341282
  (Mr.sans 团队,**51.5 万订阅**,v1.3.2)
- **开源**:https://github.com/Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview
  → 已克隆到本工作区 `references\duckov-coop-mod\`
- 许可:**Modified AGPL-3.0 + 附加限制**(见 LICENSE_RESTRICTIONS.txt——借鉴架构没问题,直接搬代码要看条款)

### 技术栈

| 层 | 选型 |
|----|------|
| 传输 | **LiteNetLib**(UDP,独立工坊依赖项)+ Steam P2P(好友邀请/房间)+ 直连 IP(远程用 VPN/内网穿透,国内常配加速器) |
| 补丁 | **HarmonyLib**(独立工坊依赖项) |
| 前置 | 控制台 Mod(调试用) |
| 架构 | **主机权威**(Host-authoritative):AI 生成/结算全在房主,客机发输入和事件 |

### 源码结构(值得照抄的组织方式)

```
EscapeFromDuckovCoopMod\
├── Main\            HostService / ClientService / LocalPlayer / Player /
│                    SceneService / Item / Weapon / Health / WeatherAndTime / UI / Loader
├── Net\             NetPack(消息包)/ Rpc / Steam(Steam 层)
├── NetTag\          网络对象标记
├── Patch\           Harmony 补丁按域分目录:Character / Input / Item / Loot / Projectile ...
├── SyncData\        同步数据结构
└── EscapeFromDuckovModApi\  对外 Mod API
```

### 从它的迭代史学到的坑(change notes 精华)

- 同步优先级:玩家移动/外观 → 场景状态(门、解锁、付费点)→ AI → 载具 → 音效
- 客机重连会产生重复实例;版本校验要做进 Steam 房间 tag
- 多人同时打同一 AI 的伤害合并、掉落容器"秒开"竞态、耐久度双扣
- 投票机制处理场景切换(谁发起、阻断重复投票)
- 聊天(Enter)、好友伤害开关、AI 倍率设置是标配 QoL

## 参考二:Doloc Town 已有「联机mod」(XvX,3744793227)

- 0.7.x 极早期测试版,146 订阅,更新到 8/10。QQ 群 416666074。
- 也是 Steam 大厅方案:主界面创建大厅 → Steam 好友邀请/好友列表加入 → 客机先进存档、房主后进并**把自己的建筑同步覆盖给所有客机**(客机存档建筑被拆平,破坏性极强,作者反复强调备份)。
- 安装方式原始:从工坊目录解压 zip 到游戏根目录(工坊不允许直接投放游戏根目录文件)。
- 已知未同步:动物 AI、自动化系统、树/杂草;断线需全员重启。
- 结论:证明了 Doloc + Steam 大厅联机可行,但架构简单粗暴,有大量空间做得更好;也是潜在合作对象。

## Doloc Town 联机的有利条件

- 游戏自带 `com.rlabrecque.steamworks.net.dll`(Steamworks.NET)→ SteamMatchmaking 大厅 +
  SteamNetworkingSockets/P2P 不需要额外原生依赖。
- Unity 2021.3 Mono → Harmony 补丁和反编译(Assembly-CSharp.dll ~6.4MB)都顺畅。
- 农场模拟的同步压力远低于射击游戏:无弹道/命中竞态,tick 级同步够用;
  难点转移到**时间流逝、存档所有权、自动化设备结算**的一致性。

## 建议架构(v0 目标:双人同存档共玩)

1. **前置**:BepInEx 5.4.23(DTMAPI 已装同款)+ HarmonyLib;发布时可参考木子的
   Doloc Auto Mod Loader(3780218737)解决"工坊不能投放 DLL 到游戏目录"的分发问题。
2. **传输**:直接用游戏内置 Steamworks.NET:SteamMatchmaking 创建 lobby + 好友邀请,
   SteamNetworkingMessages 收发(可靠+不可靠双通道)。不引入 LiteNetLib(鸭科夫需要它是因为要支持局域网直连,我们先 Steam-only,少一个依赖)。
3. **主机权威**:房主存档为准;客机进入时接收全量快照(建筑/设备/时间/天气),之后增量同步。
   ⚠ 吸取 XvX 教训:客机本地存档**只读**,退出不回写,从根上避免拆家惨案。
4. **同步域拆分**(照抄鸭科夫的 Patch 分域):
   - P0:玩家位置/动画/外观(含帽子 Mod 贴图 id 广播)
   - P1:时间/天气/睡觉跳日(需要投票)
   - P2:物品交互(捡拾/放置/商店)与库存独立
   - P3:农场设备状态(种植/浇水/机器进度,结算只在主机跑)
   - P4:NPC/动物(最后做,鸭科夫和 XvX 都栽在 AI 同步上)
5. **DTMAPI 的角色**:不承担网络(它没有网络 API),但作为 Mod 生命周期/配置菜单/日志设施,
   并保持与其生态兼容(注册成 DTMAPI mod,用 IGameLoopEvents.UpdateTicked 驱动网络泵)。
   API 参考:`docs\dtmapi-api.md`(270 个公开类型,反射生成)。

## 下一步

1. 等游戏关闭后运行 DTMAPI 安装器(当前被"游戏运行中"保护挡住)。
2. 反编译 Assembly-CSharp.dll,摸清:玩家控制器/移动、时间系统(TimeManager?)、
   建筑/设备数据结构、存档序列化(ea-doloc-archive-*.data)。
3. 读 `references\duckov-coop-mod` 的 Net\Steam 与 HostService/ClientService,提炼可复用的消息协议设计。
4. 起项目 `mods-code\doloc-coop\`(C# net472 类库,引用 BepInEx + 游戏 Managed DLL),
   v0 里程碑:两人进同一存档,互相看见对方走动。
