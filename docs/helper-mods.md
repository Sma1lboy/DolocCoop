# 开发辅助 Mod / 工具调研(2026-08-12)

## 游戏与官方自带的辅助资源

- **内置 RedSaw 控制台**:游戏本体有开发者控制台(`LocalLow\RedSawGames\DolocTown\redsaw_console_settings.json`),
  原版未暴露开关;社区的「Y Key Console」就是把它/同类调试功能开放出来的。
- **官方示例 Mod 里的开发资源**(`workshop\content\2285550\3705665433\Content\04 其他内容示例`):
  - `02 道具数据模板`:无功能道具 / 帽子 / 设备装饰 / 作物(种子+果实+plant_tbseed)/ 食物 的**空白 JSON 模板**
  - `03 备用贴图\01 调色板\doloc_palette.png`:**官方调色板**(画贴图时取色用)
  - `03 备用贴图\02 主角动画`:全套主角头发/身体/整体动画参考帧(定位帽子、换装贴图必备)
  - `01 文本本地化`:localization_tbtextmapper{en,zh_cn,zh_tw}.json 本地化模板
  - `04 掉落上限、保底配置`:掉落与保底配置示例

## 创意工坊辅助 Mod(按推荐度)

| Mod | ID | 作者 | 用途 | 状态 |
|-----|-----|------|------|------|
| **DTMAPI** | 3743016467 | Yuuka | 功能 Mod 运行时前置(BepInEx 方案):配置菜单、日志诊断、Manager 状态页、报告导出。9300+ 订阅,20+ Mod 依赖它 | 活跃,8/7 更新 |
| **Y Key Console** | 3742714442 | Yuuka | **测试神器**,依赖 DTMAPI。存档内按 Y 开调试控制台:搜索并给予物品(含 Mod 物品,右键×10)、改天气、传送、跳时间、时间/移动速度 | 活跃,2000 订阅 |
| **更好上手的模组制作工具** | 3755367789 | 夕之溪 | 订阅后**本地浏览器运行**的可视化 Mod 编辑器(不进游戏加载):`workshop\content\2285550\3755367789\Content\模组工具.html`。支持道具/静态帽子/装饰设备/配方/商店道具/作物,一键导出 zip。已知 bug:缺 JSZip 依赖时打包报错 | 7/19 更新 |
| Doloc Auto Mod Loader | 3780218737 | 木子 | BepInEx DLL 自动装载器:启动时把含 DLL 的 Mod 文件夹复制进 `BepInEx\plugins`(首次进游戏只复制,需重启二次生效)。写 DLL Mod 分发时相关 | 新,8/11 更新 |
| Developer Mode | 3742618545 | 姓王且名建林 | 调试悬浮窗(给物品/加钱/解锁基因),依赖已下架的 DolocTown SMAPI(3726044511,页面 404) | **已停更**,作者推荐改用 Y Key Console |
| [TBA] Doloc YarnLoader | 3778048961 | Qiuzy | 自定义对话/剧情(游戏对话用 YarnSpinner) | 待观察 |

## DTMAPI 要点(作为功能 Mod 基础的评估)

- 定位:作者明言是"**官方功能 API 出来前的过渡产物**"。底层是 BepInEx 插件方案。
- 安装:订阅后运行 `workshop\content\2285550\3743016467\1_install_dtmapi.bat`;
  `3_check_dtmapi_status.bat` 查状态([OK]/[MISSING]);`2_` 卸载;`4_` 收集日志。
- 主菜单左上角图标打开 Mod 配置菜单;Mod 还需在官方 Mod 界面启用。
- 风险:非官方、游戏版本更新可能击穿;与旧版 DolocPlus/BepInEx 残留冲突(修复:删游戏根目录 `BepInEx` 文件夹后重装)。
- 作者支持渠道:Steam 好友 / QQ 3033654263,响应活跃。

## 建议工作流

1. 内容型 Mod(帽子/道具/配方/作物):**不需要任何前置**,官方 JSON 体系即可(本工作区现行做法)。
2. 测试:装 DTMAPI + Y Key Console,进存档按 Y 直接给自己 Mod 物品,免去商店流程。
3. 功能型 Mod(改玩法/UI/自动化):基于 DTMAPI 生态(BepInEx + Harmony,反编译 Assembly-CSharp.dll 找 Hook 点),但注意官方 API 出现后可能要迁移。
