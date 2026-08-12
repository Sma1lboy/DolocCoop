静态帽子 Mod 需要的文件(全部放本 Content 目录):

JSON(字段结构见 docs\modding-notes.md,具体照抄官方【Dev】Example Mod):
  item_tbitem.json    物品定义,sub_type 填 "kit_hat",function 填 "ItemFunctionHat"
  player_tbhat.json   帽子定义,id 与 item 一致

贴图(把 <ID> 换成你在 JSON 里定的唯一 ID):
  icon_item_<ID>.png        28×28 物品图标
  anim_hat_<ID>_idle_0.png  64×64 站立帽子贴图
  anim_hat_<ID>_climb_0.png 64×64 攀爬帽子贴图
  preview_hat_<ID>.png      展示架预览图

Mod 根目录还需:
  preview.png  创意工坊预览图
  icon.png     32×32 游戏内 Mod 菜单图标

注意:ID 不要与其他 Mod 或原版内容冲突,先查官方文档「05 ID 对照表」。
