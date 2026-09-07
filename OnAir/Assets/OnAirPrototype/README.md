# OnAir 飞行原型

打开 `Scenes/FlightPrototype.unity`，点击 Play。飞机和建筑使用 Sprite Renderer，地面为三维网格，摄像机为固定 45° 正交视角。占位图由编辑器生成，可直接替换。

## 本版操作

- Place / Weather / Time：轮换地貌、天气和时段，并重新生成预览。
- Pause：暂停地面移动（飞机浮动和现实时间仍继续）。
- New seed：换一个随机种子；相同种子、规则与条件能复现排列。
- Reload CSV：重新读取已由 Unity 导入的表格，校验成功后重新生成；失败保留上一份有效规则。
- 天气和时段目前是筛选条件及简单地面色调预览，没有雨雪粒子和完整昼夜光照。时钟显示电脑时间，测试时段独立于时钟。

## 修改建筑规则

Excel 配置模板位于仓库 `outputs/01a07d7e-02e3-7892-acf1-37a31ca56b3a/buildings.xlsx`。

1. 在 Excel 的 buildings 工作表中修改数据。
2. 将该工作表另存为 **CSV UTF-8（逗号分隔）**，覆盖 `Data/buildings.csv`。Unity 运行时读取 CSV，不直接读取 xlsx。
3. 回到 Unity 等待文件导入。通过 `OnAir > Validate building table` 检查。
4. 再次 Play，或在运行中按 Reload CSV。

| 列 | 含义 |
| --- | --- |
| id | 数字形式的唯一字符串编号，例如 `1`、`001`；不转换为整数 |
| prefab_id | BuildingCatalog 中的预制体编号 |
| biomes | City、Coast、Desert |
| weather | Clear、Rain、Snow |
| periods | Dawn、Day、Dusk、Night |
| weight | 符合条件后参与抽取的相对权重，0 禁用 |
| min_scale / max_scale | 缩放下限与上限，0 < min <= max <= 1.5 |

同一格用 `|` 表示任选其一，`*` 表示不限制。不同列之间必须同时满足。留空属于错误，不代表不限。小数使用点。无匹配规则会留空，例如 Desert + Snow。

## 新增或替换建筑

1. 复制一个 `Prefabs/Buildings` 下的预制体，重新命名。
2. 修改 Visual 子物体的 Sprite Renderer。根物体是地面锚点，保持旋转为零、缩放为一；Visual 使用底部中心为图片轴心，朝向固定镜头。
3. 打开 `Data/BuildingCatalog.asset`，添加唯一编号，并拖入新预制体。
4. 在表格中新增一行，让 prefab_id 与该编号一致。

原型采用固定间距的格点，缩放限制配合现有占位图。更大建筑需增加地块间距或后续增加占地字段；暂不支持任意大小建筑的自动避让。预制体复用时会反复启用/停用，未来有状态的组件需在 OnEnable 重置。

## 替换飞机

`Prefabs/Characters/Plane.prefab` 是独立角色预制体。替换 Visual 的图片不影响建筑生成。PlaneView 只管理浮动与轻微倾斜；未来的操控、属性和存档另加组件。

## 程序分工

```text
buildings.csv → SpawnRules（校验、条件过滤、加权选择）
BuildingCatalog（编号 → 预制体） ─┐
WorldContext（当前条件） ────────┼→ BuildingSpawner（摆放、滚动、复用）
SpawnRules ─────────────────────┘
Plane.prefab → PlaneView（独立表现）
WorldContext → EnvironmentView（地面、背景色调）
PrototypePanel → 修改测试条件，触发预览刷新
```

没有全局单例或通用事件总线。依赖在 Inspector 显式连接。建筑预制体不读取表格、不决定生成时机。表格不知道场景对象路径，预制体移动文件夹后可继续通过 Catalog 引用。

此版聚焦模块化与数据驱动验证；尚未加入里程、存档、解锁、自动地貌切换或桌面窗口功能。

## 开发检查

`Editor/PrototypeBuilder.cs` 提供创建资源、校验规则和独立批处理冒烟验证的入口。创建操作只允许目标场景不存在时执行，不会重建并覆盖已有美术修改。日常请使用 Validate building table，不需要重复创建资源。

## 表头约定

第一列为标记列：`##var` 定义字段名，`##type` 声明类型，`##` 写中文配置说明。数据行第一列留空。以 `#` 开头的备注行不会生成建筑。类型行必须与当前支持的字段类型一致。列表使用 `|` 分隔，`*` 是不限条件的特殊值。Excel 编号列采用文本格式，C# 保持 string，可保留前导零。此读取器支持本表的平面结构，并非参考项目的完整表格生成工具。
