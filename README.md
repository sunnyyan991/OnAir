# OnAir

## 已确定的方向

- 开发引擎：Unity。
- 美术参考：《八方旅人》的 HD-2D 视觉风格。
- 画面目标：2D 像素角色与带有像素质感的 3D 场景结合，通过光影、空间层次和适度景深形成微缩景观感。

## 建议的美术落地方式

- 角色：采用像素精灵和逐帧动画，统一角色比例及像素密度。
- 环境：使用 3D 地形、建筑与道具，搭配统一尺度的像素贴图。
- 摄影：先验证固定倾斜视角；以角色轮廓清晰、场景遮挡合理为优先。
- 光影：强调环境色与局部光源的冷暖关系，控制泛光和景深，保证可玩区域清晰。
- 技术验证：重点检查像素角色与场景的遮挡、受光、投影，以及镜头移动时的像素稳定性。

## 当前状态

Unity 工程位于 `OnAir/`，使用 Unity `2022.3.62f3c1` 和 URP。打开 **`OnAir/Assets/Scenes/Main.unity`** 后点击 Play。这是唯一正式场景，也是打包入口。

已实现飞机巡航与盘旋、镜头平滑跟随、按航程切换地形、道路与建筑生成、天气和各地形昼夜表现。飞机已接入新交付的低翼 3D 模型，保留 Sprite 回退；建筑与道路使用 3D 几何体。

- 飞行参数：`Assets/Prefabs/Characters/Plane.prefab` 的 FlightController。
- 建筑、地形、天气表：`Assets/Data/Tables`。
- 地形昼夜表现：`Assets/Data/Profiles`。
- 预制体与资源映射：`Assets/Data/Catalogs`。
- 统一渲染配置：`Assets/Settings/Rendering/MainPipeline.asset`。

详细说明见 [项目结构](Docs/ProjectStructure.md)、[配置与操作](Docs/Configuration.md) 和 [飞机模型](Docs/Aircraft.md)。根目录 `Art/` 保存美术原稿，游戏实际使用的资源位于 Unity 工程的 `Assets/Art`。

临时验证场景统一使用 `Assets/Scenes/_Temp_任务名.unity`，验证完成后合入正式资源并删除临时场景。不再按每轮原型建立平行项目目录。
