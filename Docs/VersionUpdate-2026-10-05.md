# OnAir 2026年10月5日版本更新说明

本次更新把原来的分段飞行演示扩展为连续世界原型，重点增加日式城市资产、逐物体像素表现、三层云、城市交通和绿地设施，并调整飞行、天气与昼夜操作。随后完成引擎升级、原生 Render Graph 云影适配和自动存档接入。

下方“连续世界”内容统计 `0e07db3 → de01835` 的已提交变化；该部分的“已接入”指代码、资源映射及正式场景的静态核对结果，整理时没有启动 Unity，也没有复做协作者的性能或画面验收。引擎升级与存档是其后的补充，验证结果来自对应任务保留的记录；本次提交整理复核这些记录，没有重复运行 Unity。

## 本次补充：引擎升级与自动存档

- 工程升级到 Unity `6000.3.25f1` / URP `17.3.0`，同步包依赖、项目设置、材质迁移结果和默认 Volume Profile。正式场景仍为 Main，既有资源 GUID 保留。
- 云影切换到原生 Render Graph，通过图内纹理发布太阳空间投影；无云或无直射光时清空投影，保留现有云影强度、像素表现与昼夜规则。
- 启动自动继续当前旅程，保存航程、飞机游移进度、天气、昼夜、暂停状态、附近建筑布局和已发布的高架道路。默认每 30 秒保存，切到后台或正常退出时保存；离线不推进旅程。
- NEW SEED 保留上一段旅程，PREVIOUS FLIGHT 返回上一段。面板设置独立保存；主档损坏时尝试备份，不可读或不兼容的旧档保留并停止自动覆盖。
- 已有记录包括云影路径对照、Main 短时检查、Windows 构建及运行、跨进程存档恢复、上一旅程切换和坏档保护。没有验证完整巡航、多实例共用存档、真实断电或其他平台。

实现、使用方式与验证边界见 [引擎升级记录](UnityUpgrade6000.3.25f1.md)、[存档说明](SaveSystem.md) 和 [配置与操作](Configuration.md)。私人检查脚本、日志、存档样本和打包产物按现有忽略规则留在本地。

## 连续世界改动的对比范围

| 项目 | 内容 |
| --- | --- |
| 原版本 | `0e07db369e4b182237ea2fe31e90cd83271e8197`，2026年9月8日，sunnyyan991，Update git.ignore |
| 新版本 | `de0183510edf8d9f6a6c37423ef11fa995e82397`，2026年10月5日，mobypear-coder，整合当前运行版本与协作文档 |
| 范围 | 原提交不计入，本次只有一个新提交；以两个版本的最终文件差异为准 |
| 文件变化 | Git 默认统计为 5,508 条记录：新增 5,356，修改 114，删除 22，按相似度识别为重命名 16 |

其中 `.meta` 为 2,732 个、`.asset` 为 2,026 个、`.mat` 为 554 个。它们包含资源身份、网格、材质和多方向烘焙数据，不能按文件数算成几千款独立新素材。Git 的相似度匹配也不等于 Unity 资源移动或继承了原 GUID；不做重命名检测时，同一差异为 5,524 个路径记录。

## 新增与调整的功能

### 连续地图与群系预览

- 正式 Main 开启连续世界模式。新增二维地形规划、生态混合、水系、桥梁、绿地和道路模块，替代旧版主要按航程切换地形段的表现。
- 新增城市、镇区、海滨镇、森林、河流平原、丘陵、海洋七类生态区域，包含海岛、岸线、森林与部分农地。
- 新增 EXPLORE 菜单，可选择群系快速预览；左上角显示当前主要群系的名称。预览会直接向前跳转并增加路线里程，因此显示数字不全是自然飞行积累。
- 新增按镜头覆盖范围加载、前方预加载、后方回收，以及历史航程的重建信息。历史信息主要用于地图重建，尚未形成玩家可用的收藏或回放功能。

证据：`Main.unity`、`ContinuousWorldPlan.cs`、`ContinuousWorldLayout.cs`、`TerrainStreamer.cs`、`BiomeNavigator.cs`。

### 飞行与镜头

