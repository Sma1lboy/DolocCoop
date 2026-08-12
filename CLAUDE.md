# Doloc Town Mod 开发工作区

《多洛可小镇》(Steam AppID 2285550) 创意工坊 Mod 开发。用户偏好中文交流。

## 规则

- Mod 在 `mods\<名字>\` 下开发,结构:`info.json` + `preview.png` + `icon.png` + `Content\`(JSON 配置与 PNG 贴图混放)。
- JSON 字段规范和贴图命名见 `docs\modding-notes.md`;不确定的字段以创意工坊订阅的 `【Dev】Example Mod` 实际文件为准(位于 `C:\Program Files (x86)\Steam\steamapps\workshop\content\2285550\`),不要凭空猜字段。
- 测试部署:`.\deploy.ps1 <Mod名>` → 复制到 `%USERPROFILE%\AppData\LocalLow\RedSawGames\DolocTown\MODS`。
- 改动可能影响存档时,先提醒备份 `...\DolocTown\SAVE\ea-doloc-archive-0.data`。
- 内容 ID 必须唯一,新 ID 先对照官方文档「05 ID 对照表」(飞书)。

## 两类 Mod 的区别(重要,别混淆)

| | 内容 Mod(JSON) | 代码 Mod(DLL) |
|---|---|---|
| 位置 | `LocalLow\...\DolocTown\MODS\<名字>\` | `游戏目录\BepInEx\plugins\<名字>\` |
| 加载者 | 游戏自己的 ModManager | BepInEx |
| 游戏 Mod 菜单可见? | ✅ 是 | ❌ 否 |
| 例子 | red-beret(帽子) | DolocCoop、DolocDevTools、DTMAPI |

**代码 Mod 想在游戏 Mod 菜单里露面,要额外在 MODS 下放一个只含 `info.json`+`icon.png`+`preview.png`
的同名文件夹**(DTMAPI 和 Doloc Auto Mod Loader 都是这么做的)。因此 DolocCoop 是两半:
- 身份卡:`mods\doloc-coop\`(info.json,部署后在 Mod 菜单显示"多洛可联机 DolocCoop")
- 实际逻辑:`mods-code\doloc-coop\` 编译出的 DLL → `BepInEx\plugins\DolocCoop\`

发布到创意工坊时把两半合成一个包(参考 Auto Mod Loader 的 zip 解压方案)。

## 开发热键与工具链(已部署)

- 游戏目录已装 BepInEx 5.4.23(DTMAPI 安装器装的),插件在 `BepInEx\plugins\`:DTMAPI、DolocCoop、DolocDevTools。
- **F5** = 热重载 Mod 列表(DolocDevTools 调 `DolocAPI.modManager.ReloadMods()`,免重启游戏;JSON 内容改动需重进存档,贴图/启用状态即时)。
- **F1** = 官方调试控制台(`LocalLow\RedSawGames\DolocTown\config.json` 里 `enableGameConsole: true` 解锁;有 add_money/genitem/gen_monster 等全套命令)。
- **F9/F10**(DolocCoop)= 创建 Steam 大厅 / 好友邀请。
- 全游戏反编译源码在 `references\decompiled\Assembly-CSharp\`(3665 个 .cs,ilspycmd 生成);游戏静态中枢是 `DolocAPI`(modManager/gameManager/dataPersistenceManager 都挂在上面)。
- C# 插件构建:`mods-code\doloc-coop\build.ps1`;DevTools 用 `dotnet build mods-code\doloc-devtools\src\DolocDevTools.csproj -c Release` 后拷 DLL 到 plugins。

## 参考

- 官方中文文档(飞书,游客可访问):https://ka7deoo0opr.feishu.cn/wiki/ElmCwXsRCi7OPvkOh8JcvXK9nfc
- 官方英文文档:https://docs.google.com/document/d/1D9zV8NAYkAMKZ3_l4RO8mAUEQS1_8LQK6XiDcm8Mkfk/edit
- 游戏引擎:Unity 2021.3.16 Mono;逻辑在 `Doloc Town\DolocTown_Data\Managed\Assembly-CSharp.dll`(可 ILSpy 反编译);资源走 Addressables。
- 代码级功能 Mod 用社区框架 DTMAPI(创意工坊,作者 Yuuka),官方不支持。
