# DolocTownMods

《多洛可小镇》(Doloc Town, Steam AppID 2285550) 的 Mod 开发工作区。

主线目标:**DolocCoop —— 基于 Steam 的联机模组**。

## 仓库内容

| 目录 | 说明 |
|------|------|
| `mods-code/doloc-coop/` | **DolocCoop** 联机插件(C#/BepInEx)。`src/CoopCore` 是零游戏依赖的联机核心,`src/DolocCoop` 是游戏侧实现 |
| `mods-code/doloc-devtools/` | **DolocDevTools** 开发辅助插件:状态面板、Mod 热重载、运行时 dump |
| `mods-code/shared/` | 两个插件共用的代码(PlayerLoop 驱动、字体工具) |
| `mods/` | 内容 Mod(JSON + 贴图):`doloc-coop` 身份卡、`red-beret` 示例帽子 |
| `templates/` | 内容 Mod 模板 |
| `docs/` | Mod 格式、辅助工具、联机调研、调试流程 |
| `dev.ps1` | 一键开发循环:关游戏 → 编译 → 部署 → 启动 |

## 快速开始

```powershell
# 前置:游戏目录已装 BepInEx 5(订阅 DTMAPI 后运行它的 1_install_dtmapi.bat 即可)
#      本机安装 .NET SDK
.\dev.ps1              # 编译 + 部署 + 启动游戏
.\dev.ps1 -NoLaunch    # 只编译部署
.\sim.ps1              # 启动模拟客机(单机测联机,见下)
.\test.ps1             # 跑 CoopCore 自动化测试(纯逻辑,不需要游戏)
```

游戏路径默认 `C:\Program Files (x86)\Steam\steamapps\common\Doloc Town`,
可用环境变量 `DOLOC_TOWN_PATH` 覆盖。

## 怎么本地测联机

**游戏只能单开**(实测第二个实例会自行退出),所以用 `tools/CoopSimClient` ——
一个借助 `CoopCore` 假扮第二个玩家的控制台程序:

```powershell
.\dev.ps1        # 终端1:启动游戏 → 进存档 → 按 F6(回环主机)
.\sim.ps1        # 终端2:模拟客机接入,会绕着你的角色转圈
```

看到控制台打印 `[✔] 已收到主机玩家状态` 就说明双向通信正常;
游戏里会出现一个跟随移动的化身,联机大厅面板的成员列表也会显示它的实时坐标。

完整测试策略见 `mods-code/doloc-coop/docs/testing.md`。

## 功能现状(DolocCoop v0.2)

- ✅ 标题界面「联机大厅」按钮(克隆游戏原生按钮,含悬停高亮与 ▶ 箭头)
- ✅ 联机管理页面:房间状态、成员列表、好友列表(三档在线状态)、页面内直接邀请
- ✅ Steam 大厅创建 + `InviteUserToLobby` 直接邀请(不依赖 Steam 覆盖层)
- ✅ 本机回环传输(UDP),用于单机双开测试
- ✅ 远端玩家化身:复制主角视觉层级 + 独立 Animator 驱动(**已实测**:握手→建化身→位置持续同步)
- ✅ 聊天:回车说话、Esc 取消,打字时接管游戏输入(不会边打字边走路)
- ✅ 无人值守自测(AutoTest 标记文件 + 模拟客机),单机即可跑完整链路
- ✅ 客机入房流程:启动即注册 Steam 邀请回调,进房自动弹出面板并上报昵称
- ✅ 时间同步(主机权威,带容差与安全阀;**已实测**校时与拒绝异常值)
- ⬜ 世界状态同步剩余项(天气 → 交互 → 农场设备 → NPC)

## 热键

| 键 | 功能 |
|----|------|
| F9 / F4 | 创建 Steam 大厅 / 好友邀请 |
| F6 / Ctrl+F6 | 回环主机 / 连接回环主机(双开测试) |
| F8 | 开发状态面板 |
| F7 / Ctrl+F7 | dump UI 层级 / dump 运行时状态到文件 |
| F5 | 热重载 Mod 列表(免重启游戏) |
| F1 | 游戏自带调试控制台(需 `config.json` 里 `enableGameConsole: true`) |

## 本游戏的三个关键坑

1. **游戏会销毁 Mod 创建的 GameObject**,`DontDestroyOnLoad` 也保不住 →
   一切逻辑不能挂 MonoBehaviour,改用 PlayerLoop 注入(`shared/UnityLoopDriver.cs`)。
2. **IMGUI(OnGUI)会被游戏的 Screen Space Overlay Canvas 盖住** →
   插件 UI 用自建 Canvas + 高 `sortingOrder`。
3. **游戏自带 TMP 字体是像素字体**,而 `CreateDynamicFontFromOSFont` 在打包版返回 null →
   从游戏已加载的思源黑体运行时生成 SDF 资产(`shared/UiFont.cs`),并验证中文字形。

详见 `docs/debug-workflow.md`。

## 参考资料(不入库)

- 反编译的游戏源码:本地用 `ilspycmd` 生成到 `references/decompiled/`(版权内容,已 gitignore)
- 鸭科夫联机 Mod:`git clone https://github.com/Mr-sans-and-InitLoader-s-team/Escape-From-Duckov-Coop-Mod-Preview references/duckov-coop-mod`

## 许可

MIT(见 LICENSE)。注意:本仓库不包含任何游戏本体资源或反编译代码。





