using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
        private static void SetOption(Assembly asm, string key, bool enabled)
        {
            asm.GetType("UnityEngine.PlayerPrefs").GetMethod("SetInt").Invoke(null, new object[] { key, enabled ? 1 : 0 });
        }
        private static int GetOption(Assembly asm, string key, int fallback)
        {
            return (int)asm.GetType("UnityEngine.PlayerPrefs").GetMethod("GetInt").Invoke(null, new object[] { key, fallback });
        }
        private static object Settings(Assembly asm)
        {
            return asm.GetType("BongoCat.SettingsFixture").GetMethod("Create").Invoke(null, null);
        }
        private static object Pets(Assembly asm) { return asm.GetType("BongoCat.Pets").GetField("Instance").GetValue(null); }
        private static int Balance(Assembly asm) { return (int)Pets(asm).GetType().GetProperty("Balance").GetValue(Pets(asm), null); }
        private static void Quantity(Assembly asm, string name, long quantity) {
            var field = asm.GetType("SteamTools.Game+Inventory").GetField(name); var value = field.GetValue(null);
            value.GetType().GetField("Quantity").SetValue(value, quantity); field.SetValue(null, value);
        }
        private static long Quantity(Assembly asm, string name) {
            var value = asm.GetType("SteamTools.Game+Inventory").GetField(name).GetValue(null); return (long)Call(value, "GetTotalQuantity");
        }
        private static void TypeScore(object settings, string score) { foreach (char digit in score) Call(settings, "KeyFrame", 48 + digit - '0'); }
        private static void FeatureTests() {
            Test("owned normal tokens open during countdown without enabling auto claim", delegate {
                var asm = Patched(true); SetOption(asm, PatchEngine.NormalKey, false);
                var shop = Shop(asm, false, false, false); var settings = Settings(asm); Call(settings, "Initialize");
                // Settings resets the scene; recreate a discoverable shop afterward.
                shop = Shop(asm, false, false, false); Quantity(asm, "Chest_Token", 3); Call(settings, "Refresh");
                Assert(((string)Call(settings, "RowLabel", 4)).Contains("3"), "Owned-token count missing");
                Call(settings, "ClickRow", 4); Assert(Purchases(shop) == 1, "Owned token waited for countdown");
                Call(settings, "ClickRow", 4); Assert(Purchases(shop) == 1, "Repeated click duplicated pending request");
                Call(Call(shop, "GetItem"), "Complete", true); Call(settings, "Refresh");
                Assert(Quantity(asm, "Chest_Token") == 2 && Balance(asm) == 9000, "Successful claim did not consume exactly one token and price");
                Assert((int)shop.GetType().GetField("StockRefreshTimeLeft").GetValue(shop) == 1800, "Successful claim altered cooldown");
                Call(settings, "ClickRow", 4); Assert(Purchases(shop) == 2, "Second owned token could not be opened during countdown");
            });
            Test("owned emote button consumes only the matching token", delegate {
                var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                var normal = Shop(asm, false, false, false); var emote = Shop(asm, false, false, true);
                Quantity(asm, "Emote_Chest_Token", 5); Quantity(asm, "Chest_Token", 8); Call(settings, "Refresh");
                Call(settings, "ClickRow", 5); Assert(Purchases(emote) == 1 && Purchases(normal) == 0, "Emote action selected wrong shop");
                Call(Call(emote, "GetItem"), "Complete", true); Call(settings, "Refresh");
                Assert(Quantity(asm, "Emote_Chest_Token") == 4 && Quantity(asm, "Chest_Token") == 8, "Wrong token consumed");
            });
            Test("zero tokens, missing exchange inputs and busy inventory block owned claims", delegate {
                foreach (var token in new[] { "Chest_Token", "Achievement_Token", "ManualToken" }) {
                    var asm = Patched(true); var shop = Shop(asm, false, false, false); Quantity(asm, token, 0);
                    Assert(!(bool)Call(shop, "__BongoClaim_OpenOwned_v1", false) && Purchases(shop) == 0, "Claim ignored missing " + token);
                }
                var other = Patched(true); var guarded = Shop(other, false, false, false);
                other.GetType("Steam.ChestExchanger").GetField("IsExchanging", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
                Assert(!(bool)Call(guarded, "__BongoClaim_OpenOwned_v1", false), "Global exchange lock ignored");
                other.GetType("Steam.ChestExchanger").GetField("IsExchanging", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
                Call(Call(guarded, "GetItem"), "SetExchangeAllowed", false);
                Assert(!(bool)Call(guarded, "__BongoClaim_OpenOwned_v1", false), "Full exchange requirements ignored");
            });
            Test("owned claims respect price, pending server state and shop type", delegate {
                var asm = Patched(true); var shop = Shop(asm, false, false, false); Allow(asm, false);
                Assert(!(bool)Call(shop, "__BongoClaim_OpenOwned_v1", false), "Owned claim bypassed default price");
                SetOption(asm, PatchEngine.KeepPetsKey, true); Call(Call(shop, "GetItem"), "SetWaiting", true);
                Assert(!(bool)Call(shop, "__BongoClaim_OpenOwned_v1", false), "Keep pets bypassed request lock");
                Call(Call(shop, "GetItem"), "SetWaiting", false);
                Assert(!(bool)Call(shop, "__BongoClaim_OpenOwned_v1", true), "Wrong shop type opened");
                shop.GetType().GetMethod("ClearItem").Invoke(shop, null); Assert(!(bool)Call(shop, "__BongoClaim_OpenOwned_v1", false), "Null item accepted");
            });
            Test("failed owned-token request leaves inventory and pets intact", delegate {
                var asm = Patched(true); var shop = Shop(asm, false, false, false); Call(shop, "__BongoClaim_OpenOwned_v1", false);
                Call(Call(shop, "GetItem"), "Complete", false);
                Assert(Quantity(asm, "Chest_Token") == 2 && Balance(asm) == 10000, "Failure consumed token or pets");
                Assert((int)shop.GetType().GetField("StockRefreshTimeLeft").GetValue(shop) == 60, "Failure cooldown changed");
            });
            Test("token rows show loading, disable empty actions and preserve original callbacks", delegate {
                var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                asm.GetType("SteamTools.Interface").GetProperty("IsReady").SetValue(null, false, null); Call(settings, "Refresh");
                Assert(((string)Call(settings, "RowLabel", 4)).Contains("loading"), "Unknown inventory displayed as zero");
                var shop = Shop(asm, false, false, false); Call(settings, "ClickRow", 4); Assert(Purchases(shop) == 0, "Loading row was clickable");
                asm.GetType("SteamTools.Interface").GetProperty("IsReady").SetValue(null, true, null); Quantity(asm, "Chest_Token", 0); Call(settings, "Refresh"); Call(settings, "ClickRow", 4);
                Assert(Purchases(shop) == 0 && !(bool)Call(settings, "RowCheckboxVisible", 4), "Empty token action or checkbox incorrect");
                Assert((int)settings.GetType().GetField("SourceButtonClicks").GetValue(settings) == 0 && (int)settings.GetType().GetField("SourceEvents").GetValue(settings) == 0, "Action invoked original preference listeners");
            });
            Test("keep-pets control defaults off and saves independently", delegate {
                var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                Assert(!(bool)Call(settings, "RowToggleActive", 3), "Keep pets silently started on");
                Call(settings, "ClickRow", 3);
                Assert(GetOption(asm, PatchEngine.KeepPetsKey, 0) == 1 && GetOption(asm, PatchEngine.NormalKey, 1) == 1 && GetOption(asm, PatchEngine.EmoteKey, 1) == 1, "Keep pets changed another preference");
                Call(settings, "ClickRow", 3); Assert(GetOption(asm, PatchEngine.KeepPetsKey, 1) == 0, "Keep pets could not be disabled");
            });
            Test("menu shortcuts select owned token type and keep-pets state with focus guards", delegate {
                var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                var normal = Shop(asm, false, false, false); var emote = Shop(asm, false, false, true); Call(Pets(asm), "ConfigureStats", 0, 0, true);
                foreach (var context in new[] { new[] { false, true, false }, new[] { true, false, false }, new[] { true, true, true } }) {
                    Call(settings, "SetContext", context[0], context[1], context[2]); Call(settings, "KeyFrame", 286); Call(settings, "KeyFrame", 292);
                    Assert(Purchases(normal) == 0 && GetOption(asm, PatchEngine.KeepPetsKey, 0) == 0, "Blocked shortcut acted");
                }
                Call(settings, "SetContext", true, true, false); Call(settings, "KeyFrame", 286); Assert(GetOption(asm, PatchEngine.KeepPetsKey, 0) == 1 && (bool)Call(settings, "RowToggleActive", 3), "F5 did not update saved native control");
                Call(settings, "KeyFrame", 292); Call(settings, "KeyFrame", 292); Assert(Purchases(normal) == 1 && Purchases(emote) == 0, "F11 duplicated or selected wrong type");
                Call(Call(normal, "GetItem"), "Complete", true); Call(settings, "KeyFrame", 277); Assert(Purchases(emote) == 1, "Insert did not select emote");
                Call(Call(emote, "GetItem"), "Complete", true); Assert(Balance(asm) == 0, "Shortcut claims deducted pets");
                Call(settings, "KeyFrame", 277); Assert(Purchases(emote) == 2 && Purchases(normal) == 1, "Emote shortcut selected the normal chest");
            });
            Test("keep-pets claims work at zero score and preserve both chest prices when disabled", delegate {
                foreach (bool emote in new[] { false, true }) {
                    var asm = Patched(true); Call(Pets(asm), "ConfigureStats", 0, 0, true); SetOption(asm, PatchEngine.KeepPetsKey, true);
                    var shop = Shop(asm, true, false, emote); Call(shop, "OnChestReady"); Call(Call(shop, "GetItem"), "Complete", true);
                    Assert(Purchases(shop) == 1 && Balance(asm) == 0, "Keep-pets claim charged or rejected zero score");
                    SetOption(asm, PatchEngine.KeepPetsKey, false); var next = Shop(asm, true, false, emote); Call(next, "OnChestReady"); Assert(Purchases(next) == 0, "Disabling keep pets did not restore price check");
                }
            });
            Test("price choice is captured per request and gift spending is unchanged", delegate {
                var asm = Patched(true); var shop = Shop(asm, false, false, false); var item = Call(shop, "GetItem");
                Call(item, "Buy"); SetOption(asm, PatchEngine.KeepPetsKey, true); Call(item, "Complete", true); Assert(Balance(asm) == 9000, "Switch retroactively changed pending request");
                Call(item, "Buy"); SetOption(asm, PatchEngine.KeepPetsKey, false); Call(item, "Complete", true); Assert(Balance(asm) == 9000, "Switch charged a captured free request");
                SetOption(asm, PatchEngine.KeepPetsKey, true); Assert((bool)Call(item, "TrySpendForGift") && Balance(asm) == 8000, "Keep pets changed gift spending");
            });
            Test("score editor saves available pets without changing lifetime or total spent", delegate {
                var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize"); Call(Pets(asm), "ConfigureStats", 10000, 2500, true);
                Call(settings, "ClickRow", 6); TypeScore(settings, "12345"); Call(settings, "KeyFrame", 13);
                Assert(Balance(asm) == 12345 && (int)Pets(asm).GetType().GetProperty("TotalSpent").GetValue(Pets(asm), null) == 2500 && (int)Pets(asm).GetType().GetField("Lifetime").GetValue(Pets(asm)) == 10000, "Editor changed unrelated score fields");
                Assert((int)asm.GetType("Heathen.SteamworksIntegration.API.StatsAndAchievements+Client").GetField("Stores").GetValue(null) == 1, "Editor did not save Steam stats");
            });
            Test("score editor cancels, supports backspace and blocks other menu hotkeys while typing", delegate {
                var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize"); Call(settings, "KeyFrame", 291); TypeScore(settings, "999"); Call(settings, "Frame", true, true);
                Assert(GetOption(asm, PatchEngine.NormalKey, 1) == 1 && GetOption(asm, PatchEngine.EmoteKey, 1) == 1, "Typing toggled auto claim");
                Call(settings, "KeyFrame", 27); Assert(Balance(asm) == 10000, "Cancel saved score");
                Call(settings, "ClickRow", 6); TypeScore(settings, "12"); Call(settings, "KeyFrame", 8); TypeScore(settings, "3"); Call(settings, "KeyFrame", 271); Assert(Balance(asm) == 13, "Backspace or keypad Enter failed");
            });
            Test("score changes reject overflow, negative values and uninitialised stats", delegate {
                var asm = Patched(true); var pets = Pets(asm);
                Assert(!(bool)Call(pets, "__BongoClaim_SetScore_v1", -1), "Negative score accepted");
                Call(pets, "ConfigureStats", 2000, 1000, true); Assert(!(bool)Call(pets, "__BongoClaim_SetScore_v1", Int32.MaxValue), "Overflow score accepted");
                Assert(Balance(asm) == 1000, "Invalid score modified balance");
                Call(pets, "ConfigureStats", 2000, 1000, false); Assert(!(bool)Call(pets, "__BongoClaim_SetScore_v1", 5), "Uninitialized stats modified");
                Call(pets, "ConfigureStats", 2000, 1000, true); Assert((bool)Call(pets, "__BongoClaim_SetScore_v1", 0) && Balance(asm) == 0, "Zero score rejected");
            });
            Test("score input ignores unfocused and text-field contexts and cancels on menu close", delegate {
                var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                Call(settings, "SetContext", false, true, false); Call(settings, "KeyFrame", 291); Call(settings, "SetContext", true, true, false); TypeScore(settings, "5"); Call(settings, "KeyFrame", 13); Assert(Balance(asm) == 10000, "Unfocused F10 entered editor");
                Call(settings, "ClickRow", 6); Call(settings, "SetContext", true, true, true); TypeScore(settings, "9"); Call(settings, "SetContext", true, true, false); TypeScore(settings, "7"); Call(settings, "SetContext", true, false, false); Call(settings, "Refresh");
                Call(settings, "SetContext", true, true, false); Call(settings, "KeyFrame", 13); Assert(Balance(asm) == 10000, "Menu close committed editing buffer");
            });
            Test("runtime template dependencies and compile-time stub types never enter patched assembly", delegate {
                var output = NewFile("stubs"); PatchEngine.PatchCopy(fixture, output, true);
                using (var source = AssemblyDefinition.ReadAssembly(fixture)) using (var asm = AssemblyDefinition.ReadAssembly(output, new ReaderParameters { InMemory = true })) {
                    Assert(!asm.MainModule.AssemblyReferences.Any(a => a.Name.Contains("RuntimeTemplate")), "Runtime template became a game dependency");
                    Assert(asm.MainModule.GetTypes().Select(t => t.FullName).SequenceEqual(source.MainModule.GetTypes().Select(t => t.FullName)), "Stub types were shipped into game");
                }
            });
            Test("v2 upgrade rebuilds token and score controls from the original backup", delegate {
                var root = NewGame(); var target = PatchEngine.GameAssembly(root); var original = PatchEngine.Hash(target);
                WriteLegacyPatch(target, true);
                using (var asm = AssemblyDefinition.ReadAssembly(target, new ReaderParameters { InMemory = true })) {
                    var shop = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Shop");
                    shop.Methods.Single(m => m.Name == PatchEngine.LegacyMarker).Name = PatchEngine.V2Marker;
                    shop.Fields.Single(f => f.Name == "__BongoClaim_Emotes_v1").Name = PatchEngine.V2SettingsMarker;
                    NativeControlsPatch.Apply(asm, true); asm.Write(target);
                }
                SaveLegacyState(root, original, PatchEngine.Hash(target), true);
                var prior = PatchEngine.Inspect(target); Assert(prior.Supported && prior.RuntimeControls && !prior.OwnedTokenControls, "v2 misidentified");
                PatchEngine.Install(root, true); Assert(PatchEngine.Inspect(target).OwnedTokenControls && PatchEngine.LoadState(root).Version == 3, "v2 upgrade omitted new controls");
                Assert(PatchEngine.LoadState(root).OriginalHash == original, "v2 upgrade backed up modified DLL");
                PatchEngine.Restore(root); Assert(PatchEngine.Hash(target) == original, "v2 upgrade restore not exact");
            });
            Test("unknown token layouts and incomplete new controls are rejected", delegate {
                var input = NewFile("missing-token");
                using (var asm = AssemblyDefinition.ReadAssembly(fixture)) { asm.MainModule.GetTypes().Single(t => t.FullName == "SteamTools.Game/Inventory").Fields.Single(f => f.Name == "Chest_Token").Name = "ChangedToken"; asm.Write(input); }
                Assert(!PatchEngine.Inspect(input).Supported, "Unknown token field accepted");
                var output = NewFile("missing-score"); PatchEngine.PatchCopy(fixture, output, true);
                using (var asm = AssemblyDefinition.ReadAssembly(output, new ReaderParameters { InMemory = true })) { var pets = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Pets"); pets.Methods.Single(m => m.Name == "__BongoClaim_SetScore_v1").Name = "RemovedFeature"; asm.Write(output); }
                Assert(!PatchEngine.Inspect(output).Supported, "Incomplete score control accepted");
            });
        }
        private static void Prepend(MethodDefinition method, params Instruction[] instructions)
        {
            var first = method.Body.Instructions[0]; var il = method.Body.GetILProcessor();
            foreach (var instruction in instructions) il.InsertBefore(first, instruction);
            method.Body.MaxStackSize = Math.Max(method.Body.MaxStackSize, 4);
        }
        // Reproduce the published v1 patch against our source-owned fixture so
        // upgrade tests do not depend on shipping a game DLL or a legacy binary.
        private static void WriteLegacyPatch(string output, bool emotes)
        {
            using (var asm = AssemblyDefinition.ReadAssembly(fixture))
            {
                var shop = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Shop");
                var item = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.ShopItem");
                var helper = new MethodDefinition("__BongoClaim_TryClaim_v1", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig, asm.MainModule.TypeSystem.Boolean);
                shop.Methods.Add(helper);
                shop.Fields.Add(new FieldDefinition("__BongoClaim_Emotes_v1", Mono.Cecil.FieldAttributes.Private | Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Literal | Mono.Cecil.FieldAttributes.HasDefault, asm.MainModule.TypeSystem.Boolean) { Constant = emotes });
                var il = helper.Body.GetILProcessor(); var no = Instruction.Create(OpCodes.Ldc_I4_0);
                Action<string, bool> guard = delegate(string fieldName, bool failWhenTrue) {
                    il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, shop.Fields.Single(f => f.Name == fieldName))); il.Append(Instruction.Create(failWhenTrue ? OpCodes.Brtrue : OpCodes.Brfalse, no));
                };
                if (!emotes) guard("_isEmoteShop", true);
                guard("ChestIsReady", false); guard("_openingChest", true); guard("_shopItem", false);
                var itemField = shop.Fields.Single(f => f.Name == "_shopItem");
                il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, itemField)); il.Append(Instruction.Create(OpCodes.Callvirt, item.Methods.Single(m => m.Name == "CanBuy"))); il.Append(Instruction.Create(OpCodes.Brfalse, no));
                il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, itemField)); il.Append(Instruction.Create(OpCodes.Callvirt, item.Methods.Single(m => m.Name == "Buy")));
                il.Append(Instruction.Create(OpCodes.Ldc_I4_1)); il.Append(Instruction.Create(OpCodes.Ret)); il.Append(no); il.Append(Instruction.Create(OpCodes.Ret)); helper.Body.MaxStackSize = 2;
                var ready = shop.Methods.Single(m => m.Name == "OnChestReady"); var readyStart = ready.Body.Instructions[0];
                Prepend(ready, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, helper), Instruction.Create(OpCodes.Brfalse, readyStart), Instruction.Create(OpCodes.Ret));
                var timer = shop.NestedTypes.Single(t => t.Name.StartsWith("<TimerUpdate>")); var tick = timer.Methods.Single(m => m.Name == "MoveNext"); var tickStart = tick.Body.Instructions[0]; var callHelper = Instruction.Create(OpCodes.Call, helper);
                Prepend(tick, Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, timer.Fields.Single(f => f.Name == "<>4__this")), Instruction.Create(OpCodes.Dup), Instruction.Create(OpCodes.Brtrue, callHelper), Instruction.Create(OpCodes.Pop), Instruction.Create(OpCodes.Br, tickStart), callHelper, Instruction.Create(OpCodes.Pop));
                asm.MainModule.Mvid = Guid.NewGuid(); asm.Write(output);
            }
        }
        private static void SaveLegacyState(string root, string originalHash, string patchedHash, bool emotes)
        {
            var directory = Path.Combine(root, ".bongo-claim"); Directory.CreateDirectory(Path.Combine(directory, "backups"));
            File.Copy(fixture, Path.Combine(directory, "backups", originalHash + ".dll"));
            using (var stream = File.Create(Path.Combine(directory, "install.json")))
                new DataContractJsonSerializer(typeof(InstallState)).WriteObject(stream, new InstallState { Version = 1, OriginalHash = originalHash, PatchedHash = patchedHash, Emotes = emotes, InstalledUtc = DateTime.UtcNow.ToString("o") });
        }

        private static void StartupTests()
        {
            Func<string> newSteamGame = delegate {
                var steamApps = Path.Combine(temp, "steamapps-" + Guid.NewGuid().ToString("N"));
                var root = Path.Combine(steamApps, "common", "BongoCat");
                var target = PatchEngine.GameAssembly(root); Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(fixture, target); File.WriteAllText(Path.Combine(root, "BongoCat.exe"), "source-owned launcher fixture");
                return root;
            };
            Func<string, string> manifestPath = root => Path.Combine(Directory.GetParent(root).Parent.FullName, "appmanifest_3419430.acf");
            Func<string, string[]> snapshot = root => Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).Select(p => p.Substring(root.Length) + ":" + PatchEngine.Hash(p)).ToArray();
            Test("launcher preparation installs and verifies the current controls and original backup", delegate {
                var root = NewGame(); var target = PatchEngine.GameAssembly(root); var original = PatchEngine.Hash(target);
                var ready = StartupLauncher.Prepare(root); var state = PatchEngine.LoadState(root);
                Assert(ready.Supported && ready.Installed && ready.RuntimeControls && ready.OwnedTokenControls && ready.Emotes, "Launcher did not verify all current controls");
                Assert(ready.Hash == PatchEngine.Hash(target) && ready.Hash != original, "Launcher returned an unpatched or stale inspection");
                Assert(state.OriginalHash == original && state.PatchedHash == ready.Hash, "Launcher did not retain the verified install hashes");
                Assert(PatchEngine.Hash(Path.Combine(root, ".bongo-claim", "backups", original + ".dll")) == original, "Launcher lost or changed the original backup");
            });
            Test("repeated launcher preparation leaves the installed DLL and state untouched", delegate {
                var root = NewGame(); var first = StartupLauncher.Prepare(root); var target = PatchEngine.GameAssembly(root);
                var statePath = Path.Combine(root, ".bongo-claim", "install.json"); var state = File.ReadAllText(statePath);
                var dllWrite = File.GetLastWriteTimeUtc(target); var stateWrite = File.GetLastWriteTimeUtc(statePath); var before = snapshot(root);
                var second = StartupLauncher.Prepare(root);
                Assert(second.Hash == first.Hash && snapshot(root).SequenceEqual(before), "Second preparation rewrote or added an installed file");
                Assert(File.ReadAllText(statePath) == state && File.GetLastWriteTimeUtc(target) == dllWrite && File.GetLastWriteTimeUtc(statePath) == stateWrite, "Second preparation replaced the DLL or install state");
            });
            Test("launcher repairs a compatible Steam replacement and retains both original backups", delegate {
                var root = NewGame(); StartupLauncher.Prepare(root); var prior = PatchEngine.LoadState(root); var target = PatchEngine.GameAssembly(root);
                using (var asm = AssemblyDefinition.ReadAssembly(fixture)) { asm.MainModule.Mvid = Guid.NewGuid(); asm.Write(target); }
                var updated = PatchEngine.Hash(target); Assert(updated != prior.OriginalHash, "Replacement fixture did not represent a new build");
                var repaired = StartupLauncher.Prepare(root); var current = PatchEngine.LoadState(root);
                Assert(repaired.Supported && repaired.OwnedTokenControls && current.OriginalHash == updated && current.PatchedHash == repaired.Hash, "Launcher did not rebuild and verify the replacement");
                Assert(PatchEngine.Hash(Path.Combine(root, ".bongo-claim", "backups", prior.OriginalHash + ".dll")) == prior.OriginalHash, "Launcher overwrote the older build backup");
                Assert(PatchEngine.Hash(Path.Combine(root, ".bongo-claim", "backups", updated + ".dll")) == updated, "Launcher did not back up the new original build");
            });
            Test("launcher rejects an unsupported replacement without writing game or backup files", delegate {
                var root = NewGame(); StartupLauncher.Prepare(root); var target = PatchEngine.GameAssembly(root);
                using (var asm = AssemblyDefinition.ReadAssembly(fixture)) { asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Shop").Methods.Single(m => m.Name == "OnChestReady").Name = "UnsupportedUpdate"; asm.Write(target); }
                var before = snapshot(root); Throws(delegate { StartupLauncher.Prepare(root); }, "cannot be started");
                Assert(snapshot(root).SequenceEqual(before), "Rejected replacement modified the game, prior state or backups");
            });
            Test("launcher blocks Steam update flag 6 before creating a patch or backup", delegate {
                var root = newSteamGame(); var manifest = manifestPath(root); File.WriteAllText(manifest, "\"AppState\" { \"StateFlags\" \"6\" }");
                var before = snapshot(root); var manifestHash = PatchEngine.Hash(manifest);
                Assert(StartupLauncher.IsUpdatePending(File.ReadAllText(manifest)), "Pending update flags were accepted");
                Throws(delegate { StartupLauncher.Prepare(root); }, "pending or incomplete");
                Assert(snapshot(root).SequenceEqual(before) && !Directory.Exists(Path.Combine(root, ".bongo-claim")), "Update guard wrote a patch or backup before rejecting");
                Assert(PatchEngine.Hash(manifest) == manifestHash, "Update guard changed Steam's manifest");
            });
            Test("launcher accepts the fully installed Steam manifest flag 4", delegate {
                var root = newSteamGame(); var manifest = manifestPath(root); File.WriteAllText(manifest, "\"AppState\" { \"StateFlags\" \"4\" }"); var manifestHash = PatchEngine.Hash(manifest);
                Assert(!StartupLauncher.IsUpdatePending(File.ReadAllText(manifest)), "Fully installed state was blocked");
                var ready = StartupLauncher.Prepare(root); Assert(ready.Supported && ready.OwnedTokenControls, "Installed manifest prevented preparation");
                Assert(PatchEngine.Hash(manifest) == manifestHash, "Preparation changed Steam's manifest");
            });
            Test("launcher rejects noninstalled and unfamiliar Steam flag combinations without writes", delegate {
                foreach (var flags in new[] { "0", "2", "5", "1024", "18446744073709551615" }) {
                    var root = newSteamGame(); File.WriteAllText(manifestPath(root), "\"StateFlags\" \"" + flags + "\""); var before = snapshot(root);
                    Assert(StartupLauncher.IsUpdatePending(File.ReadAllText(manifestPath(root))), "Unready flag was accepted: " + flags);
                    Throws(delegate { StartupLauncher.Prepare(root); }, "pending or incomplete");
                    Assert(snapshot(root).SequenceEqual(before) && !Directory.Exists(Path.Combine(root, ".bongo-claim")), "Unready manifest wrote game files: " + flags);
                }
            });
            Test("launcher fails closed for missing, malformed and overflowing Steam manifest flags", delegate {
                Assert(StartupLauncher.IsUpdatePending(null), "Absent manifest text was accepted");
                foreach (var manifest in new[] { "", "\"AppState\" { }", "\"StateFlags\" \"ready\"", "\"StateFlags\" \"-4\"", "\"StateFlags\" \"18446744073709551616\"" }) {
                    var root = newSteamGame(); File.WriteAllText(manifestPath(root), manifest); var before = snapshot(root);
                    Assert(StartupLauncher.IsUpdatePending(manifest), "Malformed manifest text was accepted");
                    Throws(delegate { StartupLauncher.Prepare(root); }, "pending or incomplete");
                    Assert(snapshot(root).SequenceEqual(before) && !Directory.Exists(Path.Combine(root, ".bongo-claim")), "Malformed manifest wrote game files");
                }
            });
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
                Test("normal toggle applies to readiness and re-enables a visible ready chest live", delegate {
                    var asm = Patched(true); SetOption(asm, PatchEngine.NormalKey, false);
                    var shop = Shop(asm, true, false, false); Call(shop, "OnChestReady"); Assert(Purchases(shop) == 0, "Disabled normal chest claimed");
                    Assert((int)shop.GetType().GetField("Popups").GetValue(shop) == 1, "Manual ready popup was suppressed while disabled");
                    var timer = (IEnumerator)Call(shop, "TimerUpdate"); timer.MoveNext(); Assert(Purchases(shop) == 0, "Timer ignored disable toggle");
                    SetOption(asm, PatchEngine.NormalKey, true); timer.MoveNext(); timer.MoveNext(); Assert(Purchases(shop) == 1, "Live enable did not claim exactly once");
                });
                Test("emote toggle is independent of normal-chest auto claim", delegate {
                    var asm = Patched(true); SetOption(asm, PatchEngine.EmoteKey, false);
                    var emote = Shop(asm, true, false, true); Call(emote, "OnChestReady"); Assert(Purchases(emote) == 0, "Disabled emote chest claimed");
                    var normal = Shop(asm, true, false, false); Call(normal, "OnChestReady"); Assert(Purchases(normal) == 1, "Emote disable also disabled normal claim");
                    SetOption(asm, PatchEngine.NormalKey, false); SetOption(asm, PatchEngine.EmoteKey, true);
                    var secondNormal = Shop(asm, true, false, false); Call(secondNormal, "OnChestReady"); Assert(Purchases(secondNormal) == 0, "Normal disable ignored");
                    var secondEmote = Shop(asm, true, false, true); Call(secondEmote, "OnChestReady"); Assert(Purchases(secondEmote) == 1, "Normal disable also disabled emote claim");
                });
                Test("saved settings override install defaults without rewriting them", delegate {
                    var asm = Patched(false); SetOption(asm, PatchEngine.NormalKey, false); SetOption(asm, PatchEngine.EmoteKey, true);
                    var normal = Shop(asm, true, false, false); var emote = Shop(asm, true, false, true);
                    Call(normal, "OnChestReady"); Call(emote, "OnChestReady");
                    Assert(Purchases(normal) == 0 && Purchases(emote) == 1, "Installed defaults overrode saved settings");
                    Assert(GetOption(asm, PatchEngine.NormalKey, -1) == 0 && GetOption(asm, PatchEngine.EmoteKey, -1) == 1, "Claim helper rewrote saved preferences");
                });
                Test("both toggles off retain manual readiness for both chest types", delegate {
                    var asm = Patched(true); SetOption(asm, PatchEngine.NormalKey, false); SetOption(asm, PatchEngine.EmoteKey, false);
                    foreach (var emote in new[] { false, true }) { var shop = Shop(asm, true, false, emote); Call(shop, "OnChestReady"); var timer = (IEnumerator)Call(shop, "TimerUpdate"); timer.MoveNext(); Assert(Purchases(shop) == 0, "Disabled chest claimed"); Assert((int)shop.GetType().GetField("Popups").GetValue(shop) == 1, "Manual readiness changed"); }
                });
                Test("enabling after an unaffordable ready chest keeps timer retries", delegate {
                    var asm = Patched(true); SetOption(asm, PatchEngine.NormalKey, false); Allow(asm, false);
                    var shop = Shop(asm, true, false, false); Call(shop, "OnChestReady"); var timer = (IEnumerator)Call(shop, "TimerUpdate"); timer.MoveNext();
                    SetOption(asm, PatchEngine.NormalKey, true); timer.MoveNext(); Assert(Purchases(shop) == 0, "Live enable bypassed affordability");
                    Allow(asm, true); timer.MoveNext(); timer.MoveNext(); Assert(Purchases(shop) == 1, "Ready chest lost retries after settings change");
                });
                Test("native General rows initialise both defaults and avoid duplicate rows", delegate {
                    var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                    Assert((int)Call(settings, "GeneralRowCount") == 7, "Expected Autostart, three toggles and three action rows");
                    Assert(((string)Call(settings, "RowLabel", 1)).IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0, "Normal row label missing");
                    Assert(((string)Call(settings, "RowLabel", 2)).IndexOf("emote", StringComparison.OrdinalIgnoreCase) >= 0, "Emote row label missing");
                    Assert((bool)Call(settings, "RowToggleActive", 1) && (bool)Call(settings, "RowToggleActive", 2), "Defaults not active when settings first opens");
                    Assert(!String.IsNullOrWhiteSpace((string)Call(settings, "RowTooltip", 1)) && !String.IsNullOrWhiteSpace((string)Call(settings, "RowTooltip", 2)), "Control explanations missing");
                    Call(settings, "Initialize"); Assert((int)Call(settings, "GeneralRowCount") == 7, "Repeated initialization duplicated controls");
                });
                Test("native settings use saved values and clicks change each claim mode live", delegate {
                    var asm = Patched(true); SetOption(asm, PatchEngine.NormalKey, false); SetOption(asm, PatchEngine.EmoteKey, true);
                    var settings = Settings(asm); Call(settings, "Initialize");
                    Assert(!(bool)Call(settings, "RowToggleActive", 1) && (bool)Call(settings, "RowToggleActive", 2), "Native controls ignored saved settings");
                    Call(settings, "ClickRow", 1); Call(settings, "ClickRow", 2);
                    Assert(GetOption(asm, PatchEngine.NormalKey, -1) == 1 && GetOption(asm, PatchEngine.EmoteKey, -1) == 0, "Clicked controls saved wrong keys or values");
                    Assert((int)asm.GetType("UnityEngine.PlayerPrefs").GetField("Saves").GetValue(null) == 2, "Control clicks did not persist settings once each");
                    var normal = Shop(asm, true, false, false); var emote = Shop(asm, true, false, true); Call(normal, "OnChestReady"); Call(emote, "OnChestReady");
                    Assert(Purchases(normal) == 1 && Purchases(emote) == 0, "Clicks did not immediately change runtime behavior");
                });
                Test("cloned native rows do not alter the original setting or invoke its callbacks", delegate {
                    var asm = Patched(true); asm.GetType("UnityEngine.PlayerPrefs").GetMethod("SetInt").Invoke(null, new object[] { "show_chest_popup", 1 });
                    var settings = Settings(asm); Call(settings, "Initialize"); Call(settings, "ClickRow", 1); Call(settings, "ClickRow", 2);
                    Assert(GetOption(asm, "show_chest_popup", -1) == 1, "Auto-claim controls wrote an original game preference");
                    Assert((int)settings.GetType().GetField("SourceEvents").GetValue(settings) == 0 && (int)settings.GetType().GetField("SourceButtonClicks").GetValue(settings) == 0, "Cloned row invoked source callbacks");
                    Assert((string)Call(settings, "OriginalLabel") == "Chest popup" && (bool)Call(settings, "OriginalLocalizersEnabled"), "Source row content or localization was changed");
                    Assert(!(bool)Call(settings, "RowLocalizersEnabled", 1) && !(bool)Call(settings, "RowLocalizersEnabled", 2), "New label could be overwritten by source localization");
                    var manager = settings.GetType().GetField("Manager").GetValue(settings); Assert((int)manager.GetType().GetField("OriginalAwakeCalls").GetValue(manager) == 1, "Original SettingsManager startup was changed");
                });
                Test("native clone failure clears its temporary Awake guard and permits retry", delegate {
                    var asm = Patched(true); var settings = Settings(asm);
                    asm.GetType("UnityEngine.Object").GetField("FailNextInstantiate").SetValue(null, true);
                    bool rejected = false;
                    try { Call(settings, "Initialize"); }
                    catch (TargetInvocationException ex) { rejected = ex.InnerException != null && ex.InnerException.Message.IndexOf("fixture clone failure", StringComparison.Ordinal) >= 0; }
                    Assert(rejected, "Fixture cloning failure did not propagate");
                    var guard = asm.GetType("BongoCat.PlayerPrefsToggle").GetField("__BongoClaim_CloneInProgress_v1", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert(guard != null && !(bool)guard.GetValue(null), "Failed clone left all preference Awake methods disabled");
                    Call(settings, "Initialize"); Assert((int)Call(settings, "GeneralRowCount") == 7, "Native initialization could not retry after failed clone");
                });
                Test("focused-menu hotkeys independently toggle native rows and persist their states", delegate {
                    var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                    Call(settings, "Frame", true, false);
                    Assert(GetOption(asm, PatchEngine.NormalKey, -1) == 0 && !(bool)Call(settings, "RowToggleActive", 1), "F7 did not toggle normal claim and its checkmark");
                    Assert((bool)Call(settings, "RowToggleActive", 2) && GetOption(asm, PatchEngine.EmoteKey, -1) == -1, "F7 changed the emote setting");
                    Call(settings, "Frame", false, true);
                    Assert(GetOption(asm, PatchEngine.EmoteKey, -1) == 0 && !(bool)Call(settings, "RowToggleActive", 2), "F9 did not toggle emote claim and its checkmark");
                    Call(settings, "Frame", true, true);
                    Assert(GetOption(asm, PatchEngine.NormalKey, -1) == 1 && GetOption(asm, PatchEngine.EmoteKey, -1) == 1, "Both hotkeys did not restore both settings independently");
                    Assert((bool)Call(settings, "RowToggleActive", 1) && (bool)Call(settings, "RowToggleActive", 2), "Hotkey state and native row state disagree");
                    Assert((int)asm.GetType("UnityEngine.PlayerPrefs").GetField("Saves").GetValue(null) == 4, "Hotkeys did not persist exactly one update per toggle");
                    Assert((int)Call(settings, "OriginalUpdateCount") == 3, "Hotkeys changed original per-frame game behavior");
                });
                Test("hotkeys are ignored outside a focused open menu or while typing", delegate {
                    var asm = Patched(true); var settings = Settings(asm); Call(settings, "Initialize");
                    foreach (var context in new[] { new[] { false, true, false }, new[] { true, false, false }, new[] { true, true, true } })
                    {
                        Call(settings, "SetContext", context[0], context[1], context[2]); Call(settings, "Frame", true, true);
                        Assert((bool)Call(settings, "RowToggleActive", 1) && (bool)Call(settings, "RowToggleActive", 2), "Blocked hotkey context changed native controls");
                    }
                    Assert(GetOption(asm, PatchEngine.NormalKey, -1) == -1 && GetOption(asm, PatchEngine.EmoteKey, -1) == -1, "Blocked context saved preferences");
                    Assert((int)asm.GetType("UnityEngine.PlayerPrefs").GetField("Saves").GetValue(null) == 0, "Blocked hotkeys wrote settings");
                    Call(settings, "SetContext", true, true, false); Call(settings, "Frame", true, false);
                    Assert(GetOption(asm, PatchEngine.NormalKey, -1) == 0 && (bool)Call(settings, "RowToggleActive", 2), "Hotkeys did not resume when context became eligible");
                    Assert((int)Call(settings, "OriginalUpdateCount") == 4, "Blocked hotkeys suppressed the game's own Update");
                });
                Test("hotkeys tolerate missing menu and controls without altering defaults", delegate {
                    var asm = Patched(true); var settings = Settings(asm);
                    Call(settings, "Frame", true, true);
                    Assert(GetOption(asm, PatchEngine.NormalKey, -1) == -1 && GetOption(asm, PatchEngine.EmoteKey, -1) == -1, "Missing controls wrote settings");
                    Call(settings, "Initialize"); Call(settings, "ClearMenu"); Call(settings, "Frame", true, true);
                    Assert((bool)Call(settings, "RowToggleActive", 1) && (bool)Call(settings, "RowToggleActive", 2), "Missing menu changed settings");
                    Assert((int)Call(settings, "OriginalUpdateCount") == 2, "Missing UI context interrupted original game Update");
                });
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
                Test("failed install-state commit rolls the game DLL back byte-for-byte", delegate {
                    var root = NewGame(); var target = PatchEngine.GameAssembly(root); var original = PatchEngine.Hash(target);
                    Directory.CreateDirectory(Path.Combine(root, ".bongo-claim", "install.json"));
                    bool rejected = false;
                    try { PatchEngine.Install(root, true); }
                    catch (IOException) { rejected = true; }
                    catch (UnauthorizedAccessException) { rejected = true; }
                    Assert(rejected, "State commit unexpectedly succeeded");
                    Assert(PatchEngine.Hash(target) == original, "Failed state commit left a patched DLL installed");
                    var check = PatchEngine.Inspect(target); Assert(check.Supported && !check.Installed, "Rollback did not return compatible original");
                    Assert(PatchEngine.Hash(Path.Combine(root, ".bongo-claim", "backups", original + ".dll")) == original, "Rollback discarded or damaged original backup");
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
                Test("v1 upgrade uses verified original backup and restores it byte-for-byte", delegate {
                    var root = NewGame(); var target = PatchEngine.GameAssembly(root); var original = PatchEngine.Hash(target);
                    WriteLegacyPatch(target, false); var legacyHash = PatchEngine.Hash(target); SaveLegacyState(root, original, legacyHash, false);
                    Assert(PatchEngine.Inspect(target).Installed, "Legacy patch was not recognised"); Assert(!PatchEngine.Inspect(target).RuntimeControls, "Legacy patch falsely reported live controls");
                    PatchEngine.Install(root, true); var upgraded = PatchEngine.Inspect(target);
                    Assert(upgraded.Installed && upgraded.RuntimeControls && upgraded.Emotes, "Upgrade did not add both live controls");
                    Assert(PatchEngine.LoadState(root).OriginalHash == original, "Upgrade replaced original backup record with patched bytes");
                    Assert(PatchEngine.Hash(Path.Combine(root, ".bongo-claim", "backups", original + ".dll")) == original, "Upgrade changed original backup");
                    Assert(PatchEngine.Install(root, true).StartsWith("Already"), "Upgraded install is not idempotent");
                    PatchEngine.Restore(root); Assert(PatchEngine.Hash(target) == original, "Upgrade restore is not byte-for-byte");
                });
                Test("v1 upgrade rejects missing or damaged original backup without changing game", delegate {
                    var root = NewGame(); var target = PatchEngine.GameAssembly(root); var original = PatchEngine.Hash(target);
                    WriteLegacyPatch(target, false); var legacyHash = PatchEngine.Hash(target); SaveLegacyState(root, original, legacyHash, false);
                    var backup = Path.Combine(root, ".bongo-claim", "backups", original + ".dll"); File.AppendAllText(backup, "corruption");
                    Throws(delegate { PatchEngine.Install(root, true); }, "damaged"); Assert(PatchEngine.Hash(target) == legacyHash, "Rejected upgrade modified game");
                    File.Delete(backup); Throws(delegate { PatchEngine.Install(root, true); }, "missing"); Assert(PatchEngine.Hash(target) == legacyHash, "Missing backup upgrade modified game");
                });
                Test("unknown shape is rejected before install changes anything", delegate {
                    var root = NewGame(); var target = PatchEngine.GameAssembly(root);
                    using (var asm = AssemblyDefinition.ReadAssembly(fixture)) { asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Shop").Methods.Single(m => m.Name == "OnChestReady").Name = "RenamedByUpdate"; asm.Write(target); }
                    var hash = PatchEngine.Hash(target); Throws(delegate { PatchEngine.Install(root, false); }, "Unsupported"); Assert(PatchEngine.Hash(target) == hash, "Unknown build modified");
                });
                Test("unfamiliar native settings layout is rejected before changing the game", delegate {
                    var root = NewGame(); var target = PatchEngine.GameAssembly(root);
                    using (var asm = AssemblyDefinition.ReadAssembly(fixture)) { asm.MainModule.Types.Single(t => t.FullName == "BongoCat.PlayerPrefsToggle").Fields.Single(f => f.Name == "_toggle").Name = "UpdatedToggleReference"; asm.Write(target); }
                    var hash = PatchEngine.Hash(target); Assert(!PatchEngine.Inspect(target).Supported, "Unknown settings layout marked compatible");
                    Throws(delegate { PatchEngine.Install(root, true); }, "Unsupported game settings"); Assert(PatchEngine.Hash(target) == hash, "Unknown settings layout modified game");
                });
                Test("rejects another auto-claim mod rather than stacking patches", delegate {
                    var input = NewFile("othermod");
                    using (var asm = AssemblyDefinition.ReadAssembly(fixture)) {
                        var shop = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.Shop"); var method = shop.Methods.Single(m => m.Name == "OnChestReady"); var buy = asm.MainModule.Types.Single(t => t.FullName == "BongoCat.ShopItem").Methods.Single(m => m.Name == "Buy");
                        method.Body.GetILProcessor().InsertBefore(method.Body.Instructions.Last(), Mono.Cecil.Cil.Instruction.Create(Mono.Cecil.Cil.OpCodes.Callvirt, buy)); asm.Write(input);
                    }
                    Assert(!PatchEngine.Inspect(input).Supported, "Other mod accepted");
                });
                FeatureTests();
                StartupTests();
                Console.WriteLine("All " + passed + " tests passed."); return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL " + ex.GetType().FullName + ": " + ex.Message);
                while (ex.InnerException != null) { ex = ex.InnerException; Console.Error.WriteLine("CAUSE " + ex.GetType().FullName + ": " + ex.Message); }
                return 1;
            }
            finally { Directory.Delete(temp, true); }
        }
    }
}
