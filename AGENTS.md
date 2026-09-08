# OnAir 项目协作约定

- Unity 工程在 `OnAir/`；当前正式场景和打包入口只有 `OnAir/Assets/Scenes/Main.unity`。
- Assets 按 Scenes、Scripts、Prefabs、Art、Data、Settings、Editor 分类；脚本按 Game、Flight、World、Environment、UI、Data 分工。不再按开发阶段建立 Prototype、CityBlock、Journey 等平行项目目录。
- 并行开发或验证需要临时场景时，使用 `Assets/Scenes/_Temp_任务名.unity`，不加入打包。交付前合入有效修改，并删除临时场景及其 `.meta`。
- 飞机和每种建筑保持独立预制体。规则表、资源映射、渲染表现分别维护。建筑规则统一使用 `Data/Tables/buildings.csv`；id 是只含数字的 string，保留 `##var / ##type / ##` 格式。
- 移动 Unity 资源时保留 `.meta` 和 GUID，检查场景、预制体、配置和工具的引用，确认无引用后再清理。尊重其他任务未提交的修改。
- 飞机报告航程增量，JourneyController 不自行计时。天气/时段切换不重建建筑。盘旋期间暂不推进路线里程，属于当前演示规则。
- 初始化与模块连接归 GameEntry，功能规则留在各模块。编辑器工具放 Editor；素材生成工具不顺带创建另一个正式场景。
- 项目说明维护在根目录 README.md 和 Docs；美术原稿在根目录 Art，运行资源在 Unity 的 Assets/Art。
