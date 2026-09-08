# 配置与演示操作

## 配置位置

规则统一位于 `OnAir/Assets/Data/Tables`，可用 Excel 编辑 CSV。保存 UTF-8，保留 `##var / ##type / ##` 三行和数据行最前面的空列。ID 按文本导入和编辑，以免 Excel 去掉前导零；程序不会把 id 转成整数。

- **buildings.csv**：18 条规则，101–118，城市/海岸/沙漠各 6 种。biomes 筛选地形，weather/periods 在生成时筛选，weight 为相对权重；列表用 `|`，`*` 表示不限。prefab_id 对应 BuildingCatalog 资源键。
- **terrains.csv**：id 对应 TerrainProfile.id；biome 为 City/Coast/Desert；distance_meters 为巡航距离，selection_weight 为下一地形权重，均大于 0；building_density 为 0..1；weather 与 weather_weights 是一一对应的 `|` 列表，权重 0 表示禁用，至少一项大于 0。
- **weather_fx.csv**：Clear、Cloudy、Rain、HeavyRain、Snow、Sandstorm 的粒子形状、数量、速度、大小、颜色与过渡。它定义表现，地形允许什么天气由 terrains 决定。

地形光照、天空和窗灯颜色在 `Data/Profiles/*Terrain.asset`；道路、绿化、道具在 `Data/Catalogs/*WorldKit.asset`。建筑预制体放 `Prefabs/Buildings`，设置 BuildingFootprint 和 BuildingView 后，在 BuildingCatalog 登记键，再添加表格规则。当前固定街区占地要求 min_scale/max_scale 都为 1。

## 飞机与镜头

`Prefabs/Characters/Plane.prefab` 的 FlightController 调整速度、高度、偏航、起伏和盘旋参数。Main Camera 的 CameraFollow 调整跟随平滑、偏移和提前取景。

FlightView 的 modelPrefab 已引用 LowWing 低翼模型，按真实航向转动并带有螺旋桨动画；关闭 useLowPolyModel 可恢复 Sprite。模型通过预制体引用加载，不依赖 Resources 路径。模型说明见 Aircraft.md。

当前每段 320 米，巡航速度 12 米/秒；每段显示 64 Unity 单位，实际移动为 2.4 单位/秒。首次巡航 10 秒后盘旋，以后每巡航 22 秒盘旋；半径 4.5 单位，一圈约 12 秒，左右交替。关闭 autoCircle 可取消自动盘旋。这些都是演示值。

暂定：盘旋完整一圈后回到航线，其间不推进下一地形。ROUTE 是路线里程，不含盘旋航程。昼夜由面板选择，现实时间独立显示；尚无离线收益和里程存档。

## 面板

- TERRAIN：调试跳到下一段，取消盘旋并对准新位置。
- WEATHER / TIME OF DAY：切换天气/时段，保留已有建筑。
- AUTO FLIGHT：暂停/继续巡航、盘旋、起伏、路线与天气粒子。
- CIRCLE ONCE：立即绕一圈；暂停或已在盘旋时不会重复启动。
- NEW SEED：新随机种子，重置演示路线。
- RELOAD CSV：先校验三张表，再更新配置和重新生成地块。非法配置保留当前有效数据。terrains 内容改变时，用原随机种子重置路线，以应用新距离/规则；只重载建筑和天气表时保留航程。

城市允许晴、阴、雨、大雨、雪；海岸允许晴、阴、雨、大雨；沙漠允许晴、阴、沙尘暴。均为演示配置。地块边界仍直接拼接，正式海岸线和自然地势尚未制作。