- 巡航速度由 12 米/秒调整为 24 米/秒；取消自动盘旋和手动盘旋入口，飞机持续前飞并保留轻微游移、倾斜和螺旋桨表现。
- 摄像机直接保持飞机居中，不再执行旧版平滑跟随；新增机翼信号灯支持，以及飞机尺寸、地形净空相关逻辑。信号灯的实际表现还取决于所用模型的 Rig 配置。
- 当前正式场景继续使用 LowWing 小飞机。A330、涂装和机舱视角的资源与代码已入库，但机翼视角试验开关默认关闭。
- 当前固定高度预览开启：飞机视觉模型为 130 米，飞行根节点和镜头锚点为 65 米。源码中保留的地形爬升逻辑被该模式提前返回绕过，不能仅根据旧说明称它正在自动爬升避障。

证据：`GameEntry.cs`、`FlightController.cs`、`CameraFollow.cs`、`AircraftHeightPreview.cs`、`Plane.prefab`。

### 天气与连续昼夜

- 新增独立自动天气调度。天气按独立计时和权重变化，不再依赖进入新地形触发；保留手动天气切换。
- TIME OF DAY 从逐个切换时段改为可拖动的连续像素旋钮。拨动后继续自动推进；暂停飞行也暂停相关时间推进。
- 新增逐栋稳定随机窗灯，窗户按夜间安排亮灭；太阳、月亮和环境光参与昼夜表现。

晴、阴、雨、大雨以及基础昼夜配色在基线已经存在，本次新增的是调度、连续变化和窗灯规则。

证据：`GameSession.cs`、`EnvironmentController.cs`、`WindowNightSchedule.cs`、`FlightPanel.cs`。

### 三层云与云影

- 新增云图集、12 款基础云形、分层运行调度和穿云表现。大云海使用同颗粒度的云块组合。
- 当前运行三层云，候选组共 16 个；第三层复用四款既有云形，并有独立的两个活动名额，不代表新增了四款基础素材。
- 新增云片的入场、退场和天气切换连续处理，以及大雨雷光表现。
- 新增接收云影的 Lit Shader，使建筑、道路等表面在正常受光过程中接收云影。

证据：`LayeredPixelClouds.cs`、`CloudCatalog.cs`、`CloudShadowOverlay.cs`、`Resources/PixelClouds`、`CloudReceivingLit.shader`。

### 城市交通与绿地设施

- 新增轻量装饰交通，车辆沿地面道路和已生成高架移动，支持车距减速、转弯减速、车身配色及昼夜前后灯。
- 当前车辆池上限为 96，高架车辆上限为 32；实际数量受有效道路与生成条件限制，不是每屏固定显示 96 辆车。
- 新增微型社区神社、寺院庭院、篮球场、足球练习场等绿地设施；公园按符合条件的街道围合绿地生成步道、长椅与灯具。
- 绿地设施通过专用规则投放，街道抽选权重为零不等于停用。旧固定公园预制体保留，但默认公园改为程序化生成。
- 当前启用独立高架走廊规划器，验证完整进出口、建筑和地形净空，再发布道路。两套旧高架规划器仍留在源码中，但被当前模式抑制，不能将历史双层高架说明直接视为当前默认行为。

证据：`CityTraffic.cs`、`WorldGreenSpaces.cs`、`WorldParkPlans.cs`、`CityParkGeometry.cs`、`WorldLayerHighways.cs`、`CityWorldKit.asset`。

### 建筑与像素表现

- 扩充公寓、低层集合住宅、独栋住宅、商店、商业综合体、办公楼和公共设施，并增加尺度、占地、临街面、颜色与分布规则。装饰槽目前是制作元数据，未形成运行时装饰生成。
- 新增逐物体的四方向像素数据与渲染组件，配合照明、深度和随机窗灯；原模型仍作为制作源或阴影来源。
- 新增商业建筑的招牌、动态图屏幕支持和广告内容资产。秋叶原五款建筑各保留三个版本，当前 catalog 选择 R02；新增六份广告内容资产的直接引用集中于基础版和 V02，不能将它们算成当前已启用的随机广告池。

证据：`BuildingCatalog.asset`、`buildings.csv`、`PixelObjectArt.cs`、`PixelObjectView.cs`、`BuildingDiversityPicker.cs`、`CommercialScreenSet.cs`、`AnimatedBillboardSurface.cs`。

### 加载与性能

- 增加世界计划、人口与资产尺寸缓存、空间查询、高架规划分帧、地图生成预算和计时统计。
- 窗灯与像素表现主要在新对象发布前准备，减少重复初始化；部分材质、网格和临时容器共享或复用。
- 新增独立 FPS 显示。首屏、换种子、道路登记和大型生成步骤仍有同步工作，不能据此称为无卡顿版本。

