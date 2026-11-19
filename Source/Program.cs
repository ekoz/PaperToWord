// PaperToWord（试卷裁页成Word）—— 主程序入口（GUI + 命令行双模式）
// 依赖：仅 .NET Framework 4.x 自带 BCL，无第三方库；语法级别 C# 5
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PaperToWord
{
    static class Program
    {
        const string AppTitle = "PaperToWord v1.3";

        // 编译时通过 /win32icon 嵌入的图标资源名（与 exe 默认图标保持一致）
        public const string IconResourceName = "AppIcon";

        static System.IO.Stream _iconStream; // Icon 生命周期内需保持流打开

        public static System.Drawing.Icon LoadAppIcon()
        {
            try
            {
                if (_iconStream == null)
                {
                    System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
                    _iconStream = asm.GetManifestResourceStream(IconResourceName);
                    if (_iconStream == null) return null;
                }
                _iconStream.Position = 0;
                return new System.Drawing.Icon(_iconStream, 32, 32);
            }
            catch { return null; }
        }

        [STAThread]
        static void Main(string[] args)
        {
            string[] cliFiles;
            string cliOut;
            if (TryParseCli(args, out cliFiles, out cliOut))
            {
                RunCli(cliFiles, cliOut);
                return;
            }
            try { System.Windows.Forms.Application.EnableVisualStyles(); } catch { }
            try { System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false); } catch { }
            System.Windows.Forms.Application.Run(new MainForm());
        }

        static bool TryParseCli(string[] args, out string[] files, out string outFile)
        {
            files = null; outFile = null;
            if (args == null || args.Length == 0) return false;
            var list = new List<string>();
            bool hasOutFlag = false;
            bool unknownFlag = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.IsNullOrEmpty(a)) continue;
                if (a.StartsWith("--out=", StringComparison.OrdinalIgnoreCase))
                { outFile = a.Substring(6); hasOutFlag = true; }
                else if (string.Equals(a, "--out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                { outFile = args[++i]; hasOutFlag = true; }
                else if (a.StartsWith("-"))
                { unknownFlag = true; }
                else if (File.Exists(a))
                    list.Add(Path.GetFullPath(a));
            }
            files = list.ToArray();
            // 有 --out 即认定是命令行模式；否则须至少有一个有效图片文件
            bool cli = hasOutFlag || (files.Length > 0 && !unknownFlag);
            if (!cli) { files = null; outFile = null; }
            return cli;
        }

        static void RunCli(string[] files, string outFile)
        {
            IntPtr h = Native.GetConsoleWindow();
            if (h == IntPtr.Zero) Native.AttachConsole(-1);
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            try
            {
                if (files == null || files.Length == 0)
                    throw new ArgumentException("没有有效的图片文件（检查路径是否加引号）");
                if (string.IsNullOrEmpty(outFile))
                {
                    string dir = Path.GetDirectoryName(files[0]);
                    outFile = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd") + "-.docx");
                }
                else if (Path.GetDirectoryName(outFile.TrimEnd('\\', '/')).Length == 0)
                {
                    // 只给了文件名：输出到第一张图片所在文件夹
                    outFile = Path.Combine(Path.GetDirectoryName(files[0]), outFile);
                }
                var cfg = new Config();
                List<string> errors;
                List<Page> pages = Sizer.Build(files, cfg, null, out errors);
                // 成功页数为 0 判整批失败：不产出空文档（与 GUI 一致，BR-09）
                if (pages.Count == 0)
                {
                    string detail = errors.Count > 0 ? string.Join("\n", errors.ToArray()) : "没有可处理的图片。";
                    throw new InvalidOperationException("全部图片处理失败：\n" + detail);
                }
                // 用 Write 的返回值：重名/占用时实际落盘名可能已改名（绝不覆盖）
                string finalPath = DocxBuilder.Write(outFile, pages);
                Console.WriteLine(AppTitle + "（命令行模式）");
                Console.WriteLine("输出: " + finalPath);
                Console.WriteLine("页数: " + pages.Count + (errors.Count > 0 ? "，跳过 " + errors.Count + " 张失败图片" : ""));
                Native.MessageBeep(0xFFFFFFFFu);
            }
            catch (Exception ex)
            {
                Console.WriteLine("失败: " + ex.Message);
                Environment.ExitCode = 1;
            }
        }
    }

    static class Native
    {
        [DllImport("kernel32.dll")] public static extern bool AttachConsole(int pid);
        [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
        [DllImport("user32.dll")] public static extern bool MessageBeep(uint type);
    }
}
