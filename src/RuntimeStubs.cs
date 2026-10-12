// Compile-time signatures only. No stub implementation is installed or shipped.
using System;
namespace UnityEngine {
    public class Object {
        public static implicit operator bool(Object o) { return o != null; }
        public static T FindObjectOfType<T>() where T : Object { return null; }
        public static T[] FindObjectsOfType<T>(bool inactive) where T : Object { return null; }
    }
    public class GameObject : Object { public Transform transform { get { return null; } } public bool activeSelf { get { return true; } } public T GetComponent<T>() { return default(T); } }
    public class Component : Object { public GameObject gameObject { get { return null; } } public Transform transform { get { return null; } } public T GetComponent<T>() { return default(T); } }
    public class MonoBehaviour : Component { }
    public class Transform : Component { public Transform parent { get { return null; } } public Transform Find(string path) { return null; } public int GetSiblingIndex() { return 0; } }
    public enum KeyCode { Backspace=8, Return=13, Escape=27, Insert=277, F10=291, F11=292, KeypadEnter=271 }
    public static class Input { public static bool GetKeyDown(KeyCode key) { return false; } }
    public static class Application { public static bool isFocused { get { return false; } } }
    public static class Time { public static float unscaledTime { get { return 0; } } }
}
namespace UnityEngine.Events { public delegate void UnityAction(); }
namespace UnityEngine.UI { public class Button : UnityEngine.Component { public bool interactable { get; set; } } }
namespace TMPro { public class TMP_Text : UnityEngine.Component { public string text { get; set; } } }
namespace Helper { public static class InputFieldCache { public static bool FocusingInputField { get { return false; } } } }
namespace Heathen.SteamworksIntegration {
    public struct ItemData { public long GetTotalQuantity() { return 0; } }
    namespace API { public static class StatsAndAchievements { public static class Client { public static bool StoreStats() { return false; } } } }
}
namespace SteamTools {
    public static class Interface { public static bool IsReady { get { return false; } } }
    public static class Game { public static class Inventory { public static Heathen.SteamworksIntegration.ItemData Chest_Token, Emote_Chest_Token, Achievement_Token, ManualToken; } }
}
namespace Steam { public partial class ChestExchanger : UnityEngine.MonoBehaviour { private static bool IsExchanging; private BongoCat.SteamJsonParser.SteamExchange _exchange; } }
namespace BongoCat.SteamJsonParser { public class SteamExchange { public bool CanExchange() { return false; } } }
namespace BongoCat {
    public class PlayerPrefsToggle : UnityEngine.MonoBehaviour {
        public static UnityEngine.GameObject __BongoClaim_CreateActionRow_v1(PlayerPrefsToggle source, UnityEngine.Transform parent, string rowName, string label, string tooltip, UnityEngine.Events.UnityAction onClick, int index) { return null; }
    }
    public class SetAutoStart : UnityEngine.MonoBehaviour { public UnityEngine.GameObject __BongoClaim_GetRow_v1() { return null; } }
    public partial class SettingsManager : UnityEngine.MonoBehaviour { public static SettingsManager Instance; public PlayerPrefsToggle ShowChestPopup; }
    public partial class Shop : UnityEngine.MonoBehaviour { private bool _isEmoteShop, _openingChest; private ShopItem _shopItem; }
    public partial class ShopItem : UnityEngine.MonoBehaviour { private bool _waitingForServer; private Steam.ChestExchanger _chestExchanger; public bool CanBuy() { return false; } public void Buy() { } }
    public partial class Pets : UnityEngine.MonoBehaviour {
        public static Pets Instance; private int _currentGained, _totalSpent; private bool _init;
        private int Current { get { return 0; } } private void UpdateStats() { }
    }
}
