# Unity 6000.3.25f1 升级记录

2026-10-05：由 Unity `2022.3.62f3c1` 升级到 `6000.3.25f1 (e1dba0a9aba4)`。正式工程仍为 `OnAir/`，唯一正式场景及打包入口仍为 `Assets/Scenes/Main.unity`。

## 升级内容

- Unity 自动迁移项目设置和包依赖；URP、Core、Shader Graph 使用 `17.3.0`，uGUI 使用 `2.0.0`。TextMeshPro 合入 uGUI，不再保留原独立包依赖。
- Unity 更新 IDE、Test Framework、Timeline 和 Visual Scripting 等已有包。原有 VS Code 包保留；编辑器提示它已停止维护，没有将这次升级扩大为工具清理。
- 云影 Pass 改用原生 Render Graph。关闭 Compatibility Mode，移除试验阶段的 `URP_COMPATIBILITY_MODE`；现有云影投影、渐隐、像素和日夜规则保留。接收材质的说明见 [云影连续性](CloudShadowContinuity.md)。
- 保留渲染管线和材质的自动迁移结果；新默认 Volume Profile 放在 `Assets/Settings/Rendering/`。迁移前后已有资源 GUID 保持不变。
- Unity 自动新增的 Multiplayer Center 包已通过 Package Manager 移除，本次没有接入多人服务。引擎生成的 MultiplayerManager 设置保留。
- 没有重烘焙像素资产，没有新增正式场景，没有改变地图或飞行规则。

本机目标编辑器位于 `D:/Software/UnityEngine/6000.3.25f1/Editor/Unity.exe`。协作者需要安装同版本后，通过 Unity Hub 用该版本打开 `OnAir/`。

## 验证范围与结果

先复制 Assets、Packages、ProjectSettings 在私人工作目录试升，不复制旧 Library；通过检查后只合回有效改动。合并前逐个检查原文件仍与备份一致，避免覆盖期间新增的修改。

| 检查 | 结果 |
| --- | --- |
| 新版导入、脚本编译 | 通过；首次复制工程导入约 87 秒 |
| 小场景像素建筑、地面、云影和昼夜 | 通过；云影改变接收表面亮度，像素建筑可见，未记录 Shader 编译错误 |
| 兼容路径与原生 Render Graph 对照 | 日景无云、日景有云、夜景三张 PNG 字节一致 |
| Main 固定种子短时运行 | 799 次更新，运行错误 0，可见缺块 0；飞机和相机分别移动约 33.9 米 |
| Main 切换夜景和雨天 | 已有地图保留，画面正常 |
| Windows Development 构建 | 成功，构建错误 0；首次构建约 128 秒 |
| Windows 独立程序 | 短时运行通过，运行错误 0、可见缺块 0，实际相机输出已查看 |
| 合入正式路径后的导入与编译 | 通过，约 120 秒，本地缓存已更新到目标版本 |
| 合入文件与资源身份 | 21 个工程文件与验证副本一致；核对 3,050 个既有 `.meta` 的 GUID，全部保留；Main 和打包场景清单未变 |

Main 检查中，从请求进入 Play 到地图首次 Update 约 **7.59 秒**，当时有 28 个驻留块、25 个需求块和 453 栋建筑。独立程序首次地图 Update 约 **5.28 秒**。这两个数来自不同的检查环境，不是严格的画面首帧指标，也不是升级前后的对照；不能据此宣布协作者反馈的“二十多分钟”已经修复。

没有进行完整巡航、多种子扫描、全资产重烘焙、其他平台构建或长期性能验证。独立程序画面采用相机直接输出验证；隐藏窗口的 ScreenCapture 返回黑图，未将该图作为画面证据。

## 检查中的临时问题

- 国际版 Package Manager 无法识别旧中国版版本串 `2022.3.62f3c1`。只在复制工程的首次升级入口将旧版标记规范为 `2022.3.62f3`，随后由目标编辑器生成真实的新版本号。
- 自动检查过早请求进入 Play，导致 Unity Search 默认索引未建立。按目标版本源码调整检查初始化顺序后通过；没有修改游戏代码或屏蔽异常。
- 隐藏的独立程序失焦会暂停。仅在临时检查组件中允许后台运行并读取相机输出；没有修改正式工程的后台运行设置。

临时检查组件没有合入正式 Assets。验证构建含按命令行参数启用的临时检查组件，仅用于私人验证，不作为正式发行包。

## 本地记录与恢复

私人记录在 `local-work/unity-upgrade-6000.3.25f1/`，由仓库本地 exclude 忽略：

- `before.zip`：升级前完整 Assets、Packages、ProjectSettings 备份；不包含 Library。
- `baseline-hashes.json`、`merged-files.json`：原文件摘要与合入文件清单。
- `import-2.log`、`enable-graph.log`、`graph-render.log`、`main-3.log`、`build.log`、`player-render-2.log`：通过的主要检查日志。早期失败日志同时保留，不与通过结果混用。
- `original-import.log`：合入正式路径后的导入与编译日志。
- `final-audit.json`：合入文件一致性、既有 GUID、正式场景和打包清单核对结果。验证通过后清理试升工程副本，检查源码另存于 `harness/`。
- `evidence/main-day.png`、`main-night-rain.png`、`player-camera.png`：查看过的运行画面。

恢复旧版时先把备份解压到独立目录，用旧版 Unity 打开并重建 Library。若需要恢复当前工作目录，应根据合入清单逐项回退并保护之后的修改；不要用整个备份覆盖后续工作。
