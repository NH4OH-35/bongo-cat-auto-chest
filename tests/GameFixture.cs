// Source-owned behavioral fixture, not decompiled game code.
using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        private static readonly List<Object> scene = new List<Object>();
        public static bool FailNextInstantiate;
        public Object() { scene.Add(this); }
        public virtual string name { get; set; }
        public static implicit operator bool(Object value) { return value != null; }
        public static T FindObjectOfType<T>() where T : Object { return scene.Find(o => o is T) as T; }
        public static T Instantiate<T>(T source, Transform parent) where T : Object
        {
            if (FailNextInstantiate) { FailNextInstantiate = false; throw new InvalidOperationException("source-owned fixture clone failure"); }
            var sourceGameObject = source as GameObject;
            if (sourceGameObject == null) sourceGameObject = ((Component)(Object)source).gameObject;
            var mapping = new Dictionary<Object, Object>();
            var clone = CloneTree(sourceGameObject, mapping);
            clone.name = sourceGameObject.name + "(Clone)";
            foreach (var pair in mapping)
            {
                var component = pair.Key as Component;
                if (component == null || component is Transform) continue;
                for (var type = component.GetType(); type != typeof(Component); type = type.BaseType)
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        var value = field.GetValue(component); Object replacement;
                        if (value is Object && mapping.TryGetValue((Object)value, out replacement)) value = replacement;
                        field.SetValue(pair.Value, value);
                    }
            }
            clone.transform.parent = parent;
            foreach (var pair in mapping)
            {
                var component = pair.Value as Component;
                if (component == null || !component.gameObject.activeSelf) continue;
                var awake = component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (awake != null) awake.Invoke(component, null);
            }
            return (T)mapping[source];
        }
        private static GameObject CloneTree(GameObject source, Dictionary<Object, Object> mapping)
        {
            var clone = new GameObject(source.name); clone.SetActive(source.activeSelf); mapping[source] = clone; mapping[source.transform] = clone.transform;
            foreach (var component in source.Components)
                if (!(component is Transform)) mapping[component] = clone.AddComponent(component.GetType());
            foreach (var child in source.transform.Children) CloneTree(child.gameObject, mapping).transform.parent = clone.transform;
            return clone;
        }
        public static void ResetScene() { scene.Clear(); FailNextInstantiate = false; }
    }
    public class GameObject : Object
    {
        internal readonly List<Component> Components = new List<Component>();
        public Transform transform { get; private set; }
        public bool activeSelf { get; private set; }
        public GameObject() : this("GameObject") { }
        public GameObject(string objectName) { name = objectName; activeSelf = true; transform = (Transform)AddComponent(typeof(Transform)); }
        public Component AddComponent(Type type) { var component = (Component)Activator.CreateInstance(type); component.Owner = this; Components.Add(component); return component; }
        public T AddComponent<T>() where T : Component, new() { return (T)AddComponent(typeof(T)); }
        public T GetComponent<T>() where T : Component { return Components.Find(c => c is T) as T; }
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component { var all = GetComponentsInChildren<T>(includeInactive); return all.Length == 0 ? null : all[0]; }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            var result = new List<T>(); if (includeInactive || activeSelf) { foreach (var component in Components) if (component is T) result.Add((T)component); foreach (var child in transform.Children) result.AddRange(child.gameObject.GetComponentsInChildren<T>(includeInactive)); } return result.ToArray();
        }
        public void SetActive(bool active) { activeSelf = active; }
    }
    public class Component : Object
    {
        internal GameObject Owner;
        public GameObject gameObject { get { return Owner; } }
        public Transform transform { get { return Owner.transform; } }
        public T GetComponent<T>() where T : Component { return Owner.GetComponent<T>(); }
        public override string name { get { return Owner == null ? base.name : Owner.name; } set { if (Owner == null) base.name = value; else Owner.name = value; } }
    }
    public class Behaviour : Component { public Behaviour() { enabled = true; } public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour { }
    public class Transform : Component
    {
        internal readonly List<Transform> Children = new List<Transform>();
        private Transform parentValue;
        public Transform parent { get { return parentValue; } set { if (parentValue != null) parentValue.Children.Remove(this); parentValue = value; if (value != null) value.Children.Add(this); } }
        public Transform Find(string path) { var current = this; foreach (var part in path.Split('/')) { current = current.Children.Find(child => child.gameObject.name == part); if (current == null) return null; } return current; }
        public int GetSiblingIndex() { return parent == null ? 0 : parent.Children.IndexOf(this); }
        public void SetSiblingIndex(int index) { if (parent == null) return; parent.Children.Remove(this); parent.Children.Insert(Math.Max(0, Math.Min(index, parent.Children.Count)), this); }
    }
    public sealed class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) { } }
    public enum KeyCode { F7 = 288, F9 = 290 }
    public static class Application { static Application() { isFocused = true; } public static bool isFocused { get; set; } }
    public static class Input
    {
        private static readonly HashSet<KeyCode> pressed = new HashSet<KeyCode>();
        public static bool GetKeyDown(KeyCode key) { return pressed.Remove(key); }
        public static void BeginFrame(bool normalKey, bool emoteKey) { pressed.Clear(); if (normalKey) pressed.Add(KeyCode.F7); if (emoteKey) pressed.Add(KeyCode.F9); }
    }
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, int> values = new Dictionary<string, int>();
        public static int Saves;
        public static int GetInt(string key, int defaultValue) { int value; return values.TryGetValue(key, out value) ? value : defaultValue; }
        public static void SetInt(string key, int value) { values[key] = value; }
        public static bool HasKey(string key) { return values.ContainsKey(key); }
        public static void DeleteAll() { values.Clear(); Saves = 0; }
        public static void Save() { Saves++; }
    }
}

