using System.Globalization;
using Microsoft.Win32;

namespace PredatorControlApp
{
    // UI text lookup. The English source string is the key; a missing entry falls back to English.
    internal static class Loc
    {
        internal static string T(string en) =>
            Table != null && Table.TryGetValue(en, out var s) ? s : en;

        internal static string F(string enFormat, params object?[] args) =>
            string.Format(CultureInfo.CurrentCulture, T(enFormat), args);

        internal static bool IsSimplifiedChinese(CultureInfo ui)
        {
            for (var c = ui; c.Name.Length > 0; c = c.Parent)
                if (c.Name is "zh-Hans" or "zh-CN" or "zh-SG") return true;
            return false;
        }

        private static readonly Dictionary<string, string> ZhHans = new()
        {
            // Power modes
            ["Quiet"] = "安静",
            ["Silent"] = "安静",
            ["Balanced"] = "平衡",
            ["Performance"] = "性能",
            ["Perf"] = "性能",
            ["Turbo"] = "极速",
            ["Eco"] = "节能",

            // Fan modes
            ["Auto"] = "自动",
            ["Max"] = "最大",
            ["Custom"] = "自定义",
            ["Fixed Speed"] = "固定转速",
            ["Curve"] = "曲线",

            // RGB effects
            ["Static"] = "静态",
            ["Breathing"] = "呼吸",
            ["Neon"] = "霓虹",
            ["Wave"] = "波形",
            ["Shifting"] = "切换",
            ["Zoom"] = "缩放",
            ["Meteor"] = "流星雨",
            ["Twinkling"] = "闪烁",

            // Dashboard
            ["CPU FAN:"] = "CPU 风扇：",
            ["GPU FAN:"] = "GPU 风扇：",
            ["Fan speed:"] = "风扇转速：",
            ["Power:"] = "模式：",
            ["Plugged In"] = "已接通电源",
            ["N/A"] = "无",
            ["POWER MODE"] = "性能模式",
            ["ON AC POWER:"] = "接通电源时：",
            ["ON BATTERY:"] = "使用电池时：",
            ["Don't Change"] = "不更改",
            ["FAN CONTROL"] = "风扇控制",
            ["CPU FAN: {0}%"] = "CPU 风扇：{0}%",
            ["GPU FAN: {0}%"] = "GPU 风扇：{0}%",
            ["DISPLAY REFRESH RATE"] = "屏幕刷新率",
            ["{0} Hz (Max)"] = "{0} Hz（最高）",
            ["BATTERY CHARGE LIMIT"] = "充电上限",
            ["Full Charge (100%)"] = "充满（100%）",
            ["Limit to 80% (Health)"] = "限制到 80%（保护电池）",
            ["Not Supported"] = "不支持",
            ["STARTUP"] = "开机启动",
            ["Start with Windows"] = "开机自动启动",
            ["Could not register Predator Control to start with Windows.\r\n\r\nThe scheduled task could not be created. Try running the app as administrator."] =
                "无法设置 Predator Control 开机启动。\r\n\r\n计划任务创建失败。请以管理员身份运行本程序后重试。",
            ["Could not remove the Predator Control startup task."] = "无法删除 Predator Control 的开机启动任务。",
            ["KEYBOARD RGB MODE"] = "键盘灯光模式",
            ["BRIGHTNESS: {0}%"] = "亮度：{0}%",
            ["EFFECT SPEED: {0}%"] = "效果速度：{0}%",
            ["COLOR CUSTOMIZATION"] = "颜色设置",
            ["    Choose Custom Color"] = "    选择自定义颜色",
            ["GAME SYNC"] = "游戏同步",
            ["Disabled"] = "已关闭",
            ["Active \u2014 Monitoring"] = "已启用 \u2014 监控中",
            ["Active \u2014 {0}"] = "已启用 \u2014 {0}",
            ["🎮  Configure Executables"] = "🎮  配置程序",
            ["UPDATES"] = "更新",
            ["Version {0}"] = "版本 {0}",
            ["⬇  Check for Updates"] = "⬇  检查更新",
            ["⬇  Check for Updates  (v{0})"] = "⬇  检查更新  (v{0})",

            // Tray menu
            ["  Power Mode"] = "  性能模式",
            ["  Fan Mode"] = "  风扇模式",
            ["  Display"] = "  显示",
            ["  Keyboard RGB"] = "  键盘灯光",
            ["  Battery Limit"] = "  充电上限",
            ["Limit to 80%"] = "限制到 80%",
            ["Open Dashboard"] = "打开主界面",
            ["Exit"] = "退出",

            // Updates
            ["You're on the latest version (v{0})."] = "已是最新版本（v{0}）。",
            ["Could not check for updates:\n{0}"] = "无法检查更新：\n{0}",
            ["Update available"] = "有可用更新",
            ["Version {0} is available — you have v{1}"] = "新版本 {0} 可用，当前为 v{1}",
            ["Downloading update…"] = "正在下载更新…",
            ["Update failed:\n{0}"] = "更新失败：\n{0}",
            ["The update to v{0} could not be installed - you're still on v{1}.\n\nThis usually means the app file was locked or write-protected. Try again, or download it manually from GitHub."] =
                "无法安装 v{0} 更新，当前仍是 v{1}。\n\n通常是程序文件被占用或只读。请重试，或到 GitHub 手动下载。",
            ["Updated to v{0}"] = "已更新到 v{0}",
            ["What's new"] = "更新内容",
            ["Update Now"] = "立即更新",
            ["Close"] = "关闭",
            ["Later"] = "以后再说",

            // Errors
            ["{0}: {1}\n\nLogged to:\n{2}"] = "{0}：{1}\n\n日志位置：\n{2}",
            ["Fatal"] = "严重错误",
            ["Error"] = "错误",

            // Fan curve editor
            ["Fan Curve Editor"] = "风扇曲线编辑器",
            ["FAN CURVE"] = "风扇曲线",
            ["CPU FAN CURVE"] = "CPU 风扇曲线",
            ["GPU FAN CURVE"] = "GPU 风扇曲线",
            ["Reset"] = "重置",
            ["Apply"] = "应用",

            // Game Sync
            ["Game Sync Configuration"] = "游戏同步设置",
            ["🎮  Game Sync Configuration"] = "🎮  游戏同步设置",
            ["CONFIGURED APPS"] = "已添加的程序",
            ["⚡  Pick Running Process"] = "⚡  从运行中的程序选择",
            ["Select an application from the list\nor add a new one to get started."] = "从列表中选择一个程序，\n或添加一个新程序。",
            ["FAN MODE"] = "风扇模式",
            ["REFRESH RATE"] = "刷新率",
            ["RGB KEYBOARD"] = "键盘灯光",
            ["BRIGHTNESS"] = "亮度",
            ["EFFECT SPEED"] = "效果速度",
            ["💾  Save Profile"] = "💾  保存配置",
            ["🗑  Remove"] = "🗑  删除",
            ["Select an Application"] = "选择程序",
            ["Could not browse to that location.\n\nYou can find the process name in Task Manager while the game is running, and use 'Pick Running Process' to add it."] =
                "无法打开该位置。\n\n可以在游戏运行时到任务管理器里查看进程名，再用“从运行中的程序选择”添加。",
            ["Browse Failed"] = "浏览失败",
            ["No running processes found."] = "没有找到运行中的程序。",
            ["Pick Process"] = "选择程序",
            ["Pick a Running Process"] = "选择运行中的程序",
            ["Search..."] = "搜索...",
            ["Add Selected"] = "添加所选",
            ["\"{0}\" is already configured."] = "“{0}”已经添加过了。",
            ["Duplicate"] = "重复",
            ["Remove \"{0}\" from Game Sync?"] = "要从游戏同步中删除“{0}”吗？",
            ["Confirm Remove"] = "确认删除",

            // Controls
            ["Select..."] = "请选择...",

            // Language
            ["LANGUAGE"] = "语言",
            ["Follow Windows"] = "跟随系统",
            ["Language"] = "语言",
            ["Restart Predator Control now to use the new language?"] = "需要重启 Predator Control 才能切换语言。现在重启吗？",
        };

        // Language picked in the app: "auto" follows the Windows display language.
        // Order matches the language drop-down.
        internal static readonly string[] Languages = { "auto", "en", "zh-Hans" };

        internal static bool UseChinese(string language, CultureInfo ui) => language switch
        {
            "auto" => IsSimplifiedChinese(ui),
            "en" => false,
            "zh-Hans" => true,
            _ => throw new InvalidDataException(
                $@"Unknown Language value '{language}' in HKCU\SOFTWARE\PredatorControl")
        };

        internal static void SaveLanguage(string language)
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\PredatorControl");
            key.SetValue("Language", language);
        }

        private static string ReadLanguage()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\PredatorControl");
            return key?.GetValue("Language") as string ?? "auto";
        }

        // Static fields initialize in source order: these must stay after ZhHans.
        internal static readonly string Language = ReadLanguage();

        private static readonly Dictionary<string, string>? Table =
            UseChinese(Language, CultureInfo.CurrentUICulture) ? ZhHans : null;

        // One font for Latin and CJK text, so mixed strings render at one size.
        internal static readonly string UiFont = Table != null ? "Microsoft YaHei UI" : "Segoe UI";
    }
}
