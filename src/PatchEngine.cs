using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BongoClaim
{
    public sealed class Inspection
    {
        public string Path, Hash, Message;
        public bool Supported, Installed, Emotes, RuntimeControls, OwnedTokenControls;
    }

    [DataContract]
    public sealed class InstallState
    {
        [DataMember] public int Version = 3;
        [DataMember] public string OriginalHash;
        [DataMember] public string PatchedHash;
        [DataMember] public bool Emotes;
        [DataMember] public string InstalledUtc;
    }

    public static class PatchEngine
    {
        public const string Marker = "__BongoClaim_TryClaim_v3";
        public const string V2Marker = "__BongoClaim_TryClaim_v2";
        public const string LegacyMarker = "__BongoClaim_TryClaim_v1";
        public const string SettingsMarker = "__BongoClaim_Emotes_v3";
        public const string V2SettingsMarker = "__BongoClaim_Emotes_v2";
        public const string NormalKey = "BongoClaim_NormalEnabled_v1";
        public const string EmoteKey = "BongoClaim_EmoteEnabled_v1";
        public const string KeepPetsKey = "BongoClaim_KeepPets_v1";
        public const string ChargeMarker = "__BongoClaim_ShouldChargePets_v1";
        private const string RelativeDll = "BongoCat_Data/Managed/Assembly-CSharp.dll";

        public static string GameAssembly(string root)
        {
            if (String.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Select your BongoCat game folder first.");
            return System.IO.Path.Combine(System.IO.Path.GetFullPath(root), RelativeDll.Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        public static string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
        }

        private static AssemblyDefinition Read(string path, string dependencyPath = null)
        {
            var resolver = new DefaultAssemblyResolver();
            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            resolver.AddSearchDirectory(directory);
            // Backups retain their original bytes, while Unity dependencies stay in Managed.
            if (System.IO.Path.GetFileName(directory) == "backups" && System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(directory)) == ".bongo-claim")
                resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(GameAssembly(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(directory)))));
            if (dependencyPath != null)
            {
                var dependencyDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(dependencyPath));
                resolver.AddSearchDirectory(dependencyDirectory);
                if (System.IO.Path.GetFileName(dependencyDirectory) == "backups" && System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(dependencyDirectory)) == ".bongo-claim")
                    resolver.AddSearchDirectory(System.IO.Path.GetDirectoryName(GameAssembly(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(dependencyDirectory)))));
            }
            return AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = false, InMemory = true, AssemblyResolver = resolver });
        }

        private sealed class Shape
        {
            public TypeDefinition Shop, Item;
            public MethodDefinition Ready, Tick, Buy, CanBuy, Helper;
            public FieldDefinition Owner, ReadyFlag, Opening, Emote, ItemField;
        }

        private static MethodDefinition Method(TypeDefinition type, string name, string returns)
        {
            var methods = type.Methods.Where(m => m.Name == name && !m.IsStatic && m.Parameters.Count == 0 && m.ReturnType.FullName == returns && m.HasBody).ToArray();
            if (methods.Length != 1) throw new InvalidOperationException("Unsupported game build: expected one " + type.FullName + "." + name + "(). Nothing was changed.");
            return methods[0];
        }

        private static FieldDefinition Field(TypeDefinition type, string name, string fieldType)
        {
            var fields = type.Fields.Where(f => f.Name == name && f.FieldType.FullName == fieldType && !f.IsStatic).ToArray();
            if (fields.Length != 1) throw new InvalidOperationException("Unsupported game build: field " + type.FullName + "." + name + " changed. Nothing was changed.");
            return fields[0];
        }

        private static bool Calls(MethodDefinition caller, string type, string name)
        {
            return caller.Body.Instructions.Any(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) && i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName == type && ((MethodReference)i.Operand).Name == name);
        }

        private static bool Reads(MethodDefinition method, FieldDefinition field)
        {
            return method.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference && ((FieldReference)i.Operand).FullName == field.FullName);
        }

        private static Shape GetShape(AssemblyDefinition asm)
        {
            if (asm.Name.Name != "Assembly-CSharp") throw new InvalidOperationException("Not the Bongo Cat game assembly.");
            if (asm.Name.HasPublicKey || asm.Modules.Count != 1) throw new InvalidOperationException("Unsupported signed or multi-module game assembly. Nothing was changed.");
            var shop = asm.MainModule.Types.SingleOrDefault(t => t.FullName == "BongoCat.Shop");
            var item = asm.MainModule.Types.SingleOrDefault(t => t.FullName == "BongoCat.ShopItem");
            if (shop == null || item == null) throw new InvalidOperationException("Unsupported game: BongoCat.Shop and ShopItem were not found.");
            var shape = new Shape { Shop = shop, Item = item };
            shape.Ready = Method(shop, "OnChestReady", "System.Void");
            shape.Buy = Method(item, "Buy", "System.Void");
            shape.CanBuy = Method(item, "CanBuy", "System.Boolean");
            shape.ReadyFlag = Field(shop, "ChestIsReady", "System.Boolean");
            shape.Opening = Field(shop, "_openingChest", "System.Boolean");
            shape.Emote = Field(shop, "_isEmoteShop", "System.Boolean");
            shape.ItemField = Field(shop, "_shopItem", "BongoCat.ShopItem");
            Field(item, "_waitingForServer", "System.Boolean");
            var startBuy = item.Methods.SingleOrDefault(m => m.Name == "StartBuy" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Boolean" && m.ReturnType.FullName == "System.Void" && m.HasBody);
            if (startBuy == null || !Calls(shape.Buy, item.FullName, "StartBuy") || !Reads(startBuy, Field(item, "_waitingForServer", "System.Boolean"))) throw new InvalidOperationException("Unsupported game: the guarded chest purchase path changed.");
            var loading = Method(shop, "SetLoadingVisuals", "System.Void");
            if (!loading.Body.Instructions.Any(i => i.OpCode == OpCodes.Stfld && i.Operand is FieldReference && ((FieldReference)i.Operand).FullName == shape.Opening.FullName)) throw new InvalidOperationException("Unsupported game: in-flight purchase protection changed.");
            var timer = Method(shop, "TimerUpdate", "System.Collections.IEnumerator");
            var candidates = shop.NestedTypes.Where(t => t.Name.StartsWith("<TimerUpdate>", StringComparison.Ordinal) && timer.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName == t.FullName)).ToArray();
            if (candidates.Length != 1) throw new InvalidOperationException("Unsupported game: could not identify the timer state machine.");
            shape.Tick = Method(candidates[0], "MoveNext", "System.Boolean");
            shape.Owner = Field(candidates[0], "<>4__this", shop.FullName);
            if (!Calls(shape.Tick, shop.FullName, "OnChestReady") || !Calls(shape.CanBuy, "BongoCat.Pets", "CanSpendPets")) throw new InvalidOperationException("Unsupported game: readiness or affordability checks changed.");
            if (new[] { shape.Ready, shape.Tick }.Any(m => m.Body.ExceptionHandlers.Count != 0)) throw new InvalidOperationException("Unsupported game: timer exception layout changed.");
            var helpers = shop.Methods.Where(m => m.Name == Marker || m.Name == V2Marker || m.Name == LegacyMarker).ToArray();
            if (helpers.Length > 1) throw new InvalidOperationException("Conflicting Bongo Cat Auto Chest versions were detected. Restore the original first.");
            shape.Helper = helpers.SingleOrDefault();
            if (shape.Helper != null && (shape.Helper.IsStatic || shape.Helper.Parameters.Count != 0 || shape.Helper.ReturnType.FullName != "System.Boolean" || !shape.Helper.HasBody)) throw new InvalidOperationException("An invalid Bongo Cat Auto Chest helper was detected. Restore the original before continuing.");
            if (shape.Helper == null && (Calls(shape.Ready, item.FullName, "Buy") || Calls(shape.Tick, item.FullName, "Buy"))) throw new InvalidOperationException("Another auto-claim mod is already present. Use Steam Verify integrity to restore an original game DLL first.");
            if (shape.Helper != null && (!Calls(shape.Ready, shop.FullName, shape.Helper.Name) || !Calls(shape.Tick, shop.FullName, shape.Helper.Name))) throw new InvalidOperationException("An incomplete Bongo Cat Auto Chest patch was detected. Restore through this tool or Steam before continuing.");
            NativeControlsPatch.Validate(asm);
            if (shape.Helper != null && (shape.Helper.Name == Marker || shape.Helper.Name == V2Marker) && !NativeControlsPatch.IsInstalled(asm)) throw new InvalidOperationException("The Bongo Cat Auto Chest settings controls are incomplete. Restore the original before reinstalling.");
            if (shape.Helper != null && (shape.Helper.Name == Marker || shape.Helper.Name == V2Marker))
            {
                var option = shop.Fields.SingleOrDefault(f => f.Name == (shape.Helper.Name == Marker ? SettingsMarker : V2SettingsMarker));
                if (option == null || !option.IsLiteral || !option.HasConstant || option.FieldType.FullName != "System.Boolean") throw new InvalidOperationException("The Bongo Cat Auto Chest default setting is incomplete. Restore the original before reinstalling.");
            }
            OwnedTokensPatch.Validate(asm);
            if (shape.Helper != null && shape.Helper.Name == Marker)
            {
                var charge = item.Methods.SingleOrDefault(m => m.Name == ChargeMarker && m.IsStatic && m.Parameters.Count == 0 && m.ReturnType.FullName == "System.Boolean" && m.HasBody);
                if (charge == null || !Calls(shape.Buy, item.FullName, ChargeMarker) || !Calls(shape.CanBuy, item.FullName, ChargeMarker) || !NativeControlsPatch.IsExtendedInstalled(asm) || !OwnedTokensPatch.IsInstalled(asm))
                throw new InvalidOperationException("The Bongo Cat Auto Chest token or pet controls are incomplete. Restore the original before reinstalling.");
            }
            else if (shape.Helper == null)
            {
                // The price flag is captured by the original asynchronous callback.
                // Only the player's Buy() entry point is changed; gifts keep their own path.
                var body = shape.Buy.Body.Instructions;
                if (body.Count != 4 || body[0].OpCode != OpCodes.Ldarg_0 || body[1].OpCode != OpCodes.Ldc_I4_1 || body[2].Operand as MethodReference == null || ((MethodReference)body[2].Operand).FullName != startBuy.FullName || body[3].OpCode != OpCodes.Ret || shape.CanBuy.Body.ExceptionHandlers.Count != 0)
                    throw new InvalidOperationException("Unsupported game: the chest price flag changed. Nothing was changed.");
            }
            return shape;
        }

        public static Inspection Inspect(string path)
        {
            return Inspect(path, null);
        }

        private static Inspection Inspect(string path, string dependencies)
        {
            var result = new Inspection { Path = path };
            if (!File.Exists(path)) { result.Message = "Game DLL not found. Select the folder containing BongoCat.exe."; return result; }
            result.Hash = Hash(path);
            try
            {
                using (var asm = Read(path, dependencies))
                {
                    var shape = GetShape(asm);
                    result.Installed = shape.Helper != null;
                    result.RuntimeControls = result.Installed && (shape.Helper.Name == Marker || shape.Helper.Name == V2Marker);
                    result.OwnedTokenControls = result.Installed && shape.Helper.Name == Marker;
                    var setting = shape.Shop.Fields.SingleOrDefault(f => f.Name == (result.OwnedTokenControls ? SettingsMarker : result.RuntimeControls ? V2SettingsMarker : "__BongoClaim_Emotes_v1"));
                    result.Emotes = setting != null && setting.HasConstant && Convert.ToBoolean(setting.Constant);
                    result.Supported = true;
                    result.Message = result.OwnedTokenControls ? "Installed. Use Settings > General for auto claim, stored tokens and pet controls." : result.Installed ? "Older patch installed. Upgrade to add stored-token and pet controls." : "Compatible chest, inventory and settings controls found. Ready to install.";
                }
            }
            catch (Exception ex) { result.Message = ex.Message; }
            return result;
        }

        private static void Prepend(MethodDefinition method, IEnumerable<Instruction> instructions)
        {
            var il = method.Body.GetILProcessor();
            var first = method.Body.Instructions[0];
            foreach (var instruction in instructions) il.InsertBefore(first, instruction);
            method.Body.MaxStackSize = Math.Max(method.Body.MaxStackSize, 4);
        }

        // Prefixes add constant offsets without changing the distances between existing branches.
        private static void AddPatch(AssemblyDefinition asm, bool emotes)
        {
            var s = GetShape(asm);
            if (s.Helper != null) throw new InvalidOperationException("This DLL is already patched. Restore the original before changing chest options.");
            var helper = new MethodDefinition(Marker, MethodAttributes.Private | MethodAttributes.HideBySig, asm.MainModule.TypeSystem.Boolean);
            s.Shop.Methods.Add(helper);
            s.Shop.Fields.Add(new FieldDefinition(SettingsMarker, FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, asm.MainModule.TypeSystem.Boolean) { Constant = emotes });
            var il = helper.Body.GetILProcessor();
            var no = Instruction.Create(OpCodes.Ldc_I4_0);
            var normal = Instruction.Create(OpCodes.Ldstr, NormalKey);
            var enabled = Instruction.Create(OpCodes.Brfalse, no);
            var getInt = NativeControlsPatch.GetIntReference(asm);
            il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, s.Emote)); il.Append(Instruction.Create(OpCodes.Brfalse, normal));
            il.Append(Instruction.Create(OpCodes.Ldstr, EmoteKey)); il.Append(Instruction.Create(emotes ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0)); il.Append(Instruction.Create(OpCodes.Call, getInt)); il.Append(Instruction.Create(OpCodes.Br, enabled));
            il.Append(normal); il.Append(Instruction.Create(OpCodes.Ldc_I4_1)); il.Append(Instruction.Create(OpCodes.Call, getInt)); il.Append(enabled);
            il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, s.ReadyFlag)); il.Append(Instruction.Create(OpCodes.Brfalse, no));
            il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, s.Opening)); il.Append(Instruction.Create(OpCodes.Brtrue, no));
            il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, s.ItemField)); il.Append(Instruction.Create(OpCodes.Brfalse, no));
            il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, s.ItemField)); il.Append(Instruction.Create(OpCodes.Callvirt, s.CanBuy)); il.Append(Instruction.Create(OpCodes.Brfalse, no));
            il.Append(Instruction.Create(OpCodes.Ldarg_0)); il.Append(Instruction.Create(OpCodes.Ldfld, s.ItemField)); il.Append(Instruction.Create(OpCodes.Callvirt, s.Buy));
            il.Append(Instruction.Create(OpCodes.Ldc_I4_1)); il.Append(Instruction.Create(OpCodes.Ret)); il.Append(no); il.Append(Instruction.Create(OpCodes.Ret));
            helper.Body.MaxStackSize = 2;
            var original = s.Ready.Body.Instructions[0];
            Prepend(s.Ready, new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Call, helper), Instruction.Create(OpCodes.Brfalse, original), Instruction.Create(OpCodes.Ret) });
            // A retry is possible even when a ready-chest popup is already visible.
            // The game owns its one-second timer, purchase lock and failure cooldown.
            var tickStart = s.Tick.Body.Instructions[0];
            var ownerExists = Instruction.Create(OpCodes.Call, helper);
            Prepend(s.Tick, new[] { Instruction.Create(OpCodes.Ldarg_0), Instruction.Create(OpCodes.Ldfld, s.Owner), Instruction.Create(OpCodes.Dup), Instruction.Create(OpCodes.Brtrue, ownerExists), Instruction.Create(OpCodes.Pop), Instruction.Create(OpCodes.Br, tickStart), ownerExists, Instruction.Create(OpCodes.Pop) });
            NativeControlsPatch.Apply(asm, emotes);
            AddPetControl(asm, s);
            OwnedTokensPatch.Apply(asm);
            asm.MainModule.Mvid = Guid.NewGuid();
        }

        private static void AddPetControl(AssemblyDefinition asm, Shape s)
        {
            if (s.Item.Methods.Any(m => m.Name == ChargeMarker)) throw new InvalidOperationException("A conflicting pet control is already present.");
            var charge = new MethodDefinition(ChargeMarker, MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig, asm.MainModule.TypeSystem.Boolean);
            s.Item.Methods.Add(charge);
            var il = charge.Body.GetILProcessor();
            il.Append(Instruction.Create(OpCodes.Ldstr, KeepPetsKey));
            il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
            il.Append(Instruction.Create(OpCodes.Call, NativeControlsPatch.GetIntReference(asm)));
            il.Append(Instruction.Create(OpCodes.Ldc_I4_0));
            il.Append(Instruction.Create(OpCodes.Ceq));
            il.Append(Instruction.Create(OpCodes.Ret));
            charge.Body.MaxStackSize = 2;
            s.Buy.Body.Instructions[1].OpCode = OpCodes.Call;
            s.Buy.Body.Instructions[1].Operand = charge;
            var original = s.CanBuy.Body.Instructions[0];
            Prepend(s.CanBuy, new[] { Instruction.Create(OpCodes.Call, charge), Instruction.Create(OpCodes.Brtrue, original), Instruction.Create(OpCodes.Ldc_I4_1), Instruction.Create(OpCodes.Ret) });
        }

        public static void PatchCopy(string source, string output, bool emotes)
        {
            source = System.IO.Path.GetFullPath(source);
            output = System.IO.Path.GetFullPath(output);
            if (String.Equals(System.IO.Path.GetFullPath(source), System.IO.Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Patch-copy needs a separate output file. Use Install for the live game.");
            if (File.Exists(output)) throw new InvalidOperationException("The output file already exists. Choose a new output name.");
            using (var asm = Read(source))
            {
                AddPatch(asm, emotes);
                try { asm.Write(output, new WriterParameters { WriteSymbols = false }); }
                catch (FileNotFoundException ex)
                {
                    if (Directory.Exists(System.IO.Path.GetDirectoryName(output))) throw new IOException("Windows blocked writing the output file. The selected folder may be protected by Controlled Folder Access. Choose an ordinary writable output folder; do not disable Windows security.", ex);
                    throw;
                }
            }
            var check = Inspect(output, source);
            if (!check.Supported || !check.Installed || !check.OwnedTokenControls || check.Emotes != emotes) throw new InvalidOperationException("Patched output did not pass read-back verification: " + check.Message);
        }

        public static void EnsureGameClosed(string root)
        {
            foreach (var process in Process.GetProcessesByName("BongoCat"))
            {
                using (process)
                {
                    try
                    {
                        var executable = process.MainModule.FileName;
                        if (!String.Equals(System.IO.Path.GetDirectoryName(executable), System.IO.Path.GetFullPath(root).TrimEnd(System.IO.Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) continue;
                    }
                    catch { }
                    throw new InvalidOperationException("Bongo Cat is running. Exit the game normally, then try again. This tool will not force-close it.");
                }
            }
        }

        public static InstallState LoadState(string root)
        {
            var file = System.IO.Path.Combine(root, ".bongo-claim", "install.json");
            if (!File.Exists(file)) return null;
            using (var stream = File.OpenRead(file))
                return (InstallState)new DataContractJsonSerializer(typeof(InstallState)).ReadObject(stream);
        }

        private static string BackupPath(string root, string hash)
        {
            if (hash == null || hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c))) throw new InvalidOperationException("Invalid backup record. Use Steam Verify integrity.");
            return System.IO.Path.Combine(root, ".bongo-claim", "backups", hash.ToLowerInvariant() + ".dll");
        }

        private static void SaveState(string file, InstallState state)
        {
            using (var stream = File.Create(file)) new DataContractJsonSerializer(typeof(InstallState)).WriteObject(stream, state);
        }

        public static string Install(string root, bool emotes)
        {
            root = System.IO.Path.GetFullPath(root);
            EnsureGameClosed(root);
            var target = GameAssembly(root);
            var current = Inspect(target);
            if (!current.Supported) throw new InvalidOperationException(current.Message);
            var originalHash = current.Hash;
            string source = target;
            if (current.Installed)
            {
                var prior = LoadState(root);
                if (prior == null || !String.Equals(prior.PatchedHash, current.Hash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("A patched DLL has no matching backup record. Use Steam Verify integrity, then install again.");
                source = BackupPath(root, prior.OriginalHash);
                if (!File.Exists(source) || Hash(source) != prior.OriginalHash) throw new InvalidOperationException("The original backup is missing or damaged. Use Steam Verify integrity.");
                if (current.OwnedTokenControls) return "Already installed. Use Settings > General in Bongo Cat for auto claim, stored tokens and pet controls.";
                // Upgrade from the verified unmodified backup, never stack patches.
                originalHash = prior.OriginalHash;
            }
            if (!File.Exists(System.IO.Path.Combine(root, "BongoCat.exe"))) throw new InvalidOperationException("Select the folder containing BongoCat.exe.");
            var backup = BackupPath(root, originalHash);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backup));
            if (File.Exists(backup))
            {
                if (Hash(backup) != originalHash) throw new InvalidOperationException("A backup with this name is damaged. Nothing was changed.");
            }
            else File.Copy(target, backup, false);
            var pending = target + ".bongoclaim-" + Guid.NewGuid().ToString("N") + ".tmp";
            var statePath = System.IO.Path.Combine(root, ".bongo-claim", "install.json");
            var stateTemp = statePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var rollback = target + ".rollback-" + Guid.NewGuid().ToString("N") + ".tmp";
            bool replaced = false;
            bool committedOrRecovered = false;
            try
            {
                PatchCopy(source, pending, emotes);
                var patchedHash = Hash(pending);
                var state = new InstallState { OriginalHash = originalHash, PatchedHash = patchedHash, Emotes = emotes, InstalledUtc = DateTime.UtcNow.ToString("o") };
                SaveState(stateTemp, state);
                EnsureGameClosed(root);
                if (Hash(target) != current.Hash) throw new InvalidOperationException("The game updated during installation. Nothing was changed. Inspect and try again.");
                File.Replace(pending, target, rollback);
                replaced = true;
                if (File.Exists(statePath)) File.Replace(stateTemp, statePath, null); else File.Move(stateTemp, statePath);
                committedOrRecovered = true;
                return "Chest and pet controls installed. Start Bongo Cat from Steam, then open Settings > General.";
            }
            catch (Exception failure)
            {
                if (replaced)
                {
                    try { File.Replace(rollback, target, null); committedOrRecovered = true; }
                    catch (Exception recovery) { throw new IOException("Installation could not commit or restore the previous DLL. That DLL is retained at " + rollback + ". Close the game and recover it before continuing.", new AggregateException(failure, recovery)); }
                }
                throw;
            }
            finally
            {
                if (File.Exists(pending)) File.Delete(pending);
                if (File.Exists(stateTemp)) File.Delete(stateTemp);
                if (committedOrRecovered && File.Exists(rollback)) File.Delete(rollback);
            }
        }

        public static string Restore(string root)
        {
            root = System.IO.Path.GetFullPath(root);
            EnsureGameClosed(root);
            var target = GameAssembly(root);
            var state = LoadState(root);
            if (state == null) throw new InvalidOperationException("No install record found. If you used another mod, use Steam Verify integrity.");
            var currentHash = Hash(target);
            if (currentHash == state.OriginalHash) return "The original is already restored. The backup is retained.";
            if (currentHash != state.PatchedHash) throw new InvalidOperationException("The game DLL changed after installation, likely from a Steam update. Refusing to overwrite it with an old backup. Use Steam Verify integrity if needed.");
            var backup = BackupPath(root, state.OriginalHash);
            if (!File.Exists(backup) || Hash(backup) != state.OriginalHash) throw new InvalidOperationException("The original backup is missing or damaged. Use Steam Verify integrity.");
            var pending = target + ".restore-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(backup, pending, false);
                EnsureGameClosed(root);
                if (Hash(target) != currentHash) throw new InvalidOperationException("The game changed during restore. Nothing was changed.");
                File.Replace(pending, target, null);
                return "Original game DLL restored byte-for-byte. Auto claim is disabled. Your backup is retained.";
            }
            finally { if (File.Exists(pending)) File.Delete(pending); }
        }
    }
}