namespace Helper { public static class InputFieldCache { public static bool FocusingInputField { get; set; } } }

namespace UnityEngine.Events
{
    public delegate void UnityAction();
    public delegate void UnityAction<T>(T value);
    public class UnityEvent
    {
        private event UnityAction listeners;
        public void AddListener(UnityAction listener) { listeners += listener; }
        public void Invoke() { if (listeners != null) listeners(); }
    }
    public class UnityEvent<T>
    {
        private event UnityAction<T> listeners;
        public void AddListener(UnityAction<T> listener) { listeners += listener; }
        public void Invoke(T value) { if (listeners != null) listeners(value); }
    }
}
namespace UnityEngine.UI
{
    public class Button : UnityEngine.Behaviour
    {
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
        public Button() { onClick = new ButtonClickedEvent(); }
        public ButtonClickedEvent onClick { get; set; }
        public void Click() { onClick.Invoke(); }
    }
}
namespace UnityEngine.Localization.Components { public class LocalizeStringEvent : UnityEngine.Behaviour { } }
namespace TMPro { public class TMP_Text : UnityEngine.Component { public string text { get; set; } } }
public class ToggleGameObject : UnityEngine.MonoBehaviour
{
    public UnityEngine.Events.UnityEvent<bool> OnToggle = new UnityEngine.Events.UnityEvent<bool>();
    public bool Active;
    public void SetActive(bool active) { Active = active; OnToggle.Invoke(active); }
}
namespace BongoCat.UiThemes { public class ToggleImageColor : UnityEngine.MonoBehaviour { } }

