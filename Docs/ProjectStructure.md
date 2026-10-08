# 项目结构

Unity 工程为 `OnAir/`，正式入口为 `Assets/Scenes/Main.unity`。根目录 Art 保存美术原稿，Docs 保存项目说明。

默认世界已升级为 [连续世界 V01](ContinuousWorld.md)：ContinuousWorldPlan 负责全局地理与不等街区，ContinuousWorldLayout 负责表现，TerrainStreamer 只负责加载切片。下方旧分段地形说明保留为兼容模式。

| Assets 内目录 | 内容 |
| --- | --- |
| Scenes | Main；临时场景 `_Temp_任务名` 验证完成后删除 |
| Scripts/Game | GameEntry 初始化和引用连接；GameSession 当前状态及操作 |
| Scripts/Flight | FlightController 实际移动与盘旋；FlightView 外观；CameraFollow 镜头 |
| Scripts/World | JourneyController 航程与路线；TerrainStreamer 地块生命周期；BlockGenerator 布局；BuildingFootprint 占地；BuildingView 窗灯引用 |
| Scripts/World/DenseCityLayout | 城市专用窄路与错位街区，建筑占地排列，路面批量网格与释放；其他地形仍由 BlockGenerator 原布局处理 |
| Scripts/Environment | EnvironmentController 光照和窗灯；WeatherParticles 天气粒子 |
| Scripts/UI | FlightPanel 状态显示与操作；MenuView 菜单绘制；TitleBackdrop 固定标题镜头与光照 |
| Scripts/Data | CsvReader；各配置读取器、数据类型和资源映射类型 |
| Prefabs/Characters | Plane 角色根预制体、LowWing 低翼飞机模型预制体 |
| Prefabs/Buildings | 18 个旧地形建筑变体及 13 个日式独立建筑；Residential/Commercial 分类新资产；Shared 为旧变体依赖 |
| Prefabs/World | 道路、植被、道具及 Shared 基础资源 |
| Prefabs/UI | TitleBackdrop：预先摆好的标题建筑、道路、飞机、独立镜头与日景光源 |
| Art | Sprites、Meshes、Materials、Shaders |
| Data/Tables | buildings、terrains、weather_fx 三张规则表 |
| Data/Profiles | 地形昼夜表现及资源引用 |
| Data/Catalogs | 建筑键和预制体映射、地形 id 和 Profile 映射、各地形 WorldKit |
| Settings/Rendering | MainPipeline，项目默认管线和各质量等级共用 |
| Editor/Tools | 建筑预制体生成、固定标题摆放；接入 Main，不另建正式场景 |
| Editor/Validation | 项目结构、配置、飞行、天气和打包检查 |

目录按实际内容建立；正式 UI 预制体放 Prefabs/UI。标题和飞行共用 Main，标题期间不生成世界；选择开始或读档后才准备所选旅程，详见 [标题与运行设置](TitleMenu.md)。

## 运行流程与边界

1. GameEntry 检查引用、读取三张表，将建筑键对应到预制体，将 TerrainRule 和 TerrainProfile 按 id 对应成 TerrainDefinition，然后连接各模块。
2. FlightController 在巡航时报告距离增量。JourneyController 只处理距离、路线、进入地形与进入天气，不再自行计时。
3. TerrainStreamer 维护上一段、当前段和后两段；BlockGenerator 按所属路段的地形、天气及时段快照生成道路与建筑。
4. 环境系统按当前地形、天气、时段改变表现，不重建地块。窗灯通过 BuildingView 的明确引用控制，不依赖物体名字。

巡航时地面固定；跨段时飞机、地块和镜头同步移动坐标原点，保持画面连续和长距离精度。

TerrainProfile 只保存 id、资源和美术表现，距离、权重和天气只在 terrains 表维护。BuildingData 一次解析统一建筑规则，再交给各地块。CsvReader 负责通用语法，各读取器校验自己的列和规则；天气读取器不再依赖建筑读取器。天气 Shader 和表格通过 GameEntry 引用，移除了旧 Resources 路径依赖。

## 迁移与后续协作

原 OnAirPrototype、CityBlock、Journey 的有效内容合并到统一目录。旧原型场景和 SampleScene 统一为 Main。旧 BuildingSpawner、PlaneView、EnvironmentView 和场景预览管线覆盖脚本已淘汰，仍被变体引用的基础资源继续保留。移动资源保留 `.meta` 和 GUID。

并行验证场景放 Scenes，用 `_Temp_任务名` 命名，不加入打包；交付前合入有效修改并删除临时场景。不保留平行版本目录。

## 验证入口

- 菜单 `OnAir > Validate project`：场景/打包入口、缺失脚本、配置、窗灯、占地和渲染检查。
- `ProjectValidation.RunFlight`：静态检查和实际飞行、镜头、盘旋、暂停、跨段、天气/昼夜保留建筑及配置重载。
- `WeatherValidation.Run`：粒子、过渡、暂停、非法配置拒绝及恢复。
- `AircraftValidation.Run`：新低翼模型的显示、航向、螺旋桨暂停和场景/近景截图。
- `ProjectValidation.Build`：Windows Development Player 打包。

上述批处理入口用于隔离 Unity 实例；运行时验证会结束所用编辑器进程。日常可使用静态菜单检查。
