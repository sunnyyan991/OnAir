# 配置与演示操作

当前正式入口：Unity 6000.3.25f1，打开 `OnAir/Assets/Scenes/Main.unity` 后 Play。

## 当前画面与地图

- 原生分辨率输出；PIXEL 2X / PIXEL 3X 已删除。材质制作状态见 [物体级像素材质](PixelMaterialWorkflow.md)。
- 镜头驱动二维分块加载，沿飞行方向预加载，详细机制见 [V14](WorldV14.md)。过去至少一分钟的航程保留重建数据，不常驻全部旧地图对象。
- 启动进入标题；START 选槽开始新旅程，LOAD GAME 读取继续。NEW SEED 在当前槽换图并保留上一段旅程。命令行 `-onairSeed 数字` 用于复现测试，默认禁用存档；`-onairSaveDirectory 绝对目录` 开启隔离测试存档。
- NEW FLIGHT / LOAD GAME 选择占用槽后可点击 DELETE SAVE，确认后删除该槽及备份；空槽禁用。旧世界配置存档当前不自动迁移，显示 INCOMPATIBLE SAVE 并保留文件，可选空槽重新开始或明确删除，见 [存档说明](SaveSystem.md#损坏与兼容)。
- 飞机连续前飞，速度默认 24 米/秒，屏幕航向约 30°；安全高度根据建筑库和地形计算。盘旋功能已取消。
- 保留 13 种当前日式建筑。旧 City 六栋停用资产已归档；Coast / Desert 十二栋兼容资产仍保留。

## 面板

- EXPLORE：选择群系快速预览；左上英文名称反映画面中占主导的群系。
- WEATHER：AUTO 按飞机下方区域的天气权重随机切换；也可手动循环 晴 / 阴 / 雨 / 暴雨 / 雪 / 沙尘暴。天气切换不重建建筑。
- TIME OF DAY：拖动像素旋钮的指针连续选时；左侧日出、上方正午、右侧日落、下方午夜。上半圆为白天，下半圆为黑夜。拨动后时间自动继续推进，默认白天 600 秒、夜晚 300 秒；暂停飞行时沿用原规则暂停时间。
- AUTO FLIGHT：暂停 / 继续。
- NEW SEED：生成新地图并重置航程，保留上一段旅程。
- PREVIOUS FLIGHT：返回上一段旅程；显示在展开的面板下方。
- 存档状态：默认每 30 秒自动保存，切到后台或正常退出时保存；离线不推进飞行。损坏主档先尝试备份，无法恢复时保留原文件并停止自动覆盖，详见 [存档说明](SaveSystem.md)。
- RELOAD CSV：校验并更新表格；非法输入保留原配置。重建地图属于显式操作。
- WORLD STATUS / FPS：显示建筑数量、里程与帧率。建筑数包含前方预加载的对象，并非只统计屏幕可见楼房。

## 配置位置

- `Assets/Data/Tables/buildings.csv`：25 条规则，107–131。119–131 为当前十三栋日式建筑，107–118 为旧 Coast / Desert 兼容资产。零权重的 101–106 已随旧城市资产移出。
- `Assets/Data/Catalogs/BuildingCatalog.asset`：建筑 ID 与 prefab 映射。
- `Assets/Data/Profiles/WorldProfile.asset`：世界全局配置。`segmentMeters`（默认 320）为每 64 单位世界切片对应的航程米数，决定飞行速度和预加载距离；另含城市 WorldKit 与昼夜天空、太阳、环境光、窗灯颜色。
- `Assets/Data/Tables/weather_fx.csv`：天气粒子的数量、尺寸、颜色和过渡。
- `Assets/Data/Tables/themes.csv`：国家/区域主题（JAPAN / USA / CYBERPUNK CITY），每个主题列出可生成的地形及相对权重。开局在 NEW FLIGHT 页面用 THEME 按钮选择，存进存档。
- `Assets/Data/Tables/climate.csv`：每个区域的自动天气权重与持续秒数，见 [天气效果](WeatherFX.md#区域天气)。
- `Assets/Settings/Rendering/MainPipeline.asset`：正式 URP 配置，原生输出、SRP Batcher。
- `Assets/Scripts/World/ContinuousWorldPlan.cs` 及水系、高架模块：连续世界生成规则。
- `Assets/Scripts/World/TerrainStreamer.cs`：视野加载、预加载预算和历史航程记录。

CSV 保存为 UTF-8，保留 `##var / ##type / ##` 三行和数据行首空列。建筑 ID 按文本处理，保留前导零。新增资产必须有 BuildingFootprint 和 BuildingView，登记目录与规则表；min_scale / max_scale 当前均为 1。

历史 V01–V14 文档保留当时的设计与验收记录，其中旧的盘旋、渲染菜单、规则数量和固定条带加载说明不再代表当前行为。清理与性能修改见 [原生渲染与资产清理](NativeRenderingCleanup.md)。
