# 调试数据流(给开发者 + AI 助手看的)

核心问题:AI 助手看不到游戏画面。解决办法是**让游戏把状态 dump 成文件**,
助手直接读文件分析,不需要人肉截图转述。

## 输出目录

```
C:\Users\jackson\AppData\LocalLow\RedSawGames\DolocTown\DolocCoop-debug\
```

| 文件 | 内容 | 触发方式 |
|------|------|----------|
| `latest-ui-tree.txt` | 所有 Canvas 的完整层级:节点名、组件类型全名、Text/TMP 文本、Button 状态、Image sprite、RectTransform 位置尺寸 | 游戏内按 **F7** |
| `latest-game-state.txt` | DolocAPI 状态、玩家位置/朝向/状态机、玩家 GameObject 层级、Animator 与全部动画剪辑名、Mod 列表、插件列表 | 游戏内按 **Ctrl+F7** |
| `net-<PID>.log` | 联机会话滚动日志:握手、加入/离开、聊天、位置同步采样(每 30 帧一条) | 自动,建立会话时开始写 |
| `ui-tree-<时间戳>.txt` / `game-state-<时间戳>.txt` | 同上的历史存档 | 每次 dump 自动留档 |

**按进程号分文件**是特意设计的:本机双开测试时,两个游戏实例各写各的
`net-<PID>.log`,可以左右对照看主机和客机的收发时序。

## 典型用法

**要在标题界面加原生按钮** → 在标题界面按 F7 → 助手读 `latest-ui-tree.txt`
找到"继续游戏"按钮的确切节点路径和组件类型 → 写代码克隆它。

**联机同步有问题** → 双开跑一轮 → 助手对照两个 `net-<PID>.log`
看是发没发出去,还是收到了没渲染。

**动画不对** → 进存档按 Ctrl+F7 → `latest-game-state.txt` 里有完整的动画剪辑列表
和当前状态 hash,用来核对化身的 Animator 驱动。

## 全部热键

| 键 | 功能 | 提供者 |
|----|------|--------|
| F1 | 官方调试控制台(genitem / add_money / gen_monster 等) | 游戏自带,config.json 解锁 |
| F5 | 热重载 Mod 列表(免重启游戏) | DolocDevTools |
| F7 | dump UI 层级树 → 文件 | DolocDevTools |
| Ctrl+F7 | dump 运行时状态 → 文件 | DolocDevTools |
| F8 | 开关状态面板(uGUI,盖在游戏 UI 之上) | DolocDevTools |
| F9 / F4 | Steam 建大厅 / 好友邀请 | DolocCoop |
| F6 / Ctrl+F6 | 回环主机 / 连接回环主机(双开测试) | DolocCoop |

## 注意

- **游戏运行时 DLL 被锁定**,`build.ps1` 会检测并提示先关游戏;JSON 内容 Mod 不受此限制。
- IMGUI(`OnGUI`)会被游戏的 Screen Space Overlay Canvas 盖住 —— 插件 UI 一律用
  自建 Canvas + `sortingOrder = 32767`。

