# OnAir 像素天气效果

已接入统一 Main 场景：GameEntry 连接 WeatherParticles、天气表和 Shader，读取 GameSession.weather。天气切换不会重新生成建筑。Clear=0、Rain=1、Snow=2、Cloudy=3、HeavyRain=4、Sandstorm=5 保持不变。

## 区域天气

自动天气按区域配置，源表 `OnAir/Assets/Data/Tables/climate.csv`，每行一个「区域 × 天气」：

| 字段 | 含义 |
| --- | --- |
| zone | 连续世界区域 City / Town / CoastalTown / Forest / RiverPlain / Hills / Ocean，每个都必须出现 |
| weather | 天气枚举；同一区域内不可重复 |
| weight | 相对权重，0 表示该区域禁用 |
| min_seconds / max_seconds | 该天气持续秒数范围，5 ≤ min ≤ max |

每个区域至少要有一种正权重天气。ClimateZoneTracker 每 0.5 秒取飞机正下方的区域。当前天气到期后，从该区域除当前天气外的规则中按权重抽下一种，并按该区域该天气的秒数范围计时。飞入不允许当前天气的区域时，当前天气最多再持续 8 秒后切换，不会瞬间跳变。目前没有沙漠区域，因此沙尘暴不会自动出现，只能在面板手动切换。

修改后同样点 **Reload CSV** 生效。该表计入存档配置签名，改动后旧存档会提示世界规则不一致。

## 调整方式

源表：`OnAir/Assets/Data/Tables/weather_fx.csv`。各区域允许的天气在同目录 climate.csv 配置。

保持现有 ##var / ##type / ## 注释行风格。保存 CSV 后，点击原型面板的 **Reload CSV**，同时重载建筑规则和天气效果；也可使用菜单 **OnAir > Validate weather table** 检查配置。

编辑器读取表文件的已保存内容。打包后默认使用随包表；如需外部调整，可把同名 `weather_fx.csv` 放到 Unity 的 `Application.persistentDataPath` 目录，再点击重载。未找到外部表时使用随包配置。整张表通过校验才生效，错误会显示在面板中，并保留上一份有效配置。

| 字段 | 含义 / 范围 |
| --- | --- |
| weather | 每种天气一行，六种天气均需存在，不可重复 |
| shape | none 无粒子 / rain 雨线 / snow 十字雪花 / dust 菱形尘粒 |
| rate | 两层总发射量，每秒 0–800；rate × lifetime 不超过 4000 |
| speed | 镜头局部向下速度，0–40 单位/秒 |
| wind | 镜头局部向右风速，-20–20；负数向左 |
| size / length | 粒子宽 / 高；范围 0.01–1 / 0.01–8 单位。雨线宽度至少 1 个屏幕像素（近层 2 像素） |
| lifetime | 存活秒数，0.1–12 |
| color | #RRGGBBAA，最后两位为透明度 |
| near_ratio | 总发射量分给近层的比例，0–1 |
| sway | 噪声摆动强度，0–2；雨线建议为 0 |
| transition | 新层淡入与旧层淡出的秒数，0.05–10 |
| ripples | 水面雨滴波纹密度 0–1，0 为无波纹；随雨层淡入淡出 |

none 要求 rate=0。数字使用英文小数点，颜色需要 # 前缀。近层尺寸与速度为基准的 1.25 倍，远层为 0.7 倍。暂停会冻结粒子模拟及过渡；恢复后继续。夜晚粒子 RGB 降至日间的 60%，透明度仍由表控制。

## 美术与实现边界

雨线、雪花、沙尘采用代码原生粒子形状与专用 URP 着色器，不依赖生成图片或不透明棋盘底图。形状边缘硬朗，生命周期淡入淡出；没有整屏像素化后期，因此不是严格屏幕像素对齐。

雨线使用按速度拉伸的粒子（Stretched Billboard），方向跟随下落速度与风，两端淡出，读起来是一道轨迹而不是短块。

水面波纹：河流、水田与浅海的网格上叠加一层 `OnAir/RainRipples`（`Assets/Resources/Weather`），在世界像素网格（每单位 4 像素）上画 1 像素硬边圆环和起始水花点，每 2.5 单位一个潜在落点，约 1.1 秒扩散并淡出。密度、时钟由 WeatherParticles 写入全局 `_RainRipples` / `_RainRippleTime`，暂停时冻结；颜色跟随环境光 `_PixelSky`，夜间自动变暗。

两层为镜头前方的天气表现层，随镜头移动，覆盖屏幕可视区域；并不模拟真实高度中的降水碰撞。当前不包含地面（非水面）水花、积雪、湿地材质或体积雾。晴天及阴天不发射粒子，阴天沿用现有环境变暗表现。

建筑的天气过滤保持原有含义：只允许 Rain 的建筑不会自动允许 HeavyRain。需要在 buildings.csv 中明确加入新天气或使用 *。

独立验证入口：`WeatherFxValidation.Run` 用于隔离副本批处理，完成检查后退出编辑器，不要在日常工作编辑器中调用此批处理入口。普通编辑器使用 Validate weather table 菜单。
