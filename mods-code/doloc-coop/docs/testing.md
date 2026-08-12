# DolocCoop 测试策略

## 重要前提:游戏只能单开

实测确认(2026-08-12):直接启动第二个 `DolocTown.exe`,进程会在约 10 秒后
以 ExitCode=0 自行退出 —— 游戏有单实例检测。**同一台机器开不出第二个游戏实例**,
所以"双开对联"这条路走不通。

替代方案见下。

## L1:模拟客机(日常开发主力,单机即可)

用 `tools/CoopSimClient` —— 一个控制台程序,借助 `CoopCore`(零游戏依赖)
假扮第二个玩家接入。它会绕着你的角色转圈,用来验证:
握手、位置同步、化身渲染、成员列表。

```powershell
# 终端 1
.\dev.ps1                 # 编译部署并启动游戏

# 游戏内:进存档 → 按 F6(回环主机)

# 终端 2
.\sim.ps1                 # 模拟客机接入
.\sim.ps1 -Name 小明 -Radius 5 -Speed 2   # 自定义
```

预期现象:
- 控制台出现 `[✔] 已收到主机玩家状态` → **双向通信正常**
- 游戏里出现一个绕着你转圈的化身,头顶显示名字
- 联机大厅面板的成员列表里出现这个玩家,坐标实时变化

这条路能覆盖除"传输层本身"以外的全部同步逻辑,因为
`SteamTransport` 和 `LoopbackTransport` 实现同一个 `ITransport` 接口。

## L2:双机 + Steam 真通路(里程碑验收)

需要**两台机器 + 两个各自拥有游戏的 Steam 账号**(家庭共享不支持同时游玩)。
验证内容:大厅创建、好友邀请、对方接受后入房、NAT 穿透。

单机也能冒烟一半:按 F9 建大厅后看面板显示大厅 ID 与"房主"身份,
说明 Steam 大厅链路通;好友列表里点邀请能发出通知。

## L3:异地公网(发布前)

朋友异地实测 Steam P2P 中继质量,观察延迟对 15 Hz 状态同步的影响。
鸭科夫的经验:国内玩家可能需要加速器,这是 Steam 中继的固有情况。

## v0 验收清单

- [x] F9 创建 Steam 大厅成功(日志有大厅 ID)
- [x] 标题界面「联机大厅」按钮 + 悬停高亮
- [x] 管理页面:房间状态、好友列表、页面内直接邀请
- [x] 模拟客机接入后化身出现并跟随移动(2026-08-12 无人值守实测通过)
- [x] 握手 + 双向位置同步(net 日志有 PEER_STATE 持续流入)
- [ ] 挂机 10 分钟无异常日志刷屏、无明显 GC 卡顿

## 当前快捷键

| 键 | 功能 |
|----|------|
| F9 | Steam:创建好友大厅 |
| F4 | Steam:好友邀请覆盖层 |
| F6 | 回环:本机做主机 (UDP 27851) |
| Ctrl+F6 | 回环:连接 127.0.0.1 主机 |
| F8 | 开发状态面板 |
| F7 / Ctrl+F7 | dump UI 层级 / dump 运行时状态 |
| F5 | 热重载 Mod 列表 |
| F1 | 官方调试控制台 |

## 无人值守自测(AutoTest)

开发者/AI 无法在游戏里按键,所以留了一个标记文件入口:

```powershell
.\dev.ps1 -NoLaunch
Set-Content "$env:USERPROFILE\AppData\LocalLow\RedSawGames\DolocTown\DolocCoop-debug\autotest.flag" "0" -NoNewline
Start-Process "steam://rungameid/2285550"
# 游戏会自动:加载存档0 → 等玩家就绪 → 开回环主机
.\sim.ps1        # 再接入模拟客机
```

标记文件用完即删,不会影响下次正常启动。全过程写进 `net-<PID>.log`,
关键行:`AUTOTEST 开启回环主机` → `PEER_JOINED` → `[Avatar] 已为 xx 构建化身` → `PEER_STATE`。

**2026-08-12 实测结果**:全链路打通。

## 时间同步测试(反向:游戏当客机)

```powershell
.\dev.ps1 -NoLaunch
.\sim.ps1 -HostMode -Time 8000        # 模拟客机改当主机,广播假时间
Set-Content "...\DolocCoop-debug\autotest.flag" "0 client" -NoNewline
Start-Process "steam://rungameid/2285550"
```

**2026-08-12 实测**:
- 合理偏差 → `TIME_CORRECT diff=7018 local=982 host=8000 count=1`,
  之后持续 `TIME_RECV` 但不再校时(容差生效,不会反复抖动)✔
- 极端偏差(50 游戏日)→ `TIME_REJECT diff=4319018`,安全阀拦截 ✔

