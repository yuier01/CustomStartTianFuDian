# 自定义开局天赋点（CustomStartTianFuDian）

《觅长生》BepInEx 插件：设置开局（创角）天赋界面的可用天赋点数，可配置。

- 插件 GUID：`com.yuier.MCS.CustomStartTianFuDian`
- 目标框架：net472（与游戏 Mono 运行时一致）
- 依赖：BepInEx 5.4.17 + HarmonyX 2.5.5（均为游戏 / 创意工坊自带，不随本插件分发）

## 一、功能

1. 创角流程进入天赋选择界面时，界面上的剩余点数由本插件按配置值给出，而不是游戏原本的数量。
2. 可选的：允许天赋界面第 2 页（角色设定）不作选择直接进入下一页。

## 二、实现

### 天赋点数

`MainUISelectTianFu.tianfuDian` 是该界面剩余点数的唯一载体：

- 点选天赋时由 `AddTianFuDian(int)` 扣减；
- 进入下一步时由 `NextPage()` 校验是否为负（第 8 页，为负则弹"天赋点不能为负数"并拦住）；
- 游戏没有给它赋值的地方，`Init()` 只把显示文本 `tianfuNum` 置为 `"0"`。

因此插件在 `Init()` 之后写入配置值，并把显示文本同步为同一个数字。

写入时机与游戏的 `isInit` 守卫保持一致：`Init_Prefix` 先记录调用前的 `isInit`，`Init_Postfix` 只在它原本为 `false`（即本次调用真正执行了初始化）时才写入。捏脸／天赋面板来回切换时若再次触发 `Init()`，其方法体已被 `isInit` 挡住，本补丁同样不介入，因此不会覆盖玩家已分配的点数。

### 跳过第 2 页校验

`NextPage()` 开头会调用 `CheckCurHasSelect()`，未选中任何条目时弹"至少选择一个天赋"并拦住。

`CheckCurHasSelect()` 全程序集只被 `NextPage()` 调用，因此在它上面跳过校验不会影响其它逻辑。灵根页（`curPage == 5`）的校验由 `MainUISetLinGen.CheckLingGen()` 负责，与本补丁无关，照旧生效。目标页由补丁内的 `PageWithoutSelectionCheck` 常量指定。

天赋界面的页码与内容对应关系（取自 `CreateAvatarMiaoShu`，与 `CreateAvatarJsonData` 的 `fenLeiGuanLian` 一致）：

| curPage | 内容 | 条目数 |
| --- | --- | --- |
| 1 | 选择难度 | 5 |
| 2 | 角色设定 | 6 |
| 3 | 你的出生 | 7 |
| 4 | 你的资质 | 5 |
| 5 | 你的灵根（独立面板） | — |
| 6 | 先天体质 | 18 |
| 7 | 儿时经历 | 10 |
| 8 | 青年经历 | 11 |

| 目标方法 | 补丁类型 | 作用 |
| --- | --- | --- |
| `MainUISelectTianFu.Init` | Prefix | 记录调用前的 `isInit`，供 Postfix 判断本次是否真正做了初始化 |
| `MainUISelectTianFu.Init` | Postfix | 在真正初始化时写入配置点数，刷新 `tianfuNum` 显示 |
| `MainUISelectTianFu.CheckCurHasSelect` | Prefix | 目标页直接返回"已选择"，跳过"至少选择一个天赋"校验 |

说明：

- 点数补丁不改写原方法指令；`Init` 上的 Prefix 只做一次字段读取，不跳过原逻辑，实际写入由 Postfix 完成，且以游戏自身的 `isInit` 为条件。
- 校验补丁是跳过式 Prefix，仅在开关开启且 `curPage` 为目标页时生效，其余情况 `return true` 交还原逻辑。
- 天赋的扣点逻辑（`AddTianFuDian`）不参与。
- 创角流程各面板的 `Init()` 方法体都受各自的 `isInit` 字段守卫，本插件的写入条件与该守卫对齐：只要本次调用没有真正执行初始化，就不写入。
- `tianfuNum`、`isInit` 是 private `[SerializeField]`，通过 Harmony 的 `Traverse` 按名访问，不依赖字段偏移；`tianFuPageList` 与 `curPage` 是 public 字段，直接访问。
- 补丁体包在 try/catch 中，插件自身出错不会中断创角流程。读取 `isInit` 失败时按"已完成初始化"处理，宁可少写一次也不覆盖玩家已分配的点数。
- 点数只在本次创角的天赋界面内有效，不参与存档持久化。

## 三、配置

首次运行自动生成 `BepInEx/config/com.yuier.MCS.CustomStartTianFuDian.cfg`：

```ini
[天赋点]

## 进入天赋选择界面后的初始天赋点数。
# Setting type: Int32
# Default value: 9999
开局天赋点数 = 9999

## 天赋界面第 2 页为角色设定，开启后可不选任何条目直接进入下一页。
# Setting type: Boolean
# Default value: true
第2页可不选 = true
```

- 开局天赋点数是**绝对值**，不是"在原值上加 N"。
- 不设取值范围，`int` 装得下就能用；`tianfuDian` 本身是 `int`，游戏只对它做加减。
- 配置在首次进入创角天赋界面时读取一次（`Init()` 的方法体受 `isInit` 守卫，不会重跑）。中途修改配置不会被重新读取，需重启游戏才能生效。
- 若点数配置值小于点选天赋的花费之和，点数为负时会触发游戏自身的校验而无法进入下一步。

## 四、构建与部署

```powershell
dotnet build -c Release
```

产物：`bin\Release\net472\CustomStartTianFuDian.dll`

部署方式二选一：

1. **本地测试**：把 `CustomStartTianFuDian.dll` 放进
   `觅长生\本地Mod测试\plugins\`（对应游戏根目录下的本地测试目录）；
2. **创意工坊**：放进某个 mod 目录的 `plugins\` 下，再按发布流程上传。

> `Mod.bin` 是创意工坊发布用的序列化元数据（标题 / 简介 / tag / SteamID 等），由发布工具生成，与插件运行无关，本工程不提供。

日志输出：

```
[Info : 自定义开局天赋点] 自定义开局天赋点 v1.0.0 已加载，开局天赋点数 = 9999
[Info : 自定义开局天赋点] 开局天赋点已设置：0 -> 9999
```

第二条在进入天赋界面时输出，`0` 是写入前的字段值。

## 五、工程约定

`net472` + `LangVersion 11` + `Nullable enable`，游戏与 BepInEx 引用一律 `Private=false` 不随产物复制，`Config.Bind` 配置项使用中文分组与说明。

> 工程所在的目录名（`MoreTianFuDian`）是历史遗留，不影响任何运行时标识：程序集名与产物 DLL 名由 `<AssemblyName>` 决定，均为 `CustomStartTianFuDian`。
