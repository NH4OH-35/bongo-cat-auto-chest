using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace BongoClaim
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            return Run(args);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Run(string[] args)
        {
            if (args.Length > 0)
            {
                try
                {
                    bool emotes = Array.IndexOf(args, "--normal-only") < 0;
                    if (!emotes && Array.IndexOf(args, "--emotes") >= 0) throw new ArgumentException("Choose either --normal-only or --emotes, not both.");
                    switch (args[0])
                    {
                        case "--inspect":
                            if (args.Length < 2) throw new ArgumentException("Usage: --inspect <game folder or assembly DLL>");
                            var path = Directory.Exists(args[1]) ? PatchEngine.GameAssembly(args[1]) : args[1];
                            var result = PatchEngine.Inspect(path);
                            Console.WriteLine(result.Message);
                            Console.WriteLine("SHA256: " + result.Hash);
                            return result.Supported ? 0 : 2;
                        case "--patch-copy":
                            if (args.Length < 3) throw new ArgumentException("Usage: --patch-copy <input DLL> <new output DLL> [--normal-only]");
                            PatchEngine.PatchCopy(args[1], args[2], emotes);
                            Console.WriteLine("Patched copy verified. Original untouched.");
                            return 0;
                        case "--install":
                            if (args.Length < 2) throw new ArgumentException("Usage: --install <game folder> [--normal-only]");
                            Console.WriteLine(PatchEngine.Install(args[1], emotes));
                            return 0;
                        case "--restore":
                            if (args.Length < 2) throw new ArgumentException("Usage: --restore <game folder>");
                            Console.WriteLine(PatchEngine.Restore(args[1]));
                            return 0;
                        default:
                            Console.WriteLine("Bongo Cat Auto Chest 1.2.0 — run without arguments for the desktop app.");
                            Console.WriteLine("Commands: --inspect, --patch-copy, --install, --restore. Both chest types start enabled.");
                            Console.WriteLine("Optional: --normal-only starts emote claims disabled; --emotes is accepted for compatibility.");
                            Console.WriteLine("After installation, use Bongo Cat Settings > General for saved toggles, owned tokens and pet controls.");
                            Console.WriteLine("Keyboard: open the game menu, then F7 for normal or F9 for emote. Choices are saved.");
                            Console.WriteLine("F10 edits available pets: type digits, Enter saves, Escape cancels. Keep pets starts off.");
                            return args[0] == "--help" ? 0 : 2;
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine(Array.IndexOf(args, "--verbose") >= 0 ? ex.ToString() : ex.Message); return 1; }
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