协作者文档记录过性能改善，但比较的是开发过程中的不同状态，且相关原始日志未随本提交交付。本说明不把这些数字写成相对 `0e07db3` 的实测收益。

证据：`TerrainStreamer.cs`、`WorldPopulationCache.cs`、`WorldLayerHighways.cs`、`FrameRateDisplay.cs`、`CityLifeV2.md`、`PerformancePriorities-2026-10-04.md`。

### 素材制作与项目资料

Docs 新增 31 个文件、修改 3 个文件，补充建筑用途编号、尺寸与占地、颜色、光照、装饰、像素密度、资产替换与制作流程，并交付资产登记表和编号迁移表。也补充了连续世界、云层、交通、阶段性能诊断和按风险验证的说明。

这些资料包含多个开发阶段，部分内容已经被后续代码替换。运行状态应以本次最终源码和映射为准，制作标准、阶段记录与当前操作说明应分别使用。

## 素材与配置的数量

新增建筑按独立设计计为 **57 款：33 款住宅、24 款商业**，对应 67 个新增预制体文件。秋叶原五款各保留基础版、V02、R02，多出的十个版本不重复算款式。原有六款旧 City 建筑预制体被删除；海岸、沙漠和 Shared 资源仍保留。

| 新增素材组 | 独立设计数 | 交付及启用情况 |
| --- | ---: | --- |
| JP 日式建筑 | 13 | 6 款住宅、7 款商业；其中 11 款普通抽选权重为零 |
| A／B／C 住宅系列及 ReferenceHomes | 27 | 10 款公寓、12 款低层／独栋住宅、5 款参考住宅 |
| D 店铺／餐饮 | 6 | 普通城市建筑池 |
| 秋叶原商业 | 5 | 2 款店铺、3 款商场；当前选用 R02 |
| Highrise 商业 | 6 | 3 款商场、3 款办公楼 |
| 绿地设施 | 5 | 2 款寺社、3 款公园／运动设施；固定口袋公园被跳过，其余 4 款按条件投放 |
| A330300 | 1 | 另有 Navy、Wine 两套涂装和两张机舱纹理；未替换默认飞机 |
| 云形 | 12 | 组合为 16 个运行云组，第三层复用既有轮廓 |
| 广告内容 | 6 | 内容数据，直接引用在未选用的旧商业楼版本中 |

另有 **74 份逐物体像素烘焙包**，对应 67 个新增建筑版本、5 个绿地设施和2种原有城市树。原有树的烘焙接入属于表现升级；七类生态区域主要由代码和程序几何实现，不能称为新增七套独立地形美术。

建筑规则表从基线的 18 条扩展至 74 条：城市 62 条，其中 46 条抽选权重大于零、16 条为零；另有 12 条海岸或沙漠旧模式兼容规则。权重为零的记录既包括被替换的旧款，也包括专用生成设施，不能统一算成停用资产。当前连续世界主要按 City／Clear／Day 条件选择建筑，旧模式兼容规则没有在主模式随机轮换。

资产登记表有 63 项，记录状态分别为：49 项已入池、2 项已制作待实景验收、6 项待效果图验收、6 项规划中。登记表含建筑、广告与交通等类型，不能称为 63 栋已经投放的建筑；登记状态还需与实际 catalog 引用和生成规则结合判断。

主要运行资产位置：

| 对象 | 位置与内容 |
| --- | --- |
| 建筑 | `Assets/Prefabs/Buildings` 与 `Assets/Art/Buildings`，独立楼型、网格和材质 |
| 像素资产 | `Assets/Art/PixelObjects`，多方向颜色、照明、深度与窗户标记等派生数据 |
| 飞机 | `Assets/Art/Aircraft` 与 `Assets/Prefabs/Characters`，A330 与涂装资源入库；Main 保留 LowWing |
| 云 | `Assets/Resources/PixelClouds`，图集、目录、材质及接收云影的 Shader |
| 绿地与装饰 | `Assets/Art/CityLife`、`Assets/Art/Decorations` 与相关预制体，寺社、运动场和广告内容 |
| 规则与映射 | `Assets/Data/Tables`、`Assets/Data/Catalogs`、`Docs/BuildingAssetRegistry.csv` |

## 沿用的能力与未完成内容

现实时钟、飞行暂停、NEW SEED、RELOAD CSV、里程显示、基础天气粒子、主场景入口和 LowWing 模型在基线就已存在，应视为延续或增强。本次也不能将开发过程里“移除整屏像素化”的记录当成两个端点之间删除了一项原本存在的功能：基线没有该菜单和脚本。

