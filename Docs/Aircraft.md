# LowWing 飞机

2026-09-11：飞行已改为持续前进、轻微 S 形游移与翼倾，盘旋逻辑和按钮移除。详见 [世界 V02](WorldV02.md)。

以批准风格中的 A 低翼方案为参考制作的原生三维低多边形模型。机身朝本地 +Z、上方 +Y、翼展沿 X；由真实厚度的网格组成，不再依赖朝向相机的面片。

## 接入

模型路径：`Assets/Prefabs/Characters/LowWing.prefab`。正式飞机根预制体为 `Assets/Prefabs/Characters/Plane.prefab`，FlightView 通过 modelPrefab 引用加载模型，关闭原 Sprite 子对象，保留根节点位置与缩放。机头按真实 Heading 朝向，机翼按 Bank 倾斜，螺旋桨随项目暂停冻结。

Inspector 取消 useLowPolyModel 可恢复 Sprite 表现。模型引用缺失时保留原 Sprite。模型网格在 `Assets/Art/Meshes/Aircraft`，材质在 `Assets/Art/Materials/Aircraft`。运行时实例不修改保存的场景或模型。

## 造型与材质

- 低翼双座布局、分面机身、实心翼面与尾翼、灰蓝座舱、简化轮架。
- 机身红色 #C45C48，暖白 #E6D8B5，玻璃 #567887；深色轮胎/桨叶 #303D43。
- URP Lit 材质，低光滑度。使用模型法线及场景灯光表现体积，本版不额外画细密像素贴图。
- 座舱采用不透明灰蓝材质以保证远处轮廓；没有内部人物或复杂透明反射。
- 生成模型原始翼展约 3.68 单位，沿用场景飞机根节点的缩放。Main 场景中原有 2.7 倍缩放下翼展约 9.94 单位。

## 重建与验证

`OnAir > Create low-poly aircraft` 仅在模型尚不存在时生成，已有资产不覆盖。`AircraftValidation.Run` 是隔离副本中的批处理入口，会打开 Main 场景并在输出截图后退出 Unity，不用于日常编辑器操作。
