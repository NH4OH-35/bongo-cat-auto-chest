using System;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BongoClaim
{
    // Builds controls from the game's existing themed settings row. No Unity or
    // game binaries are linked into, or redistributed with, the patcher.
    internal static class NativeControlsPatch
    {
        private const string ConfigureName = "__BongoClaim_Configure_v1";
        private const string CreateName = "__BongoClaim_CreateRow_v1";
        private const string ControlsName = "__BongoClaim_CreateControls_v1";
        private const string RowName = "__BongoClaim_GetRow_v1";
        private const string CloneGuardName = "__BongoClaim_CloneInProgress_v1";
        private const string NormalControlName = "__BongoClaim_NormalControl_v1";
        private const string EmoteControlName = "__BongoClaim_EmoteControl_v1";
        private const string ToggleKeysName = "__BongoClaim_ToggleKeys_v1";
        private const string ControlKeysName = "__BongoClaim_ControlKeys_v1";

        private sealed class Shape
        {
            public ModuleDefinition Module;
            public TypeDefinition Settings, Toggle, AutoStart, Checkmark, MainCat;
            public MethodDefinition SettingsAwake, ToggleAwake, SetUI, ToggleClick, MainUpdate, InputFocused;
            public FieldDefinition Source, Key, Check, Image, Invert, Default, Value, Event, AutoCheck, CheckEvent, Menu;
        }

        private static InvalidOperationException Unsupported(string detail)
        {
            return new InvalidOperationException("Unsupported game settings: " + detail + ". Nothing was changed.");
        }

        private static TypeDefinition Own(ModuleDefinition module, string fullName)
        {
            var found = module.Types.SingleOrDefault(t => t.FullName == fullName);
            if (found == null) throw Unsupported(fullName + " is missing");
            return found;
        }

        private static FieldDefinition Field(TypeDefinition type, string name, string fullName)
        {
            var found = type.Fields.SingleOrDefault(f => f.Name == name && !f.IsStatic && f.FieldType.FullName == fullName);
            if (found == null) throw Unsupported(type.FullName + "." + name + " changed");
            return found;
        }

        private static MethodDefinition OwnMethod(TypeDefinition type, string name, string returns)
        {
            var found = type.Methods.SingleOrDefault(m => m.Name == name && !m.IsStatic && m.Parameters.Count == 0 && m.ReturnType.FullName == returns && m.HasBody);
            if (found == null) throw Unsupported(type.FullName + "." + name + "() changed");
            return found;
        }

        private static Shape GetShape(AssemblyDefinition asm)
        {
            var m = asm.MainModule;
            var s = new Shape { Module = m, Settings = Own(m, "BongoCat.SettingsManager"), Toggle = Own(m, "BongoCat.PlayerPrefsToggle"), AutoStart = Own(m, "BongoCat.SetAutoStart"), Checkmark = Own(m, "ToggleGameObject") };
            s.SettingsAwake = OwnMethod(s.Settings, "Awake", "System.Void");
            s.ToggleAwake = OwnMethod(s.Toggle, "Awake", "System.Void");
            s.SetUI = OwnMethod(s.Toggle, "SetUI", "System.Void");
            s.ToggleClick = OwnMethod(s.Toggle, "Toggle", "System.Void");
            s.Source = Field(s.Settings, "ShowChestPopup", "BongoCat.PlayerPrefsToggle");
            s.Key = Field(s.Toggle, "_playerPrefsKey", "System.String");
            s.Check = Field(s.Toggle, "_toggle", "ToggleGameObject");
            s.Image = Field(s.Toggle, "_imageColor", "BongoCat.UiThemes.ToggleImageColor");
            s.Invert = Field(s.Toggle, "_invert", "System.Boolean");
            s.Default = Field(s.Toggle, "_defaultValue", "System.Boolean");
            s.Value = Field(s.Toggle, "_value", "System.Boolean");
            s.Event = Field(s.Toggle, "OnToggleUpdated", "UnityEngine.Events.UnityEvent`1<System.Boolean>");
            s.AutoCheck = Field(s.AutoStart, "_toggle", "ToggleGameObject");
            s.CheckEvent = Field(s.Checkmark, "OnToggle", "UnityEngine.Events.UnityEvent`1<System.Boolean>");
            s.MainCat = Own(m, "BongoCat.MainCat");
            s.MainUpdate = OwnMethod(s.MainCat, "Update", "System.Void");
            s.Menu = Field(s.MainCat, "_menu", "ToggleGameObject");
            s.InputFocused = Own(m, "Helper.InputFieldCache").Methods.SingleOrDefault(method => method.Name == "get_FocusingInputField" && method.IsStatic && method.Parameters.Count == 0 && method.ReturnType.FullName == "System.Boolean" && method.HasBody);
            if (s.InputFocused == null) throw Unsupported("input-field focus detection changed");
            if (s.SettingsAwake.Body.ExceptionHandlers.Count != 0 || s.ToggleAwake.Body.ExceptionHandlers.Count != 0)
                throw Unsupported("initialization handlers changed");
            if (!s.ToggleAwake.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).FullName == "System.Int32 UnityEngine.PlayerPrefs::GetInt(System.String,System.Int32)"))
                throw Unsupported("preference initialization changed");
            // The original row has a checkmark, a toggle container, and a content
            // container below its Button root. These APIs are checked against the
            // game's own referenced Unity modules before emitting calls.
            return s;
        }

        public static MethodReference GetIntReference(AssemblyDefinition asm)
        {
            var reference = asm.MainModule.Types.SelectMany(t => t.Methods).Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
                .Select(i => i.Operand as MethodReference).FirstOrDefault(m => m != null && m.FullName == "System.Int32 UnityEngine.PlayerPrefs::GetInt(System.String,System.Int32)");
            if (reference == null) throw Unsupported("PlayerPrefs.GetInt(string,int) is missing");
            return reference;
        }

        private static MethodReference GetKeyReference(AssemblyDefinition asm)
        {
            var reference = asm.MainModule.GetTypes().SelectMany(t => t.Methods).Where(method => method.HasBody).SelectMany(method => method.Body.Instructions)
                .Select(i => i.Operand as MethodReference).FirstOrDefault(method => method != null && method.FullName == "System.Boolean UnityEngine.Input::GetKeyDown(UnityEngine.KeyCode)");
            if (reference != null) return reference;
            // The behavioral fixture owns its Unity-shaped API definitions.
            var own = asm.MainModule.GetTypes().SingleOrDefault(t => t.FullName == "UnityEngine.Input");
            if (own != null)
            {
                var method = own.Methods.SingleOrDefault(candidate => candidate.Name == "GetKeyDown" && candidate.IsStatic && candidate.Parameters.Count == 1 && candidate.Parameters[0].ParameterType.FullName == "UnityEngine.KeyCode" && candidate.ReturnType.FullName == "System.Boolean");
                if (method != null) return method;
            }
            throw Unsupported("Input.GetKeyDown(KeyCode) is missing");
        }

        private static TypeReference Type(ModuleDefinition m, string name, string assemblyName)
        {
            var own = m.GetTypes().SingleOrDefault(t => t.FullName == name);
            if (own != null) return own;
            var reference = m.GetTypeReferences().FirstOrDefault(t => t.FullName == name);
            if (reference != null) return reference;
            var scope = m.AssemblyReferences.FirstOrDefault(a => a.Name == assemblyName);
            if (scope == null) throw Unsupported(assemblyName + " reference is missing");
            int slash = name.LastIndexOf('/');
            if (slash >= 0)
            {
                var parent = Type(m, name.Substring(0, slash), assemblyName);
                return new TypeReference("", name.Substring(slash + 1), m, scope) { DeclaringType = parent };
            }
            int dot = name.LastIndexOf('.');
            return new TypeReference(dot < 0 ? "" : name.Substring(0, dot), dot < 0 ? name : name.Substring(dot + 1), m, scope);
        }

        private static MethodReference Call(TypeReference owner, string name, TypeReference result, bool instance, params TypeReference[] args)
        {
            var method = new MethodReference(name, result, owner) { HasThis = instance };
            foreach (var arg in args) method.Parameters.Add(new ParameterDefinition(arg));
            return method;
        }

        private static GenericInstanceMethod Generic(TypeReference owner, string name, TypeReference argument, bool instance, bool arrayResult, params TypeReference[] args)
        {
            var method = new MethodReference(name, owner.Module.TypeSystem.Void, owner) { HasThis = instance };
            var gp = new GenericParameter("T", method);
            method.GenericParameters.Add(gp);
            method.ReturnType = arrayResult ? (TypeReference)new ArrayType(gp) : gp;
            foreach (var arg in args) method.Parameters.Add(new ParameterDefinition(arg));
            var generic = new GenericInstanceMethod(method);
            generic.GenericArguments.Add(argument);
            return generic;
        }

        private static GenericInstanceMethod Instantiate(ModuleDefinition m, TypeReference type, TypeReference obj, TypeReference transform)
        {
            var method = new MethodReference("Instantiate", m.TypeSystem.Void, obj) { HasThis = false };
            var gp = new GenericParameter("T", method);
            method.GenericParameters.Add(gp);
            method.ReturnType = gp;
            method.Parameters.Add(new ParameterDefinition(gp));
            method.Parameters.Add(new ParameterDefinition(transform));
            var generic = new GenericInstanceMethod(method);
            generic.GenericArguments.Add(type);
            return generic;
        }

        private static MethodDefinition AddMethod(TypeDefinition owner, string name, bool isStatic, TypeReference result, params TypeReference[] args)
        {
            var method = new MethodDefinition(name, MethodAttributes.Private | MethodAttributes.HideBySig | (isStatic ? MethodAttributes.Static : 0), result);
            foreach (var arg in args) method.Parameters.Add(new ParameterDefinition(arg));
            owner.Methods.Add(method);
            method.Body.InitLocals = true;
            return method;
        }

        private static void EmitRow(ILProcessor il, MethodReference getTransform, MethodReference getParent, MethodReference getGameObject)
        {
            il.Emit(OpCodes.Call, getTransform);
            il.Emit(OpCodes.Callvirt, getParent);
            il.Emit(OpCodes.Callvirt, getParent);
            il.Emit(OpCodes.Callvirt, getParent);
            il.Emit(OpCodes.Callvirt, getGameObject);
        }

        public static void Validate(AssemblyDefinition asm)
        {
            var s = GetShape(asm);
            GetIntReference(asm);
            GetKeyReference(asm);
            Type(s.Module, "UnityEngine.GameObject", "UnityEngine.CoreModule");
            Type(s.Module, "UnityEngine.Transform", "UnityEngine.CoreModule");
            Type(s.Module, "UnityEngine.UI.Button", "UnityEngine.UI");
            Type(s.Module, "TMPro.TMP_Text", "Unity.TextMeshPro");
            Type(s.Module, "UnityEngine.Localization.Components.LocalizeStringEvent", "Unity.Localization");
        }

        public static bool IsInstalled(AssemblyDefinition asm)
        {
            try
            {
                var s = GetShape(asm);
                var create = s.Settings.Methods.SingleOrDefault(m => m.Name == ControlsName);
                var configure = s.Toggle.Methods.SingleOrDefault(m => m.Name == ConfigureName);
                var guard = s.Toggle.Fields.SingleOrDefault(f => f.Name == CloneGuardName && f.IsStatic && f.FieldType.FullName == "System.Boolean");
                var keys = s.MainCat.Methods.SingleOrDefault(method => method.Name == ControlKeysName && method.Parameters.Count == 0 && !method.IsStatic && method.ReturnType.FullName == "System.Void" && method.HasBody);
                return create != null && configure != null && guard != null && s.Toggle.Methods.Any(m => m.Name == CreateName) && s.AutoStart.Methods.Any(m => m.Name == RowName)
                    && s.SettingsAwake.Body.Instructions.Count(i => i.Operand is MethodReference && ((MethodReference)i.Operand).FullName == create.FullName) == 1
                    && s.ToggleAwake.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldsfld && i.Operand is FieldReference && ((FieldReference)i.Operand).FullName == guard.FullName)
                    && keys != null && s.MainUpdate.Body.Instructions.Count(i => i.Operand is MethodReference && ((MethodReference)i.Operand).FullName == keys.FullName) == 1
                    && s.Toggle.Methods.Any(method => method.Name == ToggleKeysName && method.IsStatic && method.Parameters.Count == 0 && method.ReturnType.FullName == "System.Void" && method.HasBody)
                    && s.Toggle.Fields.Any(field => field.Name == NormalControlName && field.IsStatic && field.FieldType.FullName == s.Toggle.FullName)
                    && s.Toggle.Fields.Any(field => field.Name == EmoteControlName && field.IsStatic && field.FieldType.FullName == s.Toggle.FullName);
            }
            catch (InvalidOperationException) { return false; }
        }

        public static void Apply(AssemblyDefinition asm, bool emotes)
        {
            Validate(asm);
            var s = GetShape(asm);
            if (s.Settings.Methods.Any(method => method.Name == ControlsName) || s.Toggle.Methods.Any(method => method.Name == ConfigureName))
                throw Unsupported("native controls are already present");
            var m = s.Module;
            var obj = Type(m, "UnityEngine.Object", "UnityEngine.CoreModule");
            var component = Type(m, "UnityEngine.Component", "UnityEngine.CoreModule");
            var behavior = Type(m, "UnityEngine.Behaviour", "UnityEngine.CoreModule");
            var go = Type(m, "UnityEngine.GameObject", "UnityEngine.CoreModule");
            var transform = Type(m, "UnityEngine.Transform", "UnityEngine.CoreModule");
            var button = Type(m, "UnityEngine.UI.Button", "UnityEngine.UI");
            var clicked = Type(m, "UnityEngine.UI.Button/ButtonClickedEvent", "UnityEngine.UI");
            var unityEvent = Type(m, "UnityEngine.Events.UnityEvent", "UnityEngine.CoreModule");
            var unityAction = Type(m, "UnityEngine.Events.UnityAction", "UnityEngine.CoreModule");
            var localize = Type(m, "UnityEngine.Localization.Components.LocalizeStringEvent", "Unity.Localization");
            var text = Type(m, "TMPro.TMP_Text", "Unity.TextMeshPro");
            var application = Type(m, "UnityEngine.Application", "UnityEngine.CoreModule");
            var getTransform = Call(component, "get_transform", transform, true);
            var getObjectTransform = Call(go, "get_transform", transform, true);
            var getGameObject = Call(component, "get_gameObject", go, true);
            var getParent = Call(transform, "get_parent", transform, true);
            var implicitBool = Call(obj, "op_Implicit", m.TypeSystem.Boolean, false, obj);
            var getInt = GetIntReference(asm);
            var getKey = GetKeyReference(asm);
            var getCheck = Generic(go, "GetComponentInChildren", s.Checkmark, true, false, m.TypeSystem.Boolean);
            var getButton = Generic(go, "GetComponent", button, true, false);
            var getText = Generic(component, "GetComponent", text, true, false);
            var getLocalizers = Generic(go, "GetComponentsInChildren", localize, true, true, m.TypeSystem.Boolean);
            var find = Call(transform, "Find", transform, true, m.TypeSystem.String);
            var setText = Call(text, "set_text", m.TypeSystem.Void, true, m.TypeSystem.String);
            var setActive = Call(go, "SetActive", m.TypeSystem.Void, true, m.TypeSystem.Boolean);
            var setEnabled = Call(behavior, "set_enabled", m.TypeSystem.Void, true, m.TypeSystem.Boolean);
            var newBoolEvent = Call(s.Event.FieldType, ".ctor", m.TypeSystem.Void, true);

            var guard = new FieldDefinition(CloneGuardName, FieldAttributes.Private | FieldAttributes.Static, m.TypeSystem.Boolean);
            s.Toggle.Fields.Add(guard);
            var normalControl = new FieldDefinition(NormalControlName, FieldAttributes.Private | FieldAttributes.Static, s.Toggle);
            var emoteControl = new FieldDefinition(EmoteControlName, FieldAttributes.Private | FieldAttributes.Static, s.Toggle);
            s.Toggle.Fields.Add(normalControl); s.Toggle.Fields.Add(emoteControl);
            var awakeIL = s.ToggleAwake.Body.GetILProcessor();
            var originalAwake = s.ToggleAwake.Body.Instructions[0];
            awakeIL.InsertBefore(originalAwake, Instruction.Create(OpCodes.Ldsfld, guard));
            awakeIL.InsertBefore(originalAwake, Instruction.Create(OpCodes.Brfalse, originalAwake));
            awakeIL.InsertBefore(originalAwake, Instruction.Create(OpCodes.Ret));

            var rowAccessor = AddMethod(s.AutoStart, RowName, false, go);
            rowAccessor.Attributes = MethodAttributes.Assembly | MethodAttributes.HideBySig;
            var il = rowAccessor.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, s.AutoCheck);
            EmitRow(il, getTransform, getParent, getGameObject);
            il.Emit(OpCodes.Ret);

            var configure = AddMethod(s.Toggle, ConfigureName, false, m.TypeSystem.Void, m.TypeSystem.String, m.TypeSystem.Boolean, m.TypeSystem.String, go);
            var localizers = new VariableDefinition(new ArrayType(localize));
            var index = new VariableDefinition(m.TypeSystem.Int32);
            var clickEvent = new VariableDefinition(clicked);
            configure.Body.Variables.Add(localizers); configure.Body.Variables.Add(index); configure.Body.Variables.Add(clickEvent);
            il = configure.Body.GetILProcessor();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, s.Key);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Stfld, s.Default);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stfld, s.Invert);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Stfld, s.Image);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Newobj, newBoolEvent); il.Emit(OpCodes.Stfld, s.Event);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg, configure.Parameters[3]); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Callvirt, getCheck); il.Emit(OpCodes.Stfld, s.Check);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, s.Check); il.Emit(OpCodes.Newobj, newBoolEvent); il.Emit(OpCodes.Stfld, s.CheckEvent);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Call, getInt); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ceq); il.Emit(OpCodes.Stfld, s.Value);
            il.Emit(OpCodes.Ldarg, configure.Parameters[3]); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Callvirt, getLocalizers); il.Emit(OpCodes.Stloc, localizers);
            il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stloc, index);
            var testLoop = Instruction.Create(OpCodes.Ldloc, index);
            var loop = Instruction.Create(OpCodes.Ldloc, localizers);
            il.Emit(OpCodes.Br, testLoop); il.Append(loop);
            il.Emit(OpCodes.Ldloc, index); il.Emit(OpCodes.Ldelem_Ref); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Callvirt, setEnabled);
            il.Emit(OpCodes.Ldloc, index); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc, index);
            il.Append(testLoop); il.Emit(OpCodes.Ldloc, localizers); il.Emit(OpCodes.Ldlen); il.Emit(OpCodes.Conv_I4); il.Emit(OpCodes.Blt, loop);
            il.Emit(OpCodes.Ldarg, configure.Parameters[3]); il.Emit(OpCodes.Callvirt, getObjectTransform); il.Emit(OpCodes.Ldstr, "Content/Label"); il.Emit(OpCodes.Callvirt, find); il.Emit(OpCodes.Callvirt, getText); il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Callvirt, setText);
            il.Emit(OpCodes.Ldarg, configure.Parameters[3]); il.Emit(OpCodes.Callvirt, getObjectTransform); il.Emit(OpCodes.Ldstr, "HoverTooltip/Text"); il.Emit(OpCodes.Callvirt, find); il.Emit(OpCodes.Callvirt, getText); il.Emit(OpCodes.Ldstr, "Automatically claim when ready at the usual pets cost. While the menu is open and Bongo Cat has focus: F7 toggles normal chests; F9 toggles emote chests."); il.Emit(OpCodes.Callvirt, setText);
            il.Emit(OpCodes.Ldarg, configure.Parameters[3]); il.Emit(OpCodes.Callvirt, getObjectTransform); il.Emit(OpCodes.Ldstr, "DisableOverlay"); il.Emit(OpCodes.Callvirt, find); il.Emit(OpCodes.Callvirt, getGameObject); il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Callvirt, setActive);
            il.Emit(OpCodes.Newobj, Call(clicked, ".ctor", m.TypeSystem.Void, true)); il.Emit(OpCodes.Stloc, clickEvent);
            il.Emit(OpCodes.Ldloc, clickEvent); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldftn, s.ToggleClick); il.Emit(OpCodes.Newobj, Call(unityAction, ".ctor", m.TypeSystem.Void, true, m.TypeSystem.Object, m.TypeSystem.IntPtr)); il.Emit(OpCodes.Callvirt, Call(unityEvent, "AddListener", m.TypeSystem.Void, true, unityAction));
            il.Emit(OpCodes.Ldarg, configure.Parameters[3]); il.Emit(OpCodes.Callvirt, getButton); il.Emit(OpCodes.Ldloc, clickEvent); il.Emit(OpCodes.Callvirt, Call(button, "set_onClick", m.TypeSystem.Void, true, clicked));
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, s.SetUI);
            var compareKey = Call(m.TypeSystem.String, "op_Equality", m.TypeSystem.Boolean, false, m.TypeSystem.String, m.TypeSystem.String);
            var checkEmoteKey = Instruction.Create(OpCodes.Ldarg_1);
            var configured = Instruction.Create(OpCodes.Ret);
            il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldstr, PatchEngine.NormalKey); il.Emit(OpCodes.Call, compareKey); il.Emit(OpCodes.Brfalse, checkEmoteKey);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stsfld, normalControl); il.Emit(OpCodes.Br, configured);
            il.Append(checkEmoteKey); il.Emit(OpCodes.Ldstr, PatchEngine.EmoteKey); il.Emit(OpCodes.Call, compareKey); il.Emit(OpCodes.Brfalse, configured);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Stsfld, emoteControl); il.Append(configured);

            var toggleKeys = AddMethod(s.Toggle, ToggleKeysName, true, m.TypeSystem.Void);
            toggleKeys.Attributes = MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig;
            il = toggleKeys.Body.GetILProcessor();
            var checkEmoteShortcut = Instruction.Create(OpCodes.Ldc_I4, 290);
            var shortcutEnd = Instruction.Create(OpCodes.Ret);
            il.Emit(OpCodes.Ldc_I4, 288); il.Emit(OpCodes.Call, getKey); il.Emit(OpCodes.Brfalse, checkEmoteShortcut);
            il.Emit(OpCodes.Ldsfld, normalControl); il.Emit(OpCodes.Call, implicitBool); il.Emit(OpCodes.Brfalse, checkEmoteShortcut);
            il.Emit(OpCodes.Ldsfld, normalControl); il.Emit(OpCodes.Callvirt, s.ToggleClick);
            il.Append(checkEmoteShortcut); il.Emit(OpCodes.Call, getKey); il.Emit(OpCodes.Brfalse, shortcutEnd);
            il.Emit(OpCodes.Ldsfld, emoteControl); il.Emit(OpCodes.Call, implicitBool); il.Emit(OpCodes.Brfalse, shortcutEnd);
            il.Emit(OpCodes.Ldsfld, emoteControl); il.Emit(OpCodes.Callvirt, s.ToggleClick); il.Append(shortcutEnd);

            var controlKeys = AddMethod(s.MainCat, ControlKeysName, false, m.TypeSystem.Void);
            il = controlKeys.Body.GetILProcessor(); var controlsEnd = Instruction.Create(OpCodes.Ret);
            il.Emit(OpCodes.Call, Call(application, "get_isFocused", m.TypeSystem.Boolean, false)); il.Emit(OpCodes.Brfalse, controlsEnd);
            il.Emit(OpCodes.Call, s.InputFocused); il.Emit(OpCodes.Brtrue, controlsEnd);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, s.Menu); il.Emit(OpCodes.Call, implicitBool); il.Emit(OpCodes.Brfalse, controlsEnd);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, s.Menu); il.Emit(OpCodes.Call, getGameObject); il.Emit(OpCodes.Callvirt, Call(go, "get_activeSelf", m.TypeSystem.Boolean, true)); il.Emit(OpCodes.Brfalse, controlsEnd);
            il.Emit(OpCodes.Call, toggleKeys); il.Append(controlsEnd);
            var updateIL = s.MainUpdate.Body.GetILProcessor(); var originalUpdate = s.MainUpdate.Body.Instructions[0];
            updateIL.InsertBefore(originalUpdate, Instruction.Create(OpCodes.Ldarg_0));
            updateIL.InsertBefore(originalUpdate, Instruction.Create(OpCodes.Call, controlKeys));

            var create = AddMethod(s.Toggle, CreateName, true, m.TypeSystem.Void, s.Toggle, transform, m.TypeSystem.String, m.TypeSystem.Boolean, m.TypeSystem.String, m.TypeSystem.Int32);
            create.Attributes = MethodAttributes.Assembly | MethodAttributes.Static | MethodAttributes.HideBySig;
            var node = new VariableDefinition(s.Toggle); var row = new VariableDefinition(go);
            create.Body.Variables.Add(node); create.Body.Variables.Add(row);
            il = create.Body.GetILProcessor();
            il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Stsfld, guard);
            var tryStart = Instruction.Create(OpCodes.Ldarg_0); il.Append(tryStart);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, getTransform); il.Emit(OpCodes.Callvirt, getParent); il.Emit(OpCodes.Call, Instantiate(m, s.Toggle, obj, transform)); il.Emit(OpCodes.Stloc, node);
            var afterFinally = Instruction.Create(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Leave, afterFinally);
            var finallyStart = Instruction.Create(OpCodes.Ldc_I4_0); il.Append(finallyStart); il.Emit(OpCodes.Stsfld, guard); il.Emit(OpCodes.Endfinally); il.Append(afterFinally);
            create.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally) { TryStart = tryStart, TryEnd = finallyStart, HandlerStart = finallyStart, HandlerEnd = afterFinally });
            il.Emit(OpCodes.Ldfld, s.Check); EmitRow(il, getTransform, getParent, getGameObject); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, Instantiate(m, go, obj, transform)); il.Emit(OpCodes.Stloc, row);
            il.Emit(OpCodes.Ldloc, row); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Callvirt, Call(obj, "set_name", m.TypeSystem.Void, true, m.TypeSystem.String));
            il.Emit(OpCodes.Ldloc, node); il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Ldarg, create.Parameters[4]); il.Emit(OpCodes.Ldloc, row); il.Emit(OpCodes.Call, configure);
            il.Emit(OpCodes.Ldloc, row); il.Emit(OpCodes.Callvirt, getObjectTransform); il.Emit(OpCodes.Ldarg, create.Parameters[5]); il.Emit(OpCodes.Callvirt, Call(transform, "SetSiblingIndex", m.TypeSystem.Void, true, m.TypeSystem.Int32));
            il.Emit(OpCodes.Ldloc, row); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Callvirt, setActive); il.Emit(OpCodes.Ret);

            var controls = AddMethod(s.Settings, ControlsName, false, m.TypeSystem.Void);
            var autoStart = new VariableDefinition(s.AutoStart); var anchor = new VariableDefinition(go); var destination = new VariableDefinition(transform); var position = new VariableDefinition(m.TypeSystem.Int32);
            controls.Body.Variables.Add(autoStart); controls.Body.Variables.Add(anchor); controls.Body.Variables.Add(destination); controls.Body.Variables.Add(position);
            il = controls.Body.GetILProcessor(); var end = Instruction.Create(OpCodes.Ret);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, s.Source); il.Emit(OpCodes.Call, implicitBool); il.Emit(OpCodes.Brfalse, end);
            il.Emit(OpCodes.Call, Generic(obj, "FindObjectOfType", s.AutoStart, false, false)); il.Emit(OpCodes.Stloc, autoStart);
            il.Emit(OpCodes.Ldloc, autoStart); il.Emit(OpCodes.Call, implicitBool); il.Emit(OpCodes.Brfalse, end);
            il.Emit(OpCodes.Ldloc, autoStart); il.Emit(OpCodes.Call, rowAccessor); il.Emit(OpCodes.Stloc, anchor);
            il.Emit(OpCodes.Ldloc, anchor); il.Emit(OpCodes.Callvirt, getObjectTransform); il.Emit(OpCodes.Callvirt, getParent); il.Emit(OpCodes.Stloc, destination);
            il.Emit(OpCodes.Ldloc, destination); il.Emit(OpCodes.Ldstr, PatchEngine.NormalKey); il.Emit(OpCodes.Callvirt, find); il.Emit(OpCodes.Call, implicitBool); il.Emit(OpCodes.Brtrue, end);
            il.Emit(OpCodes.Ldloc, anchor); il.Emit(OpCodes.Callvirt, getObjectTransform); il.Emit(OpCodes.Callvirt, Call(transform, "GetSiblingIndex", m.TypeSystem.Int32, true)); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc, position);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, s.Source); il.Emit(OpCodes.Ldloc, destination); il.Emit(OpCodes.Ldstr, PatchEngine.NormalKey); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Ldstr, "Auto claim normal chests"); il.Emit(OpCodes.Ldloc, position); il.Emit(OpCodes.Call, create);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, s.Source); il.Emit(OpCodes.Ldloc, destination); il.Emit(OpCodes.Ldstr, PatchEngine.EmoteKey); il.Emit(emotes ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ldstr, "Auto claim emote chests"); il.Emit(OpCodes.Ldloc, position); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Call, create); il.Append(end);
            // Insert before the original return, leaving all initialization intact.
            var settingsIL = s.SettingsAwake.Body.GetILProcessor();
            var returns = s.SettingsAwake.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray();
            if (returns.Length != 1) throw Unsupported("SettingsManager.Awake control flow changed");
            settingsIL.InsertBefore(returns[0], Instruction.Create(OpCodes.Ldarg_0));
            settingsIL.InsertBefore(returns[0], Instruction.Create(OpCodes.Call, controls));
        }
    }
}