以下内容尚不能作为当前已完成功能交付：

- A330 与机舱／机翼视角正式启用。
- 风景收藏、完整历史飞行记录、回放与地貌解锁。自动保存当前及上一段旅程已在本次补充中接入。
- 完整交通信号和交通仿真。
- 尚在规划或评审中的建筑，以及成套季节、雪国、火山、温泉、工业港区等区域素材。
- 长时间副屏功耗、实际 Player GPU 表现与所有卡顿消除的验收结论。

## 交付缺项与说明冲突

1. README、Configuration 和部分阶段文档仍保留“13 栋建筑／25 条规则”等旧数字；以当前表、catalog 与源码为准。
2. 部分历史文档描述双层高架和地形自动爬升，当前则启用独立高架规划及固定视觉高度预览。后续应同步正式操作说明。
3. 根目录 `Art/` 没有跟踪进本提交，文档中的素材浏览页、截图、验收日志和归档恢复清单未随提交交付；部分历史文档链接也缺失，例如 `Docs/WorldV14.md`。现有说明提到的 Windows 验证版不能仅凭此次拉取保证获得。
4. 旧资产生成工具已删除，但新增文档提到的建筑制作、像素烘焙和验收工具多数没有入库。运行资产存在，不代表制作和验证流程能够在新电脑上复现。
5. 旧 `ProjectValidation.cs` 仍要求 18 条建筑规则及首条 ID 101，`FlightValidation.cs` 仍要求盘旋和四个地形块，与当前规则冲突。旧 RunFlight／Build 工具会先调用这些检查，不能直接作为当前版本的有效验证或打包工具；本次没有运行它们。
6. EXPLORE 搜索失败时有内部提示，但面板没有显示该提示，属于反馈缺口；LEG 与 “Next terrain in” 仍采用兼容航段含义，不代表真实群系编号或下一群系距离。

静态资源检查未发现 3,050 个 Assets GUID 重复，也未发现删除的 19 个旧 GUID 在目标资源中残留引用；已跟踪的实际资产均有 `.meta`。这些检查不覆盖 Unity 导入、编译、Shader 变体或实机画面。实际运行入口仍为 Unity `2022.3.62f3c1` 的 `OnAir/Assets/Scenes/Main.unity`，本次没有升级 Unity、修改依赖包或新增正式场景。

## 主要证据入口

| 内容 | 文件 |
| --- | --- |
| 正式场景与模块连接 | [Main](../OnAir/Assets/Scenes/Main.unity)、[GameEntry](../OnAir/Assets/Scripts/Game/GameEntry.cs) |
| 连续世界与加载 | [ContinuousWorldPlan](../OnAir/Assets/Scripts/World/ContinuousWorldPlan.cs)、[TerrainStreamer](../OnAir/Assets/Scripts/World/TerrainStreamer.cs) |
| 建筑规则与资源映射 | [建筑表](../OnAir/Assets/Data/Tables/buildings.csv)、[BuildingCatalog](../OnAir/Assets/Data/Catalogs/BuildingCatalog.asset)、[资产登记表](BuildingAssetRegistry.csv) |
| 交通与高架 | [CityTraffic](../OnAir/Assets/Scripts/World/CityTraffic.cs)、[WorldLayerHighways](../OnAir/Assets/Scripts/World/WorldLayerHighways.cs) |
| 飞行与高度 | [FlightController](../OnAir/Assets/Scripts/Flight/FlightController.cs)、[AircraftHeightPreview](../OnAir/Assets/Scripts/Flight/AircraftHeightPreview.cs) |
| 气候与云 | [GameSession](../OnAir/Assets/Scripts/Game/GameSession.cs)、[LayeredPixelClouds](../OnAir/Assets/Scripts/Environment/LayeredPixelClouds.cs)、[云影说明](CloudShadowContinuity.md) |
| 像素与商业表现 | [PixelObjectView](../OnAir/Assets/Scripts/World/PixelObjectView.cs)、[CommercialScreenSet](../OnAir/Assets/Scripts/World/CommercialScreenSet.cs) |
| 当前验证工具的限制 | [ProjectValidation](../OnAir/Assets/Editor/Validation/ProjectValidation.cs)、[FlightValidation](../OnAir/Assets/Editor/Validation/FlightValidation.cs) |
| 阶段记录及其限制 | [CityLifeV2](CityLifeV2.md)、[性能诊断](PerformancePriorities-2026-10-04.md)、[项目进度](ProjectStatus.md) |
