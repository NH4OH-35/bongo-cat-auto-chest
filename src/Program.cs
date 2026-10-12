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
                    bool emotes = Array.IndexOf(args, "--emotes") >= 0;
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
                            if (args.Length < 3) throw new ArgumentException("Usage: --patch-copy <input DLL> <new output DLL> [--emotes]");
                            PatchEngine.PatchCopy(args[1], args[2], emotes);
                            Console.WriteLine("Patched copy verified. Original untouched.");
                            return 0;
                        case "--install":
                            if (args.Length < 2) throw new ArgumentException("Usage: --install <game folder> [--emotes]");
                            Console.WriteLine(PatchEngine.Install(args[1], emotes));
                            return 0;
                        case "--restore":
                            if (args.Length < 2) throw new ArgumentException("Usage: --restore <game folder>");
                            Console.WriteLine(PatchEngine.Restore(args[1]));
                            return 0;
                        default:
                            Console.WriteLine("BongoClaim 1.0.0 — run without arguments for the desktop app.");
                            Console.WriteLine("Commands: --inspect, --patch-copy, --install, --restore. Optional: --emotes.");
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
