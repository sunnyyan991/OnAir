# OnAir

当前飞机：Main 使用原 LowWing 小飞机。第二视角暂时关闭并保留进度；第一视角使用已确认的 12 款云形和三层运行云，统一颗粒度，支持云海飞行与穿云。A330 及涂装已归档。详见 [分层云说明](Docs/CloudAssetApproval.md) 与 [三层云更新](Docs/CloudLayers.md)。

面向副屏的日式城市飞行氛围游戏：飞机持续前进，城市、河流、森林与山地在下方滚动，并显示现实时间。

## 打开与运行

Unity 工程在 `OnAir/`，版本为 `6000.3.25f1`，使用 URP `17.3.0` 与原生 Render Graph。打开唯一正式场景 **`OnAir/Assets/Scenes/Main.unity`**，点击 Play。

- 启动进入标题：START 选槽开始新旅程，LOAD GAME 读取继续。NEW SEED 换地图并保留上一段旅程，PREVIOUS FLIGHT 返回上一段旅程；EXPLORE 快速预览群系。
- 选槽界面可用 DELETE SAVE 删除占用槽并确认；不兼容或损坏存档同样可删除。世界配置更新前的旧档目前不自动迁移，原文件保留，详见 [存档兼容说明](Docs/SaveSystem.md#损坏与兼容)。
- 标题使用预先摆好的固定背景，不随机生成地图，也不预览存档位置。开始或读档时才显示地图准备进度，完成后进入飞行；仍只使用 Main 场景。
- 原生分辨率连续渲染；已移除 PIXEL 2X / PIXEL 3X 整屏颗粒化路径。
- 日式建筑和树木使用独立物体像素外观，保留真实遮挡、昼夜和窗灯；城市沙盒可分别管理 CBD 与住宅区的建筑池。
- 飞机居中，沿世界正 Z 轴前进；正常巡航固定在绝对高度 130 米，不随地形爬升，保留轻微游移和翼灯。机场阶段使用专用起降与地面服务流程，离场后恢复巡航。
- 自动天气按飞机所在区域的气候权重切换，机场沿用 City 气候；新行程可选择主题，当前各主题共用建筑资源。TIME OF DAY 使用像素旋钮连续选时，拨动后从该时刻继续自动推进。
- 地图按镜头覆盖和前方预加载；历史航程保存重建数据，不保留所有经过的场景对象。

## 当前说明

- [小机场布局、起降与地勤流程](Docs/AirportFlightCycle.md)
- [机场地面画线规范](Docs/AirportGroundMarkings.md)
- [机场与飞机灯光规则](Docs/AirportLighting.md)
- [城市分区与资产沙盒](Docs/CitySandbox.md)
- [小型飞机资产与停放接口](Docs/SmallAircraftAssets.md)
- [固定巡航高度与镜头锚点](Docs/AircraftHeightPreview.md)
- [Unity 6.3 升级与验证记录](Docs/UnityUpgrade6000.3.25f1.md)
- [自动存档与恢复](Docs/SaveSystem.md)
- [标题、存档选择与运行设置](Docs/TitleMenu.md)
- [项目全盘进度](Docs/ProjectStatus.md)
- [配置与操作](Docs/Configuration.md)
- [原生渲染、资产清理与性能验证](Docs/NativeRenderingCleanup.md)
- [像素材质核查与后续美术制作](Docs/PixelMaterialWorkflow.md)
- [像素片体架构自查、第二轮绘制与随机夜灯](Docs/PixelArtV2.md)
- [建筑共创工作流](Docs/BuildingArtWorkflow.md)
- [连续地图与加载](Docs/ContinuousWorld.md)
- [项目结构](Docs/ProjectStructure.md)

## 文件管理

道路车辆、绿地神社 / 公园 / 球场已接入，见[生成与资产说明](Docs/CityLife.md)。启动耗时的测量结果与后续执行重点见[性能诊断](Docs/PerformancePriorities-2026-10-04.md)。

运行资源统一在 Unity `Assets` 中，规则表、资源映射和渲染表现分开维护。`Docs/` 保留说明，根目录 `Art/` 保存美术原稿、验证截图和退役资产归档。

运行所需资源已烘焙并纳入仓库。本机生成工具、`Art/` 审查资料和临时文件不随 Git 提交；文档中的本地审查链接需在原工作机查看。共享检查入口为 `ProjectValidation.Validate`，扩展飞行截图工具为本机可选项。

本轮归档位于 `Art/Archive/2026-09-24-retired-city/`，保留 GUID、引用审计和恢复所需的配置快照。不会删除现有建筑仍依赖的共享素材。历史 V01–V14 文档描述各阶段状态；与当前操作冲突时，以本 README 和 Configuration 为准。

临时场景使用 `_Temp_任务名`，不加入打包，验证后清理。Main 始终是唯一正式入口。
