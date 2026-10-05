# 标题与运行设置

Main 仍是唯一正式场景。启动停在标题，背景不累计航程、不读成正在玩的旅程，也不自动写玩家槽位。所有新界面文字使用英文，CREDITS 只在标题页，署名 Moby、Kisa。

标题先显示，城市背景分帧准备。没有可用存档时使用固定种子 `GameEntry.titleBackgroundSeed`（默认 2409）的起点画面；有存档时按保存时间选择最近一次成功保存的兼容快照，恢复位置、飞机姿态、天气和时段作为静止预览，不绑定活动槽。坏档、不兼容档保留原文件并跳过；没有兼容快照则使用固定起点。返回标题时同样读取已落盘的快照，不把未保存位置当作标题背景。

准备背景期间标题与按钮可用，完成前遮住未生成完的地图；点击 START / LOAD 可以取消背景准备，改为准备所选旅程。PREPARING 页面显示读取或创建、生成地图、保存三个阶段；生成进度按实际完成地块占所需地块的比例换算为百分比。可见区域、短距离预加载和首轮道路发布完成后才显示地图并进入飞行。后台分帧不减少生成内容，单帧生成步骤仍可能超过预算；超时或错误会停止当前准备并反馈，活动旅程可以 RETRY / BACK。标题子页返回会复用背景。

## 玩家流程

- START：进入 NEW FLIGHT 列表，默认选择空位。占用位需要 OVERWRITE THIS SAVE? 确认，取消不动文件；准备与初始保存成功后才进入运行。失败可 RETRY / BACK，重试已准备好的同一旅程，不重新随机。
- LOAD GAME：选择槽位，再点击 LOAD，恢复原旅程。坏档和不兼容存档留在列表中提示；空位不能读取。无存档时提供 NEW FLIGHT / BACK。
- SETTINGS：标题与运行中共用显示设置。
- QUIT：标题退出需确认，默认 CANCEL。运行中先处理未保存变化。

原 FlightPanel 的时钟、群系、SEED、FLIGHT CONTROL、WORLD STATUS、FPS、PREVIOUS FLIGHT 和环境控件位置保留。仅在 SEED 下增加独立 SETTINGS + / -，Flight Control 折叠后仍能使用。

展开 SETTINGS 有 SAVE GAME / DISPLAY / BACK TO TITLE / QUIT。展开与 DISPLAY 不暂停飞行；点击空白收起下拉并消费该次输入。离开确认临时冻结航程、气候、螺旋桨、飞机灯、云、降水及车辆，取消恢复玩家原有暂停状态。临时暂停不写入存档。

存在未保存变化时，返回标题选择 SAVE AND RETURN / RETURN WITHOUT SAVING / CANCEL；退出选择 SAVE AND QUIT / QUIT WITHOUT SAVING / CANCEL。保存失败留在确认框，不离开。不保存离开先收尾已经发起的写入，之后停止自动保存及失焦、应用暂停、退出补存，保留最近成功的快照。

Esc 一次只关闭最上层：显示模式确认回退、离开/覆盖/退出确认取消、DISPLAY 返回、群系选择关闭、SETTINGS 收起、标题子页返回。确认框的默认焦点为 CANCEL；Enter 在未另选时取消危险操作。

## 存档与兼容

GameEntry.saveSlotCount 默认 3，可配置 1–8。每个槽位独立维护 journey.json、journey.bak、previousJourney.json。默认 30 秒、玩家操作后的延迟保存和生命周期保存仍保留；NEW SEED 先保护上一旅程，再正常保存新旅程。

```text
Saves/Editor 或 Saves/Player/
├── settings.json         原面板偏好，所有槽共享
├── display.json          独立显示偏好
├── Slots/01/             正常槽 01
├── Slots/02/
└── Slots/03/
```

减少配置槽数不会删除旧槽，仍显示范围内实际存在的旧槽。旧版本根目录 journey.json / journey.bak 和 previousJourney.json 保留原样，列表显示 LEGACY FLIGHT / PREVIOUS FLIGHT 兼容入口。显式读取后，在 Slots/LegacyCurrent 或 Slots/LegacyPrevious 保存；原文件不迁移、删除或覆盖。滚动备份不会作为单独的新档位。

存档格式仍为 1，仅增加可选的保存时群系名。使用原 SaveFileStore 的临时写入、替换、校验与备份；更高格式、不同生成器或配置不兼容时保持原文件，不静默回退。地图准备和成功落盘之前，不删除占用槽原主档。

显示偏好与旅程分开写入。WINDOW MODE 有 WINDOWED / BORDERLESS，预览后提供 KEEP / REVERT，15 秒未确认自动回退，其他显示偏好的保存不会携带未确认的模式。UI SCALE 为 0.75 / 1 / 1.25 / 1.5，按可用窗口限制实际大小以避免裁切。FRAME LIMIT 可选 30 / 60 / UNLIMITED；首次显示 DEFAULT 表示沿用原引擎行为，不主动改变帧率。RESET DEFAULTS 恢复启动时的显示行为。

## 程序职责

