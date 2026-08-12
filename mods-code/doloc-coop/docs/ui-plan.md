# DolocCoop UI 方案:做成和游戏原生一模一样

## 结论:对,玩家看到的界面必须用游戏自己的 UI 组件做

分两类,标准不同:

| | 开发面板(F8) | 玩家界面(大厅/邀请/好友) |
|---|---|---|
| 受众 | 只有我们自己 | 所有玩家 |
| 做法 | 自建 Canvas,能看就行 | **克隆游戏原生 UI 组件** |
| 现状 | 已改用 uGUI(IMGUI 被游戏 Canvas 盖住了) | 待做 |

## 游戏的 UI 架构(已摸清)

```
DolocUiSystem                 UI 总线,FindObjectOfType 可拿到
  └ DolocUiState<TPanel>      每个界面 = 一个"状态"(带 Register/Unregister 生命周期)
      └ DolocUIPanel          面板本体
          └ MenuUI / TextMenu / MenuButton / IconWithTitleMenu   可复用的菜单组件
```

- 菜单内容是**配置驱动**的:`TbMainMenu` 表提供 `title` + `icon_asset`,
  `MenuUI.Render(titles, icons)` 一次性渲染;回调用
  `SetSelectCallbacks / SetClickCallbacks / SetPointerEnterCallbacks` 挂。
- 文本走 TMPro + 本地化表(`localization_tbtextmapper*.json`),字体自动跟随语言。

## 做法:克隆,不要重写

**不要**用 uGUI 从零拼一套"看起来像"的界面——字体、九宫格边框、悬停音效、手柄导航
都会对不上。正确做法是运行时**把游戏现成的按钮/面板 Instantiate 一份**,改文字改回调:

```csharp
// 伪代码
var uiSystem = Object.FindObjectOfType<DolocUiSystem>();
var templateBtn = /* 从标题界面找到"继续游戏"按钮 */;
var coopBtn = Object.Instantiate(templateBtn, templateBtn.transform.parent);
coopBtn.name = "CoopButton";
SetLabel(coopBtn, L("联机大厅", "Multiplayer"));
Bind(coopBtn, OnCoopClicked);
```

这样字体、配色、动效、音效全部自动一致,游戏更新皮肤我们也跟着变。
XvX 的联机 mod 就是这么在标题界面底部加了"创建房间"。

## 玩家流程(按你说的:先 menu 层面)

```
标题界面
  └ [联机大厅]  ← 新增按钮(克隆原生按钮)
       ├ 创建房间 → Steam 建 lobby → 显示房间面板
       │    ├ 成员列表(Steam 头像 + 昵称)
       │    ├ [邀请好友] → SteamFriends.ActivateGameOverlayInviteDialog
       │    └ [开始] → 房主先进存档,广播 "进房" 给所有客机
       └ 加入房间 → 接受好友邀请后自动进入房间面板,等房主开始
```

关键设计(吸取 XvX 的教训):
1. **房主先进**,世界以房主存档为准;客机收到广播后进入,只做渲染不写盘。
2. 房间面板显示"版本校验"结果(mod 版本 + 游戏 build),不匹配直接拒绝并说明原因。
3. 客机存档只读:进联机前弹一次原生确认框(`ConfirmUiState`/`QuestionUiState` 可复用)。

## 实施顺序

1. **v0.2** 标题界面加一个原生按钮 + 房间面板(成员列表 / 邀请 / 开始),先只连 Steam 大厅,不同步游戏
2. **v0.3** 房主"开始"→ 全员进同一存档 → 玩家位置/动画同步(化身已实现)
3. **v0.4** 聊天走原生 UI(游戏有对话框组件可复用)
4. 之后再做世界状态同步

## 待办:定位标题界面的按钮

标题界面(Title/Start)不在 `DolocTown.UI` 的 MainMenuPanel 里——那是游戏内 ESC 菜单。
下一步用运行时反射 dump 标题场景的 Canvas 层级(写个 F7 命令打印整棵树),
找到"继续游戏/新游戏"按钮的实际类型和路径,然后克隆它。
