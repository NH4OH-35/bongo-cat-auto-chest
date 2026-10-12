using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace BongoClaim
{
    internal static class EngineTests
    {
        private static int passed;
        private static string temp;
        private static string fixture;

        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Throws(Action action, string contains)
        {
            try { action(); }
            catch (Exception ex) { Assert(ex.Message.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0, "Unexpected error: " + ex.Message); return; }
            throw new Exception("Expected rejection: " + contains);
        }
        private static void Test(string title, Action action) { action(); passed++; Console.WriteLine("PASS " + title); }
        private static string NewFile(string stem) { return Path.Combine(temp, stem + "-" + Guid.NewGuid().ToString("N") + ".dll"); }
        private static string NewGame()
        {
            var root = Path.Combine(temp, "game-" + Guid.NewGuid().ToString("N"));
            var dll = PatchEngine.GameAssembly(root); Directory.CreateDirectory(Path.GetDirectoryName(dll)); File.Copy(fixture, dll); File.WriteAllText(Path.Combine(root, "BongoCat.exe"), "source-owned test fixture"); return root;
        }
        private static Assembly Patched(bool emotes)
        {
            var output = NewFile("runtime"); PatchEngine.PatchCopy(fixture, output, emotes); return Assembly.Load(File.ReadAllBytes(output));
        }
        private static object Shop(Assembly asm, bool ready, bool opening, bool emote)
        {
            var shop = Activator.CreateInstance(asm.GetType("BongoCat.Shop")); Call(shop, "Configure", ready, opening, emote); return shop;
        }
        private static object Call(object instance, string method, params object[] args) { return instance.GetType().GetMethod(method).Invoke(instance, args); }
        private static int Purchases(object shop) { var item = Call(shop, "GetItem"); return (int)item.GetType().GetField("Purchases").GetValue(item); }
        private static void Allow(Assembly asm, bool allowed)
        {
            var pets = asm.GetType("BongoCat.Pets"); var instance = pets.GetField("Instance").GetValue(null); pets.GetField("Allowed").SetValue(instance, allowed);
        }

        public static int Main()
        {
            temp = Path.Combine(Path.GetTempPath(), "BongoClaimTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assembly-CSharp.dll");
            try
            {
                Test("recognises source-owned current-shape fixture", delegate { Assert(PatchEngine.Inspect(fixture).Supported, "Fixture unsupported"); });
                Test("normal ready chest claims once and suppresses its popup", delegate {
                    var asm = Patched(false); var shop = Shop(asm, true, false, false); Call(shop, "OnChestReady"); Call(shop, "OnChestReady");
                    Assert(Purchases(shop) == 1, "Duplicate or missing claim"); Assert((int)shop.GetType().GetField("Popups").GetValue(shop) == 1, "First successful claim should suppress popup; second remains original behavior");
                });
                Test("unready, unaffordable and already-opening chests do not claim", delegate {
                    var asm = Patched(false); var shop = Shop(asm, false, false, false); Call(shop, "OnChestReady"); Assert(Purchases(shop) == 0, "Unready claimed");
                    Call(shop, "Configure", true, true, false); Call(shop, "OnChestReady"); Assert(Purchases(shop) == 0, "In-flight claimed");
                    Call(shop, "Configure", true, false, false); Allow(asm, false); Call(shop, "OnChestReady"); Assert(Purchases(shop) == 0, "Unaffordable claimed");
                });
                Test("normal-only mode leaves emote purchases manual", delegate { var asm = Patched(false); var shop = Shop(asm, true, false, true); Call(shop, "OnChestReady"); Assert(Purchases(shop) == 0, "Emote claimed by default"); });
                Test("emote opt-in uses the same guarded purchase path", delegate { var asm = Patched(true); var shop = Shop(asm, true, false, true); Call(shop, "OnChestReady"); Assert(Purchases(shop) == 1, "Opt-in did not claim"); });
                Test("existing server-wait guard still blocks a second request", delegate { var asm = Patched(false); var shop = Shop(asm, true, false, false); var item = Call(shop, "GetItem"); Call(item, "SetWaiting", true); Call(shop, "OnChestReady"); Assert(Purchases(shop) == 0, "Server guard bypassed"); });
                Test("timer retries when a chest is ready and popup already shown", delegate {
                    var asm = Patched(false); var shop = Shop(asm, true, false, false); shop.GetType().GetField("Shown").SetValue(shop, true);
                    Allow(asm, false); var timer = (IEnumerator)Call(shop, "TimerUpdate"); timer.MoveNext(); Assert(Purchases(shop) == 0, "Unaffordable timer claim");
                    Allow(asm, true); timer.MoveNext(); timer.MoveNext(); Assert(Purchases(shop) == 1, "Timer failed or duplicated");
                });
                Test("null shop item is ignored without crashing", delegate { var asm = Patched(false); var shop = Shop(asm, true, false, false); Call(shop, "ClearItem"); Call(shop, "OnChestReady"); });
                Test("unrelated game behavior remains unchanged", delegate { var asm = Patched(false); var shop = Shop(asm, false, false, false); Assert((int)shop.GetType().GetField("UnrelatedFeature").GetValue(shop) == 42, "Unrelated behavior changed"); });
                Test("patch copy never modifies the original", delegate { var original = PatchEngine.Hash(fixture); var copy = NewFile("copy"); PatchEngine.PatchCopy(fixture, copy, false); Assert(PatchEngine.Hash(fixture) == original, "Source modified"); Assert(PatchEngine.Inspect(copy).Installed, "Patch missing"); });
                Test("patch copy rejects overwriting its source or existing output", delegate { Throws(delegate { PatchEngine.PatchCopy(fixture, fixture, false); }, "separate"); var output = NewFile("existing"); File.Copy(fixture, output); Throws(delegate { PatchEngine.PatchCopy(fixture, output, false); }, "already exists"); });
                Test("install is idempotent and restore is byte-for-byte", delegate {
                    var root = NewGame(); var target = PatchEngine.GameAssembly(root); var original = PatchEngine.Hash(target);
                    PatchEngine.Install(root, false); var changed = PatchEngine.Hash(target); Assert(changed != original, "No patch");
                    Assert(PatchEngine.Install(root, false).StartsWith("Already"), "Not idempotent"); Assert(PatchEngine.Hash(target) == changed, "Installed twice");
                    PatchEngine.Restore(root); Assert(PatchEngine.Hash(target) == original, "Restore not exact"); Assert(PatchEngine.Restore(root).Contains("already restored"), "Restore not idempotent");
                });
                Test("restore refuses a changed game build", delegate { var root = NewGame(); PatchEngine.Install(root, false); File.AppendAllText(PatchEngine.GameAssembly(root), "steam-update"); Throws(delegate { PatchEngine.Restore(root); }, "changed after installation"); });
                Test("restore refuses a corrupted backup", delegate {
                    var root = NewGame(); PatchEngine.Install(root, false); var state = PatchEngine.LoadState(root); File.AppendAllText(Path.Combine(root, ".bongo-claim", "backups", state.OriginalHash + ".dll"), "corruption"); Throws(delegate { PatchEngine.Restore(root); }, "damaged");
                });
                Test("reinstall after a new compatible build keeps old backups", delegate {
                    var root = NewGame(); PatchEngine.Install(root, false); var old = PatchEngine.LoadState(root);
                    using (var asm = AssemblyDefinition.ReadAssembly(fixture)) { asm.MainModule.Mvid = Guid.NewGuid(); asm.Write(PatchEngine.GameAssembly(root)); }
                    var updatedHash = PatchEngine.Hash(PatchEngine.GameAssembly(root)); PatchEngine.Install(root, false); Assert(PatchEngine.LoadState(root).OriginalHash == updatedHash, "Update backed up wrong");
                    Assert(File.Exists(Path.Combine(root, ".bongo-claim", "backups", old.OriginalHash + ".dll")), "Old backup lost"); PatchEngine.Restore(root); Assert(PatchEngine.Hash(PatchEngine.GameAssembly(root)) == updatedHash, "Restored old version");
                });
                Test("unknown shape is rejected before install changes anything", delegate {
                    var root = NewGame(); var target = PatchEngine.GameAssembly(root);
                    using (var asm = AssemblyDefinition.ReadAssembly(fixture)) { asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Shop").Methods.Single(m => m.Name == "OnChestReady").Name = "RenamedByUpdate"; asm.Write(target); }
                    var hash = PatchEngine.Hash(target); Throws(delegate { PatchEngine.Install(root, false); }, "Unsupported"); Assert(PatchEngine.Hash(target) == hash, "Unknown build modified");
                });
                Test("rejects another auto-claim mod rather than stacking patches", delegate {
                    var input = NewFile("othermod");
                    using (var asm = AssemblyDefinition.ReadAssembly(fixture)) {
                        var shop = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Shop"); var method = shop.Methods.Single(m => m.Name == "OnChestReady"); var buy = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.ShopItem").Methods.Single(m => m.Name == "Buy");
                        method.Body.GetILProcessor().InsertBefore(method.Body.Instructions.Last(), Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Callvirt, buy)); asm.Write(input);
                    }
                    Assert(!PatchEngine.Inspect(input).Supported, "Other mod accepted");
                });
                Console.WriteLine("All " + passed + " tests passed."); return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
            finally { Directory.Delete(temp, true); }
        }
    }
}
