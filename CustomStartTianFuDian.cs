using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.UI;

namespace CustomStartTianFuDian
{
    /// <summary>
    /// 自定义开局（创角）天赋界面的可用天赋点数。
    ///
    /// <c>MainUISelectTianFu.tianfuDian</c> 是该界面剩余点数的唯一载体：
    /// 点选天赋时由 <c>AddTianFuDian(int)</c> 扣减，进入下一步时由 <c>NextPage()</c> 校验是否为负。
    /// 游戏没有给它赋值的地方，<c>Init()</c> 只把显示文本 <c>tianfuNum</c> 置为 "0"。
    /// 本插件在 <c>Init()</c> 之后写入配置值并刷新显示，使其成为该界面点数的实际来源。
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class CustomStartTianFuDianPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log = null!;

        /// <summary>开局天赋点数（绝对值）。</summary>
        internal static ConfigEntry<int> StartPoints = null!;

        /// <summary>是否允许第 2 页（角色设定）不作选择直接继续。</summary>
        internal static ConfigEntry<bool> SkipSelectionOnPage2 = null!;

        private void Awake()
        {
            Log = Logger;

            StartPoints = Config.Bind(
                "天赋点",
                "开局天赋点数",
                9999,
                "进入天赋选择界面后的初始天赋点数。"
            );

            SkipSelectionOnPage2 = Config.Bind(
                "天赋点",
                "第2页可不选",
                true,
                "天赋界面第 2 页为角色设定，开启后可不选任何条目直接进入下一页。"
            );

            Harmony.CreateAndPatchAll(typeof(CustomStartTianFuDianPatch));
            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} 已加载，开局天赋点数 = {StartPoints.Value}");
        }
    }

    internal static class CustomStartTianFuDianPatch
    {
        /// <summary>
        /// 允许不作选择直接进入下一页的天赋页面。
        ///
        /// 各页对应 <c>CreateAvatarMiaoShu</c> 的标题（与 <c>CreateAvatarJsonData</c> 的
        /// <c>fenLeiGuanLian</c> 一致）：
        /// 1 选择难度、2 角色设定、3 你的出生、4 你的资质、5 你的灵根、6 先天体质、7 儿时经历、8 青年经历。
        /// </summary>
        private const int PageWithoutSelectionCheck = 2;

        /// <summary>
        /// 调用 <c>Init()</c> 之前的 <c>isInit</c> 值。由 <c>Init_Prefix</c> 写入、<c>Init_Postfix</c> 读取。
        /// </summary>
        private static bool _isInitBeforeCall;

        /// <summary>
        /// 记录 <c>Init()</c> 执行前的 <c>isInit</c>，供 Postfix 判断本次调用是否真正执行了初始化。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MainUISelectTianFu), nameof(MainUISelectTianFu.Init))]
        private static void Init_Prefix(MainUISelectTianFu __instance)
        {
            try
            {
                _isInitBeforeCall = Traverse.Create(__instance).Field("isInit").GetValue<bool>();
            }
            catch (Exception e)
            {
                // 读取失败时保守视为"已完成初始化"，宁可少写一次也不覆盖玩家已分配的点数
                _isInitBeforeCall = true;
                CustomStartTianFuDianPlugin.Log.LogError($"读取 isInit 失败：{e}");
            }
        }

        /// <summary>
        /// <c>Init()</c> 仅在 <c>isInit</c> 为 false 时执行初始化，本补丁与之保持一致：
        /// 只在本次调用真正做了初始化时才写入点数。
        ///
        /// 捏脸／天赋面板来回切换时若再次触发 <c>Init()</c>，其方法体已被 <c>isInit</c> 挡住，
        /// 本补丁同样不介入，因此不会覆盖玩家已分配的点数。
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(MainUISelectTianFu), nameof(MainUISelectTianFu.Init))]
        private static void Init_Postfix(MainUISelectTianFu __instance)
        {
            try
            {
                if (_isInitBeforeCall)
                {
                    return;
                }

                int old = __instance.tianfuDian;
                int value = CustomStartTianFuDianPlugin.StartPoints.Value;

                __instance.tianfuDian = value;

                // tianfuNum 是 private [SerializeField]，按名访问，不依赖字段偏移。
                // Init() 已把它置为 "0"，此处同步为实际点数。
                var text = Traverse.Create(__instance).Field("tianfuNum").GetValue<Text>();
                if (text != null)
                {
                    text.text = value.ToString();
                }
                else
                {
                    CustomStartTianFuDianPlugin.Log.LogWarning("未找到 tianfuNum 文本组件：点数已生效，但界面数字可能不同步。");
                }

                CustomStartTianFuDianPlugin.Log.LogInfo($"开局天赋点已设置：{old} -> {value}");
            }
            catch (Exception e)
            {
                CustomStartTianFuDianPlugin.Log.LogError($"设置开局天赋点失败：{e}");
            }
        }

        /// <summary>
        /// 允许指定页不作选择直接进入下一页。
        ///
        /// 该页条目的 <c>feiYong</c> 均为 0，<c>NextPage()</c> 的"至少选择一个天赋"校验
        /// 是它唯一的强制点。<c>CheckCurHasSelect()</c> 只被 <c>NextPage()</c> 调用，
        /// 因此在这里跳过校验不影响其它逻辑。
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(MainUISelectTianFu), nameof(MainUISelectTianFu.CheckCurHasSelect))]
        private static bool CheckCurHasSelect_Prefix(MainUISelectTianFu __instance, ref bool __result)
        {
            try
            {
                if (!CustomStartTianFuDianPlugin.SkipSelectionOnPage2.Value)
                {
                    return true;
                }

                // 只放行目标页；其余页面（含 curPage == 5 灵根页）的校验照旧。
                if (__instance.curPage != PageWithoutSelectionCheck
                    || !__instance.tianFuPageList.ContainsKey(PageWithoutSelectionCheck))
                {
                    return true;
                }

                __result = true;
                return false;
            }
            catch (Exception e)
            {
                CustomStartTianFuDianPlugin.Log.LogError($"跳过第 {PageWithoutSelectionCheck} 页校验失败：{e}");
                return true;
            }
        }
    }
}
