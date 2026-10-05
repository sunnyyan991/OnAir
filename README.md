# OnAir

当前飞机：Main 使用原 LowWing 小飞机。第二视角暂时关闭并保留进度；第一视角使用已确认的 12 款云形和三层运行云，统一颗粒度，支持云海飞行与穿云。A330 及涂装已归档。详见 [分层云说明](Docs/CloudAssetApproval.md) 与 [三层云更新](Docs/CloudLayers.md)。

面向副屏的日式城市飞行氛围游戏：飞机持续前进，城市、河流、森林与山地在下方滚动，并显示现实时间。

## 打开与运行

Unity 工程在 `OnAir/`，版本为 `6000.3.25f1`，使用 URP `17.3.0` 与原生 Render Graph。打开唯一正式场景 **`OnAir/Assets/Scenes/Main.unity`**，点击 Play。

- 有存档时启动自动继续旅程；首次运行使用新种子。NEW SEED 换地图并保留上一段旅程，PREVIOUS FLIGHT 返回上一段旅程；EXPLORE 快速预览群系。
- 原生分辨率连续渲染；已移除 PIXEL 2X / PIXEL 3X 整屏颗粒化路径。
- 现有 13 种日式建筑和两种树木已接入独立物体像素外观，保留真实遮挡、昼夜和窗灯。原始模型继续作为制作源；像素美术细节仍需打磨。
- 飞机居中，约 30° 屏幕航向，速度 24 米/秒；无自动盘旋，有轻微游移、翼灯及地形安全高度。
- 天气独立随机；TIME OF DAY 使用像素旋钮连续选时，拨动后从该时刻继续自动推进。
- 地图按镜头覆盖和前方预加载；历史航程保存重建数据，不保留所有经过的场景对象。

## 当前说明

- [Unity 6.3 升级与验证记录](Docs/UnityUpgrade6000.3.25f1.md)
- [自动存档与恢复](Docs/SaveSystem.md)
- [项目全盘进度](Docs/ProjectStatus.md)
- [配置与操作](Docs/Configuration.md)
- [原生渲染、资产清理与性能验证](Docs/NativeRenderingCleanup.md)
- [像素材质核查与后续美术制作](Docs/PixelMaterialWorkflow.md)
- [像素片体架构自查、第二轮绘制与随机夜灯](Docs/PixelArtV2.md)
- [建筑共创工作流](Docs/BuildingArtWorkflow.md)
- [镜头驱动的地图加载](Docs/WorldV14.md)
- [项目结构](Docs/ProjectStructure.md)

## 文件管理

道路车辆、绿地神社 / 公园 / 球场已接入，见[生成与资产说明](Docs/CityLife.md)和[日夜实物图](Art/Reviews/CityLife/index.html)。启动耗时的测量结果与后续执行重点见[性能诊断](Docs/PerformancePriorities-2026-10-04.md)。

运行资源统一在 Unity `Assets` 中，规则表、资源映射和渲染表现分开维护。`Docs/` 保留说明，根目录 `Art/` 保存美术原稿、验证截图和退役资产归档。

本轮归档位于 `Art/Archive/2026-09-24-retired-city/`，保留 GUID、引用审计和恢复所需的配置快照。不会删除现有建筑仍依赖的共享素材。历史 V01–V14 文档描述各阶段状态；与当前操作冲突时，以本 README 和 Configuration 为准。

临时场景使用 `_Temp_任务名`，不加入打包，验证后清理。Main 始终是唯一正式入口。
