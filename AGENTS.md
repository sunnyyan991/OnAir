# OnAir 项目协作约定

- Unity 工程在 `OnAir/`；当前正式场景和打包入口只有 `OnAir/Assets/Scenes/Main.unity`。
- Assets 按 Scenes、Scripts、Prefabs、Art、Data、Settings、Editor 分类；脚本按 Game、Flight、World、Environment、UI、Data 分工。不再按开发阶段建立 Prototype、CityBlock、Journey 等平行项目目录。
- 并行开发或验证需要临时场景时，使用 `Assets/Scenes/_Temp_任务名.unity`，不加入打包。交付前合入有效修改，并删除临时场景及其 `.meta`。
- 飞机和每种建筑保持独立预制体。规则表、资源映射、渲染表现分别维护。建筑规则统一使用 `Data/Tables/buildings.csv`；id 是只含数字的 string，保留 `##var / ##type / ##` 格式。
- 移动 Unity 资源时保留 `.meta` 和 GUID，检查场景、预制体、配置和工具的引用，确认无引用后再清理。尊重其他任务未提交的修改。
- 飞机报告航程增量，JourneyController 不自行计时。天气/时段切换不重建建筑。盘旋期间暂不推进路线里程，属于当前演示规则。
- 初始化与模块连接归 GameEntry，功能规则留在各模块。编辑器工具放 Editor；素材生成工具不顺带创建另一个正式场景。
- 项目说明维护在根目录 README.md 和 Docs；美术原稿在根目录 Art，运行资源在 Unity 的 Assets/Art。

## 按改动范围验证

- 默认仅在 Unity Editor 内测试验收。只有用户明确要求打包时才打包，包括 Development 或临时测试包；不因验证方便或编辑器被占用而自行改用运行包验收。
- 何时需要打包测试的具体规则尚未确定，后续随实际开发逐步确认，不自行补充自动打包条件。
- 验证前明确本次风险、最小检查和停止条件，遵循 `Docs/ValidationLessons.md`。不默认完整巡航、多种子扫描、全资产烘焙。
- 文档/参数：检查差异和直接影响；单资产：只烘焙该资产并查看相关方向；渲染：小场景覆盖关键状态，再短时检查 Main；生成/流式加载：才使用固定种子和有界运行。
- 一轮检查通过后，不因“更放心”重复。出现错误先定位，只有相关改动后重跑对应检查。超时、无进展、项目锁必须停止并换方法，不能延长等待冒充进展。
- 日志只读相关错误、状态和最终摘要；截图只查看能回答问题的画面。保留原始记录，报告已验证项与未验证项。

- 新运行资产的 Lit 材质使用 `OnAir/Cloud Receiving Lit` 保持云影接收；烘焙像素资产使用现有像素 Shader。不要把云影改回最终画面乘色，详见 `Docs/CloudShadowContinuity.md`。
