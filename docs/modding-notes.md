# Doloc Town Mod 格式笔记(整理自官方文档,2026-06-12 版)

来源:官方英文指南(Google Docs)+ 飞书中文文档。做 Mod 前请先订阅并参考
创意工坊里的 `【Dev】Example Mod`。

## Mod 文件夹结构

放在 `C:\Users\jackson\AppData\LocalLow\RedSawGames\DolocTown\MODS\<你的Mod名>\`:

```
<你的Mod名>\
├── info.json      必需,Mod 元数据,游戏靠它识别 Mod
├── preview.png    创意工坊预览图
├── icon.png       游戏内 Mod 菜单图标(建议 32×32)
├── Content\       所有 JSON 配置 + PNG 贴图都放这里
└── _IGNORE\       (可选)此文件夹内容不会被打包,放临时文件
```

## info.json

```json
{
  "name": "Mod Name",
  "author": "Author Name",
  "version": "1.0.0",
  "description": "Description text",
  "tags": ["Tag1", "Tag2"],
  "localized_name": {
    "schinese": "中文名",
    "tchinese": "繁體名",
    "english": ""
  },
  "localized_description": {
    "schinese": "中文描述",
    "tchinese": "繁體描述",
    "english": ""
  }
}
```

多语言字段留空则使用默认 `name`/`description`。

## 美化模组(贴图替换,零 JSON)

1. 从官方文档「05 ID 对照表」查到目标资源 ID;
2. 用**完全相同的文件名**制作替换贴图,丢进 `Content\`;
3. 缺失的帧自动用原版 → 可以只替换某一帧,例如只放
   `anim_player_run_2.png` 就只改跑步动画第 3 帧。

## 贴图命名规范

| 类型 | 命名 | 尺寸 |
|------|------|------|
| 物品图标 | `icon_item_<物品ID>` | 28×28,居中 |
| 主角动画 | `anim_player_<动作>_<帧号>` | 64×64,底部留 16px |
| 帽子 | `anim_hat_<帽子ID>_<动作>_<帧号>` | 64×64 |
| 设备/装饰 | `sprite_equipment_<ID>`、`sprite_equipment_<ID>_flip` | 四周留 1px |
| 平台地块 | `tile_platform_<ID>_platform_left_0` / `_middle_[0-1]` / `_right_0` | 最大 12×12 |

帧号从 0 开始。

## 内容 Mod 的 JSON 配置文件

全部放 `Content\` 内。**ID 必须全局唯一** —— 与其他已启用 Mod 撞 ID 会显示错误。

### item_tbitem.json(物品定义,一切内容的基础)

关键字段:
- `id` — 唯一 ID
- `sub_type` — 分类:`material_nature` / `kit_hat` / `equipment_ornament` 等
- `salable` / `disposable` / `consumable` / `cookable` — 布尔属性
- `selling_price` / `buying_price` — 售价/买价
- `overlay` — 堆叠上限
- `ui_sprite_asset` — 图标引用,格式 `icon_item_<ID>`
- `title` / `description_basic` — 本地化 key 及默认文本
- `function` — 物品行为:`ItemFunction` / `ItemFunctionHat` / `ItemFunctionEquipment` 等

### player_tbhat.json(帽子,新手推荐)

- `id` — 与 item ID 一致
- `defense` — 防御加成
- `idle_sprite` — `anim_hat_<ID>_idle_0`
- `climb_sprite` — 攀爬姿势贴图
- `preview` — 展示架图片 `preview_hat_<ID>`
- `hide_hair` — 是否隐藏发型

静态帽子 = `item_tbitem.json` + `player_tbhat.json` + 3 张贴图(idle / climb / preview)。

### equipment_tbequipment.json(设备/农场装饰)

- `id`、`menu_type`(0 农业 / 1 工业 / 2 生活 / 3 牧场)
- `scene_asset` — `sprite_equipment_<ID>`,`flip_asset` — 镜像贴图
- `cover_size` — 占地格子 (x, y)
- `fit_type` — 0 通用 / 1 室内 / 2 室外
- `function` — 如 `EquipmentFuncDecorator`

装饰 Mod = item + equipment + (可选) recipe 三个 JSON。

### recipe_tbrecipe.json(配方)

- `id`、`output_item`(含 min/max 数量)、`input_items`(材料数组)
- `cost_time` — 制作时长,单位 5 分钟
- `recipe_sub_type` — 分类
- `show_in_handbook` — 是否显示在图鉴

### 料理(菜肴)

固定配方需 4 个文件:
`item_tbitem.json` + `item_tbeatingeffect.json`(食用 buff)+
`recipe_tbrecipe.json` + `mod_tbmodrecipegroupextension.json`(绑定到炊具)。

灵活配方(可变食材)另加:
`recipe_tbingredientgroup.json`(食材分类)+ `recipe_tbdish.json`(组合规则)。

### 商店上架

- 贸易部(普通商店):`mod_tbmodstoreextension.json`,游戏货币,支持季节性数量变化
- 兑换部:`mod_tbmodexchangestoreextension.json`,物品/货币兑换,支持前置条件

两者都通过 `extra_items` 数组追加到现有商店列表。

## 测试与上传

1. 部署到 MODS 目录(用本工作区 `deploy.ps1`);
2. **先备份存档** `...\DolocTown\SAVE\ea-doloc-archive-0.data`;
3. 游戏内 Mod 菜单 → Local 标签启用;
4. 验证后在游戏内上传创意工坊(详见飞书文档「在创意工坊上传你的模组」)。

## 代码级 Mod(非官方)

官方 JSON 体系不支持改核心玩法。功能性 Mod 依赖社区框架 **DTMAPI**
(创意工坊,作者 Yuuka),游戏为 Unity 2021.3 Mono,可反编译
`DolocTown_Data\Managed\Assembly-CSharp.dll`(约 6.4MB)研究内部逻辑。
注意:功能 Mod 不受官方支持,更新后易失效。
