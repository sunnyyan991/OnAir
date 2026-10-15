# SmallField 灯光

基础灯保留跑道边白、门槛向进近侧绿、同一端向跑道内侧红、滑行边蓝，全部常亮。门槛、跑道末端及 PAPI 的前向镜片使用 Back culling，避免在背面看见反向信号；闭合边灯和暖灯保持原材质双面规则。黄色跑道尾段适用于仪表跑道，本版本不用；不增加中心线灯、REIL 或跑道状态灯。新增的缩尺顺序进近引导见下节。[FAA AIM 2-1-2、2-1-4、2-1-10](https://www.faa.gov/air_traffic/publications/atpubs/aim_html/chap2_section_1.html)

模型以米为单位，但采用游戏压缩布局：白边灯 20m；蓝灯沿曲线加密，保持切点连续，发光镜片与钢座分别建组。镜片日间保留自身色，夜间 emission 为自身色 × 2.2 × night，不改变草地、路面或全球光照。安装几何参考 [FAA AC 150/5340-30J 官方资料](https://www.faa.gov/airports/resources/advisory_circulars/index.cfm/go/document.current/documentnumber/150_5340-30)，这些缩小布局参数属于游戏设计。

PAPI 四个独立前向镜片按距跑道近到远编号。依据飞机相对灯具的高度、纵向距离和进近方向计算；3°时近侧两红、远侧两白，过高全白、过低全红。本游戏只在有效 `Approach` 时启用对应进场端的信号；其他阶段或超出进近侧/侧向范围时隐藏镜片信号，钢壳保留。基础机场灯保持常亮，不跟随此策略闪动。当前飞机高度与机场长度的压缩比例不同，真实计算可能在大部分进近中显示全白；不伪造固定两红两白。[FAA AIM PAPI](https://www.faa.gov/air_traffic/publications/atpubs/aim_html/chap2_section_1.html)

飞机左红、右绿、尾白为位置灯。[FAA Airplane Flying Handbook，第11章](https://www.faa.gov/sites/faa.gov/files/regulations_policies/handbooks_manuals/aviation/airplane_handbook/12_afh_ch11.pdf) 白频闪用于飞行及跑道内起降，接地后继续至整个机体离开跑道；滑行道与库内关闭。红 beacon 仅发动机运行时使用，牵引与维护时关闭；维护时位置灯也关闭。这是本游戏的自动操作策略，参考 FAA 关于地面眩光和发动机警示的建议。[FAA AIM 4-3-24](https://www.faa.gov/air_traffic/publications/atpubs/aim_html/chap4_section_3.html)

## 接入与检查

`AirportGroundLights.Build(airportRoot)` 只绑定 `AirfieldLights` 的精确子组：`RunwayEdge`、四个 `Threshold…Green` / `End…Red`、`TaxiEdgeBlue`、`WarmApron`、`WarmTerminal`、`PapiSouth/Lens-0..3`、`PapiNorth/Lens-0..3`，以及 `ApproachSequenceSouth/Lamp-0..4`、`ApproachSequenceNorth/Lamp-0..4`。这些组内只放发光镜片。调用 `Refresh(night, simulationTime, site, aircraftWorld, phase)`；调用方时钟暂停时冻结，基础灯不使用闪动。组件销毁或重新 Build 时恢复源材质和属性，释放自身克隆。

纯检查接口：`PapiRedCount(angle)` 对 2.4/2.8/3/3.2/3.6°返回 4/3/2/1/0；`PapiVisible` 检查方向；`AircraftLights.ShouldUseStrobes`、`ShouldUseBeacon` 检查阶段；`OccupiesRunway` 使用源坐标与 6.5m 整机净空。验证落地仍闪、脱离跑道停闪、维护全停、两方向 PAPI、暂停不推进脉冲；实际画面只在 Unity 中统一检查。

## 缩尺顺序进近引导

FAA AIM 描述顺序闪光形成朝跑道移动的光点，每秒重复两次；REIL 是门槛两侧同步闪光的一对灯，PAPI 则提供红白角度指示，三者用途与节奏分开。[FAA AIM 2-1-1、2-1-2、2-1-3](https://www.faa.gov/air_traffic/publications/atpubs/aim_html/chap2_section_1.html) FAA 的操作表列有日间及夜间的不同强度；顺序闪光属于 ALS，按能见度、操作需要或飞行员请求使用，现实中不限定只在夜晚运行。[FAA JO 7110.65，3-4-6 至 3-4-8](https://www.faa.gov/air_traffic/publications/atpubs/atc_html/chap3_section_4.html)

本游戏借用向门槛移动的方向和2Hz节奏，保留紧凑机场，组成32m缩尺混合设施；不把它标为完整 MALSR、ALSF 或其他大型仪表机场系统。每端5个低矮白光镜片在跑道主轴上，距门槛6至38m，间距8m；编号从远到近。只有当前 `Approach` 的进场端启用，其他阶段立即关闭。

周期0.5秒，每灯时隙0.1秒、单次亮55ms，其余时间暗，最后一灯结束后从远端重新开始；同一时刻最多一灯亮。白天白光 emission 为7，满夜为3.5，中间连续过渡。原有边灯、暖灯和真实射灯的颜色、强度、覆盖保持不变；新设施只增加光学镜片，不增加真实 Light 对象。前向单面光学面采用 Back culling。

纯接口 `SequenceIndex(time,count,period=0.5)` 返回当前时隙编号；`SequenceFlash(time,count,period=0.5)` 判断该时隙是否在55ms亮段。五灯在0.025/0.125/0.225/0.325/0.425秒依次亮0/1/2/3/4，0.075秒为暗段，0.525秒重新亮0。`SequenceSouthIndex` / `SequenceNorthIndex` 在关闭或暗段为-1，可检查单向启用、昼夜亮度、暂停及离开进近后关闭。飞机滑行及库内频闪策略独立，保持原规则。

## 燃油设施附加素材

燃油围场采用独立围栏、罐体、泵柜、管线与可读警示牌，放在飞机和车辆通道之外；保持检修入口及设备之间的间隔。它是缩小的环境素材，具体造型参考 [FAA AC 150/5230-4C：机场燃油储存、处理与加注官方资料](https://www.faa.gov/airports/resources/advisory_circulars/index.cfm/go/document.information/documentID/1040345)。设备材质保持正常受光，不把储罐或围栏整体做成发光物。当前有效版本的[2026年勘误合并文件](https://www.faa.gov/documentLibrary/media/Advisory_Circular/150-5230-4C-Aircraft-Fuel-20260617-errata-changes.pdf)第14页给出了围栏与门锁、燃料类型/禁烟标识、消防器材和紧急切断等参考要素。

## 夜景材质与机库

夜间亮色长条来自旧控制器以 `Contains("Light")` 选择材质，误把 `SmallField GrassLight` 草地当成灯。相同做法也误选了木材 `WarmWood` 和停放飞机的 `Warm ivory`。现以明确灯组和精确材质身份控制发光；普通地面、漆面、木材和车窗保持受光。只有航站楼玻璃所属的建筑组额外启用窗光，停车汽车玻璃不会自发光。机场状态来自 `FlightController.airport`，不能假定机场控制器和飞机灯位于同一对象。

机库前后屋面保持渲染，2.5秒内由不透明连续过渡到10%不透明，暂停时冻结。出库时直到整段机身离开门墙、中心本地x≤98.5米才反向恢复。屋面使用本机场独立的透明材质克隆，墙壁与内部屋架继续投影；屋面不写入深度、不投影，避免透明阶段留下深度遮挡或突然改变影子。恢复库内存档直接使用10%不透明，避免闭顶闪一下。

机库用四盏交叉瞄准飞机的真实暖色射灯，满夜强度110、覆盖距离26米、外角110°，并以低强度偏冷点光补出细节。场坪与停车射灯保留每盏作者设置的覆盖和强度，随昼夜系数渐变，不再统一覆盖为35。本轮原八盏室外射灯保留，另给集中后的后勤场新增一盏同风格暖色射灯，共九盏；灯杆在服务车道边，灯头以悬臂伸入照明区域。导航小灯和顺序闪灯仅使用发光镜片，不增加数百个真实灯；机场照明不创建额外实时阴影贴图。

停车位50个，2排垂直车位，各5×2.8米，中央6米车道，分开入口车道与人行横道。周边低层建筑使用现有正式住宅资源，并保留跑道两端净空。物料、消防柜、轮挡与工具架布置在辅助设施和机库后侧，避开实际飞机、拖车与地勤路线。规范参数和夜景检查记录见 `Art/Reviews/Airport/SmallField/parking-facilities-spec.txt` 与 `night-polish-validation.txt`。
