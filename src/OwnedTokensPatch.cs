using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BongoClaim {
    // Transplant source-owned C# feature bodies and rebind every game/Unity
    // reference to the inspected installation. Compile-time stubs never leave
    // the patcher resource, and no companion runtime DLL is needed by the game.
    internal static class OwnedTokensPatch {
        private const string Prefix = "__BongoClaim_";
        private const string Create = "__BongoClaim_CreateTokenActions_v1";
        private const string Tick = "__BongoClaim_FeatureTick_v1";
        private const string MainTick = "__BongoClaim_RunFeatures_v1";
        private static AssemblyDefinition Template() {
            var stream = typeof(OwnedTokensPatch).Assembly.GetManifestResourceStream("BongoClaim.RuntimeTemplate.dll");
            if (stream == null) throw new InvalidOperationException("Runtime feature resource missing. Rebuild Bongo Cat Auto Chest.");
            using (stream)
            using (var copy = new System.IO.MemoryStream()) {
                stream.CopyTo(copy);
                return AssemblyDefinition.ReadAssembly(new System.IO.MemoryStream(copy.ToArray()), new ReaderParameters { InMemory = true });
            }
        }
        private sealed class Binder {
            internal readonly ModuleDefinition Target, Source;
            private readonly Dictionary<string, TypeDefinition> types = new Dictionary<string, TypeDefinition>();
            internal Binder(ModuleDefinition target, ModuleDefinition source) { Target = target; Source = source; }
            internal TypeReference Type(TypeReference source) {
                if (source is ArrayType) return new ArrayType(Type(((ArrayType)source).ElementType), ((ArrayType)source).Rank);
                if (source is ByReferenceType) return new ByReferenceType(Type(((ByReferenceType)source).ElementType));
                if (source is GenericInstanceType) {
                    var input = (GenericInstanceType)source; var result = new GenericInstanceType(Type(input.ElementType));
                    foreach (var arg in input.GenericArguments) result.GenericArguments.Add(Type(arg)); return result;
                }
                if (source is GenericParameter) return source;
                if (source.Scope == Source || source is TypeDefinition && ((TypeDefinition)source).Module == Source) return Target.ImportReference(Definition(source));
                return Target.ImportReference(source);
            }
            internal TypeDefinition Definition(TypeReference source) {
                TypeDefinition result;
                if (types.TryGetValue(source.FullName, out result)) return result;
                result = Target.GetTypes().SingleOrDefault(t => t.FullName == source.FullName);
                if (result == null) {
                    var coreScope = source.Scope as AssemblyNameReference;
                    if (coreScope != null && coreScope.Name == "mscorlib") {
                        var core = Target.AssemblyResolver.Resolve(Target.AssemblyReferences.Single(a => a.Name == "mscorlib"));
                        result = core.MainModule.GetTypes().FirstOrDefault(t => t.FullName == source.FullName);
                    }
                    var reference = Target.GetTypeReferences().FirstOrDefault(t => t.FullName == source.FullName);
                    if (result == null && reference != null) result = reference.Resolve();
                    else if (result == null) foreach (var scope in Target.AssemblyReferences) {
                        if (scope.Name == "mscorlib" || scope.Name.StartsWith("System")) continue;
                        var asm = Target.AssemblyResolver.Resolve(scope);
                        result = asm.MainModule.GetTypes().FirstOrDefault(t => t.FullName == source.FullName);
                        if (result != null) break;
                    }
                }
                if (result == null) throw new InvalidOperationException("Unsupported game inventory: type " + source.FullName + " is missing. Nothing was changed.");
                types[source.FullName] = result; return result;
            }
            internal MethodReference Method(MethodReference source) {
                var generic = source as GenericInstanceMethod;
                if (generic != null) {
                    var result = new GenericInstanceMethod(Method(generic.ElementMethod));
                    foreach (var arg in generic.GenericArguments) result.GenericArguments.Add(Type(arg)); return result;
                }
                var coreScope = source.DeclaringType.Scope as AssemblyNameReference;
                if ((coreScope == null || coreScope.Name != "mscorlib") && source.DeclaringType.Scope != Source && !(source.DeclaringType is TypeDefinition && ((TypeDefinition)source.DeclaringType).Module == Source)) return Target.ImportReference(source);
                var owner = Definition(source.DeclaringType);
                MethodDefinition method = null;
                for (var current = owner; current != null; current = current.BaseType == null ? null : current.BaseType.Resolve()) {
                    method = current.Methods.SingleOrDefault(m => m.Name == source.Name && m.HasThis == source.HasThis && m.ReturnType.FullName == source.ReturnType.FullName && m.GenericParameters.Count == source.GenericParameters.Count && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(source.Parameters.Select(p => p.ParameterType.FullName)));
                    if (method != null) break;
                }
                if (method == null) throw new InvalidOperationException("Unsupported game inventory: method " + source.FullName + " changed. Nothing was changed.");
                return Target.ImportReference(method);
            }
            internal FieldReference Field(FieldReference source) {
                if (source.DeclaringType.Scope != Source && !(source.DeclaringType is TypeDefinition && ((TypeDefinition)source.DeclaringType).Module == Source)) return Target.ImportReference(source);
                var owner = Definition(source.DeclaringType);
                var field = owner.Fields.SingleOrDefault(f => f.Name == source.Name && f.FieldType.FullName == source.FieldType.FullName);
                if (field == null) throw new InvalidOperationException("Unsupported game inventory: field " + source.FullName + " changed. Nothing was changed.");
                return Target.ImportReference(field);
            }
            internal object Operand(object source) {
                if (source is MethodReference) return Method((MethodReference)source);
                if (source is FieldReference) return Field((FieldReference)source);
                if (source is TypeReference) return Type((TypeReference)source);
                return source;
            }
        }
        private static bool Own(string name) { return name.StartsWith(Prefix, StringComparison.Ordinal); }
        public static void Validate(AssemblyDefinition asm) {
            using (var template = Template()) {
                var bind = new Binder(asm.MainModule, template.MainModule);
                foreach (var type in template.MainModule.GetTypes().Where(t => t.Methods.Any(m => Own(m.Name)))) {
                    foreach (var method in type.Methods.Where(m => Own(m.Name) && m.HasBody))
                        foreach (var i in method.Body.Instructions) {
                            var mr = i.Operand as MethodReference; var fr = i.Operand as FieldReference;
                            if (mr != null && Own(mr.Name)) {
                                // The action-row and Autostart accessors are added
                                // by NativeControlsPatch before this pass is applied.
                                bind.Definition(mr.DeclaringType); continue;
                            }
                            if (fr != null && Own(fr.Name)) continue;
                            var bound = bind.Operand(i.Operand);
                            var boundMethod = bound as MethodReference;
                            var boundField = bound as FieldReference;
                            if (boundMethod != null) {
                                var definition = boundMethod.Resolve();
                                if (definition == null || definition.IsPrivate && definition.DeclaringType.FullName != method.DeclaringType.FullName || definition.IsAssembly && definition.Module != asm.MainModule)
                                    throw new InvalidOperationException("Unsupported game inventory: inaccessible method " + boundMethod.FullName + ". Nothing was changed.");
                            }
                            if (boundField != null) {
                                var definition = boundField.Resolve();
                                bool staticInstruction = i.OpCode == OpCodes.Ldsfld || i.OpCode == OpCodes.Ldsflda || i.OpCode == OpCodes.Stsfld;
                                if (definition == null || definition.IsStatic != staticInstruction || definition.IsPrivate && definition.DeclaringType.FullName != method.DeclaringType.FullName || definition.IsAssembly && definition.Module != asm.MainModule)
                                    throw new InvalidOperationException("Unsupported game inventory: inaccessible field " + boundField.FullName + ". Nothing was changed.");
                            }
                        }
                }
            }
        }
        public static bool IsInstalled(AssemblyDefinition asm) {
            using (var template = Template()) {
                foreach (var source in template.MainModule.GetTypes()) {
                    var members = source.Methods.Where(m => Own(m.Name) && m.HasBody && m.Name != "__BongoClaim_CreateActionRow_v1" && m.Name != "__BongoClaim_GetRow_v1").ToArray();
                    if (members.Length == 0) continue;
                    var target = asm.MainModule.GetTypes().SingleOrDefault(t => t.FullName == source.FullName);
                    if (target == null || members.Any(m => target.Methods.Count(t => t.Name == m.Name && t.HasBody) != 1) || source.Fields.Where(f => Own(f.Name)).Any(f => target.Fields.Count(t => t.Name == f.Name) != 1)) return false;
                }
                var settings = asm.MainModule.GetTypes().Single(t => t.FullName == "BongoCat.SettingsManager");
                var main = asm.MainModule.GetTypes().Single(t => t.FullName == "BongoCat.MainCat");
                return Calls(settings.Methods.Single(m => m.Name == "__BongoClaim_CreateControls_v1"), Create) && main.Methods.Any(m => m.Name == MainTick && Calls(m, Tick)) && Calls(main.Methods.Single(m => m.Name == "Update"), MainTick);
            }
        }
        private static bool Calls(MethodDefinition method, string name) { return method.HasBody && method.Body.Instructions.Any(i => i.Operand is MethodReference && ((MethodReference)i.Operand).Name == name); }
        public static void Apply(AssemblyDefinition asm) {
            Validate(asm);
            using (var template = Template()) {
                var bind = new Binder(asm.MainModule, template.MainModule);
                var map = new Dictionary<MethodDefinition, MethodDefinition>();
                foreach (var source in template.MainModule.GetTypes()) {
                    var methods = source.Methods.Where(m => Own(m.Name) && m.HasBody && m.Name != "__BongoClaim_CreateActionRow_v1" && m.Name != "__BongoClaim_GetRow_v1").ToArray();
                    if (methods.Length == 0) continue;
                    var target = bind.Definition(source);
                    foreach (var field in source.Fields.Where(f => Own(f.Name))) {
                        if (target.Fields.Any(f => f.Name == field.Name)) throw new InvalidOperationException("Conflicting token controls detected.");
                        target.Fields.Add(new FieldDefinition(field.Name, field.Attributes, bind.Type(field.FieldType)));
                    }
                    foreach (var method in methods) {
                        if (target.Methods.Any(m => m.Name == method.Name)) throw new InvalidOperationException("Conflicting token controls detected.");
                        var copy = new MethodDefinition(method.Name, method.Attributes, bind.Type(method.ReturnType));
                        foreach (var p in method.Parameters) copy.Parameters.Add(new ParameterDefinition(p.Name, p.Attributes, bind.Type(p.ParameterType)));
                        target.Methods.Add(copy); map[method] = copy;
                    }
                }
                foreach (var pair in map) {
                    var source = pair.Key; var target = pair.Value;
                    target.Body.InitLocals = source.Body.InitLocals; target.Body.MaxStackSize = source.Body.MaxStackSize;
                    foreach (var v in source.Body.Variables) target.Body.Variables.Add(new VariableDefinition(bind.Type(v.VariableType)));
                    var instructions = new Dictionary<Instruction, Instruction>();
                    foreach (var i in source.Body.Instructions) { var copy = Instruction.Create(OpCodes.Nop); copy.OpCode = i.OpCode; target.Body.Instructions.Add(copy); instructions[i] = copy; }
                    foreach (var i in source.Body.Instructions) {
                        object operand = i.Operand;
                        if (operand is Instruction) operand = instructions[(Instruction)operand];
                        else if (operand is Instruction[]) operand = ((Instruction[])operand).Select(o => instructions[o]).ToArray();
                        else if (operand is VariableDefinition) operand = target.Body.Variables[((VariableDefinition)operand).Index];
                        else if (operand is ParameterDefinition) operand = target.Parameters[((ParameterDefinition)operand).Index];
                        else operand = bind.Operand(operand);
                        instructions[i].Operand = operand;
                    }
                    if (source.Body.ExceptionHandlers.Count != 0) throw new InvalidOperationException("Unexpected source runtime exception layout.");
                }
            }
            var module = asm.MainModule;
            var settings = module.GetTypes().Single(t => t.FullName == "BongoCat.SettingsManager");
            var create = settings.Methods.Single(m => m.Name == "__BongoClaim_CreateControls_v1");
            // Repeated Settings initialization can return early after finding the
            // first toggle, so append the idempotent action creation to every return.
            foreach (var ret in create.Body.Instructions.Where(i => i.OpCode == OpCodes.Ret).ToArray()) {
                var il = create.Body.GetILProcessor();
                var load = Instruction.Create(OpCodes.Ldarg_0);
                foreach (var i in create.Body.Instructions) { if (i.Operand == ret) i.Operand = load; }
                il.InsertBefore(ret, load); il.InsertBefore(ret, Instruction.Create(OpCodes.Call, settings.Methods.Single(m => m.Name == Create)));
            }
            var main = module.GetTypes().Single(t => t.FullName == "BongoCat.MainCat");
            var menu = main.Fields.Single(f => f.Name == "_menu");
            var go = (TypeReference)module.GetTypes().FirstOrDefault(t => t.FullName == "UnityEngine.GameObject") ?? module.GetTypeReferences().First(t => t.FullName == "UnityEngine.GameObject");
            var component = (TypeReference)module.GetTypes().FirstOrDefault(t => t.FullName == "UnityEngine.Component") ?? module.GetTypeReferences().First(t => t.FullName == "UnityEngine.Component");
            var getGo = new MethodReference("get_gameObject", go, component) { HasThis = true };
            var active = new MethodReference("get_activeSelf", module.TypeSystem.Boolean, go) { HasThis = true };
            var run = new MethodDefinition(MainTick, Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig, module.TypeSystem.Boolean);
            main.Methods.Add(run); var runIL = run.Body.GetILProcessor(); var hasMenu = Instruction.Create(OpCodes.Ldarg_0);
            runIL.Emit(OpCodes.Ldarg_0); runIL.Emit(OpCodes.Ldfld, menu); runIL.Emit(OpCodes.Brtrue, hasMenu); runIL.Emit(OpCodes.Ldc_I4_0); runIL.Emit(OpCodes.Call, settings.Methods.Single(m => m.Name == Tick)); runIL.Emit(OpCodes.Ret);
            runIL.Append(hasMenu); runIL.Emit(OpCodes.Ldfld, menu); runIL.Emit(OpCodes.Call, getGo); runIL.Emit(OpCodes.Callvirt, active); runIL.Emit(OpCodes.Call, settings.Methods.Single(m => m.Name == Tick)); runIL.Emit(OpCodes.Ret); run.Body.MaxStackSize = 2;
            var update = main.Methods.Single(m => m.Name == "Update"); var first = update.Body.Instructions[0]; var updateIL = update.Body.GetILProcessor();
            updateIL.InsertBefore(first, Instruction.Create(OpCodes.Ldarg_0)); updateIL.InsertBefore(first, Instruction.Create(OpCodes.Call, run)); updateIL.InsertBefore(first, Instruction.Create(OpCodes.Brfalse, first)); updateIL.InsertBefore(first, Instruction.Create(OpCodes.Ret));
            update.Body.MaxStackSize = Math.Max(update.Body.MaxStackSize, 2);
        }
    }
}