namespace BongoCat
{
    public class PlayerPrefsToggle : UnityEngine.MonoBehaviour
    {
        private string _playerPrefsKey;
        private global::ToggleGameObject _toggle;
        private BongoCat.UiThemes.ToggleImageColor _imageColor;
        private bool _invert, _defaultValue, _value;
        public UnityEngine.Events.UnityEvent<bool> OnToggleUpdated = new UnityEngine.Events.UnityEvent<bool>();
        public bool Value { get { return _value; } set { _value = value; SetUI(); } }
        private void Awake() { _value = UnityEngine.PlayerPrefs.GetInt(_playerPrefsKey, _defaultValue ? 1 : 0) != 0; SetUI(); }
        private void SetUI() { if (_toggle != null) _toggle.SetActive(_invert ? !_value : _value); }
        public void Toggle() { _value = !_value; UnityEngine.PlayerPrefs.SetInt(_playerPrefsKey, _value ? 1 : 0); UnityEngine.PlayerPrefs.Save(); OnToggleUpdated.Invoke(_value); SetUI(); }
        public void Configure(string key, global::ToggleGameObject toggle, bool defaultValue) { _playerPrefsKey = key; _toggle = toggle; _defaultValue = defaultValue; _invert = false; _imageColor = toggle.gameObject.GetComponent<BongoCat.UiThemes.ToggleImageColor>(); }
        public string Key { get { return _playerPrefsKey; } }
        public global::ToggleGameObject ToggleView { get { return _toggle; } }
        public BongoCat.UiThemes.ToggleImageColor Color { get { return _imageColor; } }
        public void Initialize() { Awake(); }
    }
    public class SetAutoStart : UnityEngine.MonoBehaviour
    {
        private global::ToggleGameObject _toggle;
        public void Configure(global::ToggleGameObject toggle) { _toggle = toggle; }
        public global::ToggleGameObject ToggleView { get { return _toggle; } }
    }
    public class SettingsManager : UnityEngine.MonoBehaviour
    {
        public static SettingsManager Instance;
        public PlayerPrefsToggle ShowChestPopup;
        public int OriginalAwakeCalls;
        private void Awake() { Instance = this; OriginalAwakeCalls++; }
        public void Initialize() { Awake(); }
    }
    public class MainCat : UnityEngine.MonoBehaviour
    {
        private global::ToggleGameObject _menu;
        public int OriginalUpdateCalls;
        private void Update() { OriginalUpdateCalls++; }
        public void SetMenu(global::ToggleGameObject menu) { _menu = menu; }
        public void Tick() { Update(); }
    }
    public class SettingsFixture
    {
        public SettingsManager Manager;
        public MainCat Cat;
        public UnityEngine.GameObject Menu;
        public UnityEngine.GameObject General, Visuals;
        public int SourceEvents, SourceButtonClicks;
        public static SettingsFixture Create()
        {
            var fixture = new SettingsFixture(); UnityEngine.Object.ResetScene();
            fixture.General = new UnityEngine.GameObject("General"); fixture.Visuals = new UnityEngine.GameObject("Visuals");
            var autoRow = fixture.CreateRow(fixture.General, "Autostart");
            var autoStart = new UnityEngine.GameObject("SetAutoStart").AddComponent<SetAutoStart>(); autoStart.Configure(autoRow.GetComponentInChildren<global::ToggleGameObject>(true));
            var sourceRow = fixture.CreateRow(fixture.Visuals, "Chest popup");
            var source = new UnityEngine.GameObject("ShowChestPopup").AddComponent<PlayerPrefsToggle>(); source.Configure("show_chest_popup", sourceRow.GetComponentInChildren<global::ToggleGameObject>(true), true);
            source.OnToggleUpdated.AddListener(delegate(bool value) { fixture.SourceEvents++; });
            source.ToggleView.OnToggle.AddListener(delegate(bool value) { fixture.SourceEvents++; });
            sourceRow.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.AddListener(delegate { fixture.SourceButtonClicks++; source.Toggle(); });
            fixture.Manager = new UnityEngine.GameObject("SettingsManager").AddComponent<SettingsManager>(); fixture.Manager.ShowChestPopup = source;
            fixture.Menu = new UnityEngine.GameObject("GameMenu"); var menuToggle = fixture.Menu.AddComponent<global::ToggleGameObject>();
            fixture.Cat = new UnityEngine.GameObject("MainCat").AddComponent<MainCat>(); fixture.Cat.SetMenu(menuToggle);
            fixture.SetContext(true, true, false);
            return fixture;
        }
        private UnityEngine.GameObject Child(UnityEngine.GameObject parent, string name) { var child = new UnityEngine.GameObject(name); child.transform.parent = parent.transform; return child; }
        private UnityEngine.GameObject CreateRow(UnityEngine.GameObject parent, string label)
        {
            var row = Child(parent, label + "Row"); row.AddComponent<UnityEngine.UI.Button>(); var content = Child(row, "Content");
            var labelObject = Child(content, "Label"); labelObject.AddComponent<TMPro.TMP_Text>().text = label; labelObject.AddComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
            var button = Child(content, "Button");
            var checkmark = Child(button, "Checkmark"); checkmark.AddComponent<global::ToggleGameObject>(); checkmark.AddComponent<UiThemes.ToggleImageColor>();
            var tooltip = Child(row, "HoverTooltip"); var text = Child(tooltip, "Text"); text.AddComponent<TMPro.TMP_Text>().text = "Original tooltip"; text.AddComponent<UnityEngine.Localization.Components.LocalizeStringEvent>();
            Child(row, "DisableOverlay");
            return row;
        }
        public void Initialize() { Manager.Initialize(); }
        public void SetContext(bool gameFocused, bool menuOpen, bool textInputFocused) { UnityEngine.Application.isFocused = gameFocused; Menu.SetActive(menuOpen); Helper.InputFieldCache.FocusingInputField = textInputFocused; }
        public void Frame(bool normalKey, bool emoteKey) { UnityEngine.Input.BeginFrame(normalKey, emoteKey); Cat.Tick(); }
        public void ClearMenu() { Cat.SetMenu(null); }
        public int OriginalUpdateCount() { return Cat.OriginalUpdateCalls; }
        public int GeneralRowCount() { return General.transform.Children.Count; }
        public string RowLabel(int index) { return General.transform.Children[index].Find("Content/Label").gameObject.GetComponent<TMPro.TMP_Text>().text; }
        public string RowTooltip(int index) { return General.transform.Children[index].Find("HoverTooltip/Text").gameObject.GetComponent<TMPro.TMP_Text>().text; }
        public bool RowToggleActive(int index) { return General.transform.Children[index].gameObject.GetComponentInChildren<global::ToggleGameObject>(true).Active; }
        public bool RowLocalizersEnabled(int index) { foreach (var localizer in General.transform.Children[index].gameObject.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true)) if (localizer.enabled) return true; return false; }
        public void ClickRow(int index) { General.transform.Children[index].gameObject.GetComponentInChildren<UnityEngine.UI.Button>(true).Click(); }
        public string OriginalLabel() { return Visuals.transform.Children[0].Find("Content/Label").gameObject.GetComponent<TMPro.TMP_Text>().text; }
        public bool OriginalLocalizersEnabled() { return Visuals.transform.Children[0].gameObject.GetComponentInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true).enabled; }
    }
    public sealed class Pets
    {
        public static Pets Instance = new Pets();
        public int Balance = 10000;
        public bool Allowed = true;
        public bool CanSpendPets(int price) { return Allowed && Balance >= price; }
    }

    public sealed class Shop
    {
        public bool ChestIsReady;
        private bool _openingChest;
        private bool _isEmoteShop;
        private ShopItem _shopItem;
        public bool Shown;
        public int Popups;
        public int UnrelatedFeature = 42;
        public Shop() { _shopItem = new ShopItem(this); }
        public void Configure(bool ready, bool opening, bool emote) { ChestIsReady = ready; _openingChest = opening; _isEmoteShop = emote; }
        public ShopItem GetItem() { return _shopItem; }
        public void ClearItem() { _shopItem = null; }
        public void OnChestReady() { Shown = true; Popups++; }
        public void SetLoadingVisuals() { _openingChest = true; }
        public void FinishPurchase(bool success) { _openingChest = false; if (success) ChestIsReady = false; }
        public IEnumerator TimerUpdate()
        {
            while (true)
            {
                if (ChestIsReady && !Shown) OnChestReady();
                yield return new UnityEngine.WaitForSecondsRealtime(1);
            }
        }
    }

    public sealed class ShopItem
    {
        private bool _waitingForServer;
        private readonly Shop _shop;
        public int Purchases;
        public ShopItem(Shop shop) { _shop = shop; }
        public bool CanBuy() { return Pets.Instance.CanSpendPets(1000); }
        public void Buy() { StartBuy(true); }
        private void StartBuy(bool chargePets)
        {
            if (chargePets && !Pets.Instance.CanSpendPets(1000)) return;
            if (_waitingForServer) return;
            _waitingForServer = true;
            _shop.SetLoadingVisuals();
            Purchases++;
        }
        public void SetWaiting(bool waiting) { _waitingForServer = waiting; }
        public void Complete(bool success) { _waitingForServer = false; _shop.FinishPurchase(success); }
    }
}