| 模块 | 职责 |
|---|---|
| GameEntry | 初始化与模块连接，Main 场景入口 |
| SaveSlots | 只读列槽、元数据、兼容旧存档的路径映射 |
| SaveController | 只读标题快照、当前槽绑定、准备/提交、自动与显式保存、备份、上一旅程和兼容保护 |
| MenuController | 首帧标题、背景与活动旅程准备、确认、临时暂停与离开交接 |
| TerrainStreamer | 有预算的初始地图准备、真实地块进度、取消与正常流式加载 |
| MenuView | OnGUI 绘制及界面输入层级 |
| DisplayController | 原显示默认值、模式预览回退、独立偏好读写 |
| FlightPanel | 原 HUD 与共用像素字、面板、按钮样式 |

GameSession.paused 是玩家状态；menuPaused 是临时控制状态，不序列化；SimulationPaused 汇总两者给模拟与视觉更新使用。未新建正式场景或启用实验飞机视角。

TerrainStreamer 的 BeginPreparation / CancelPreparation 控制初始准备。取消后挂起地图生成，避免正常加载的同步补齐继续完成已放弃的地图。初始地块网格先分帧完成，再推进独立道路规划与发布，保证网格迭代期间道路列表稳定；完成后恢复原有巡航流式加载和安全补齐。SaveController 的只读标题预览不改变当前槽绑定或开启自动保存。

## 验证记录

2026-10-05 首屏加载更新：独立 Windows Player、隔离存档、固定新旅程种子 1372。最终初始化检查 29 项、最近存档与读档检查 16 项通过，0 个运行错误。覆盖第一个标题帧零地块、固定起点、背景中途取消并开始、真实中间进度、不保存返回落盘位置、精确读档、准备后保存失败重试、标题子页复用背景，以及最近兼容快照和异常文件保留。既有存档含 7 段已发布道路。实际截图采用 `local-work/startup-flow-2026-10-05/flow-sequence-evidence/` 与 `saved-sequence-evidence/`。

1280×800 Development Player 测得标题首帧约 2.66–2.76 秒；固定背景随后约 13.2 秒准备完成，所测存档背景约 2.4 秒。时间来自 Player 的运行计时，不能当作 Editor 点击 Play 含编译与域重载的总等待时间。单个生成步骤仍可能超过预算，本轮最高约 44.5 ms。未验证长时间巡航、真实磁盘满或断电。详细原始记录与限制见 `local-work/startup-flow-2026-10-05/program-report.md`。

正式 Windows x64 包更新为 `Builds/Windows/OnAir-2026-10-05-Startup-Win64.zip`，保留旧包。非 Development 构建，0 错误、2 条既有警告；最终源码同步、Main 与版本未改、移除测试脚本、ZIP 完整性通过。下列菜单与单独标题外观记录为此前阶段的验证。

最终正式包另做 16 秒有界启动观察：未报运行异常，标题未创建旅程文件或 Slots 目录；测试进程已关闭。

2026-10-05 标题外观后续调整：按用户新选定参考图替换 OnAir 字标、虚线航迹与小飞机，以及深青切角按钮、双层细边、淡金箭头和悬停表现。修改仅在 MenuView 的标题绘制中，菜单流程、运行 HUD 和存档规则沿用已验证实现。使用当前工程的 Unity 编译器及程序集引用编译全部运行脚本通过；独立标题元素预览检查了默认、悬停与缩小后的比例。详见 `local-work/title-style-2026-10-05/validation.md`。本次没有重新运行 Main 或打包，下文 Windows 包和实机截图属于此前功能交付版本。

独立 Unity 工程、固定种子 1372、显式隔离 -onairSaveDirectory。编译与 Main 引用通过；有界 Main 流程使用真实 MouseDown / MouseUp GUI 事件验证 DISPLAY 的 UI SCALE / BACK、SAVE AND RETURN / CANCEL 等，覆盖槽位、损坏、不兼容、重试、离开回调和临时暂停。详细输入与截图记录保留在 local-work/ui-implementation-2026-10-05/program-report.md。

Windows Player 验证通过：1280×800 窗口实际切到 3200×2000 无边框，未确认等待 15.003 秒后恢复原模式与尺寸，display.json 仍为原偏好。运行无错误，测试进程已退出。

实际 Player 照片在 `local-work/ui-implementation-2026-10-05/visual-evidence/`，覆盖标题、运行 SETTINGS、折叠后的独立入口、选槽、DISPLAY、离开确认与署名，包含 1280×800 和 640×480。该目录照片方向、背景正常；原离屏截图与隐藏启动生成的黑图不用于验收。正式交付包结果见实施记录。

补充局部检查：选择空槽时 LOAD 禁用样式与可执行按钮区分；时间旋钮拖到 SETTINGS 内松开后正确释放鼠标捕获，随后 DISPLAY / SETTINGS 仍能接收实际点击。菜单消费释放事件前，由 FlightPanel 处理自己捕获的旋钮，避免下层 OnGUI 被跳过而无法收尾。

2026-10-05 正式 Windows x64 包：`Builds/Windows/OnAir-2026-10-05-Menu-Win64.zip`。非 Development 构建，0 错误、2 条既有警告；ZIP 完整性、最终源码同步和 Main 未改检查通过。最终包独立启动 12 秒无运行错误，标题没有创建旅程或槽目录。未验证长时间巡航、多实例共用目录、真实磁盘满或断电。
