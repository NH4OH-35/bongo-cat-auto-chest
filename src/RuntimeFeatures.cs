// Only __BongoClaim_ members are transplanted into the installed game's types.
// Its inventory, purchase callback and Steam stat APIs remain the owners of state.
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
namespace BongoCat {
    public partial class ShopItem {
        public bool __BongoClaim_CanOpenOwned_v1() {
            return !_waitingForServer && _chestExchanger && !Steam.ChestExchanger.__BongoClaim_IsBusy_v1()
                && SteamTools.Interface.IsReady
                && SteamTools.Game.Inventory.Achievement_Token.GetTotalQuantity() > 0
                && SteamTools.Game.Inventory.ManualToken.GetTotalQuantity() > 0
                && _chestExchanger.__BongoClaim_CanExchange_v1() && CanBuy();
        }
    }
    public partial class Shop {
        public bool __BongoClaim_OpenOwned_v1(bool emote) {
            if (_isEmoteShop != emote || _openingChest || !_shopItem) return false;
            long quantity = emote ? SteamTools.Game.Inventory.Emote_Chest_Token.GetTotalQuantity() : SteamTools.Game.Inventory.Chest_Token.GetTotalQuantity();
            if (quantity <= 0 || !_shopItem.__BongoClaim_CanOpenOwned_v1()) return false;
            _shopItem.Buy(); return true;
        }
        public bool __BongoClaim_IsOwnedType_v1(bool emote) { return _isEmoteShop == emote; }
    }
    public partial class Pets {
        public int __BongoClaim_GetScore_v1() { return Current; }
        public bool __BongoClaim_SetScore_v1(int score) {
            if (!_init || score < 0 || _totalSpent < 0 || score > 2147483646 - _totalSpent) return false;
            _currentGained = score + _totalSpent;
            UpdateStats();
            Heathen.SteamworksIntegration.API.StatsAndAchievements.Client.StoreStats();
            return true;
        }
    }
    public partial class SettingsManager {
        private static GameObject __BongoClaim_NormalTokensRow_v1, __BongoClaim_EmoteTokensRow_v1, __BongoClaim_ScoreRow_v1;
        private static bool __BongoClaim_EditingScore_v1;
        private static string __BongoClaim_ScoreBuffer_v1;
        private static float __BongoClaim_NextRefresh_v1;
        private static void __BongoClaim_SetLabel_v1(GameObject row, string text) {
            if (!row) return;
            var label = row.transform.Find("Content/Label").GetComponent<TMP_Text>();
            if (label.text != text) label.text = text;
        }
        private static void __BongoClaim_ClaimNormal_v1() { __BongoClaim_ClaimOwned_v1(false); }
        private static void __BongoClaim_ClaimEmote_v1() { __BongoClaim_ClaimOwned_v1(true); }
        private static void __BongoClaim_ClaimOwned_v1(bool emote) {
            if (__BongoClaim_EditingScore_v1) return;
            var shops = UnityEngine.Object.FindObjectsOfType<Shop>(true);
            foreach (var shop in shops) {
                if (shop && shop.__BongoClaim_IsOwnedType_v1(emote)) { shop.__BongoClaim_OpenOwned_v1(emote); break; }
            }
            __BongoClaim_NextRefresh_v1 = 0;
        }
        private static void __BongoClaim_EditScore_v1() {
            if (!Pets.Instance || !__BongoClaim_ScoreRow_v1) return;
            __BongoClaim_EditingScore_v1 = !__BongoClaim_EditingScore_v1;
            __BongoClaim_ScoreBuffer_v1 = "";
            __BongoClaim_NextRefresh_v1 = 0;
            __BongoClaim_SetLabel_v1(__BongoClaim_ScoreRow_v1, "Type score; Enter to save");
        }
        public void __BongoClaim_CreateTokenActions_v1() {
            var autoStart = UnityEngine.Object.FindObjectOfType<SetAutoStart>();
            if (!autoStart || !ShowChestPopup) return;
            var anchor = autoStart.__BongoClaim_GetRow_v1();
            var parent = anchor.transform.parent;
            if (parent.Find("BongoClaim_NormalTokens")) return;
            int index = anchor.transform.GetSiblingIndex() + 4;
            __BongoClaim_NormalTokensRow_v1 = PlayerPrefsToggle.__BongoClaim_CreateActionRow_v1(ShowChestPopup, parent, "BongoClaim_NormalTokens", "Normal tokens: loading", "Consume one owned normal chest token, even during the local countdown. F11 opens one while the menu is open. No new tokens are generated.", __BongoClaim_ClaimNormal_v1, index);
            __BongoClaim_EmoteTokensRow_v1 = PlayerPrefsToggle.__BongoClaim_CreateActionRow_v1(ShowChestPopup, parent, "BongoClaim_EmoteTokens", "Emote tokens: loading", "Consume one owned emote chest token, even during the local countdown. Insert opens one while the menu is open. Each click opens one chest.", __BongoClaim_ClaimEmote_v1, index + 1);
            __BongoClaim_ScoreRow_v1 = PlayerPrefsToggle.__BongoClaim_CreateActionRow_v1(ShowChestPopup, parent, "BongoClaim_EditScore", "Edit pet score", "Click, type a nonnegative whole number, then Enter to save or Escape to cancel. Backspace deletes. Changes available pets and saves through Steam stats; lifetime achievement taps and total spent are preserved. F10 also starts editing while the menu is open.", __BongoClaim_EditScore_v1, index + 2);
            __BongoClaim_NextRefresh_v1 = 0;
        }
        private static void __BongoClaim_RefreshToken_v1(GameObject row, string name, long quantity, bool ready) {
            if (!row) return;
            __BongoClaim_SetLabel_v1(row, ready ? name + ": " + quantity.ToString() + " | claim" : name + ": loading");
            row.GetComponent<Button>().interactable = ready && quantity > 0 && !Steam.ChestExchanger.__BongoClaim_IsBusy_v1() && !__BongoClaim_EditingScore_v1;
        }
        public static bool __BongoClaim_FeatureTick_v1(bool menuOpen) {
            bool editing = __BongoClaim_EditingScore_v1;
            if (!menuOpen) { __BongoClaim_EditingScore_v1 = false; __BongoClaim_NextRefresh_v1 = 0; }
            if (menuOpen && Application.isFocused && !Helper.InputFieldCache.FocusingInputField) {
                if (!__BongoClaim_EditingScore_v1 && Input.GetKeyDown(KeyCode.F11)) __BongoClaim_ClaimNormal_v1();
                if (!__BongoClaim_EditingScore_v1 && Input.GetKeyDown(KeyCode.Insert)) __BongoClaim_ClaimEmote_v1();
                if (!__BongoClaim_EditingScore_v1 && Input.GetKeyDown(KeyCode.F10)) __BongoClaim_EditScore_v1();
                else if (__BongoClaim_EditingScore_v1) {
                    if (Input.GetKeyDown(KeyCode.Escape)) { __BongoClaim_EditingScore_v1 = false; __BongoClaim_NextRefresh_v1 = 0; }
                    else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
                        int value;
                        if (Int32.TryParse(__BongoClaim_ScoreBuffer_v1, out value) && Pets.Instance && Pets.Instance.__BongoClaim_SetScore_v1(value)) {
                            __BongoClaim_EditingScore_v1 = false; __BongoClaim_NextRefresh_v1 = 0;
                        } else { __BongoClaim_ScoreBuffer_v1 = ""; __BongoClaim_SetLabel_v1(__BongoClaim_ScoreRow_v1, "Invalid score; type again"); }
                    } else {
                        bool changed = false;
                        if (Input.GetKeyDown(KeyCode.Backspace) && __BongoClaim_ScoreBuffer_v1.Length > 0) { __BongoClaim_ScoreBuffer_v1 = __BongoClaim_ScoreBuffer_v1.Substring(0, __BongoClaim_ScoreBuffer_v1.Length - 1); changed = true; }
                        for (int digit = 0; digit <= 9; digit++) {
                            if ((Input.GetKeyDown((KeyCode)(48 + digit)) || Input.GetKeyDown((KeyCode)(256 + digit))) && __BongoClaim_ScoreBuffer_v1.Length < 10) { __BongoClaim_ScoreBuffer_v1 += ((char)('0' + digit)).ToString(); changed = true; }
                        }
                        if (changed) __BongoClaim_SetLabel_v1(__BongoClaim_ScoreRow_v1, "Score: " + __BongoClaim_ScoreBuffer_v1 + "_ (Enter)");
                    }
                }
            }
            if (menuOpen && Time.unscaledTime >= __BongoClaim_NextRefresh_v1) {
                __BongoClaim_NextRefresh_v1 = Time.unscaledTime + 0.5f;
                bool ready = SteamTools.Interface.IsReady;
                __BongoClaim_RefreshToken_v1(__BongoClaim_NormalTokensRow_v1, "Normal tokens", ready ? SteamTools.Game.Inventory.Chest_Token.GetTotalQuantity() : 0, ready);
                __BongoClaim_RefreshToken_v1(__BongoClaim_EmoteTokensRow_v1, "Emote tokens", ready ? SteamTools.Game.Inventory.Emote_Chest_Token.GetTotalQuantity() : 0, ready);
                if (!__BongoClaim_EditingScore_v1 && Pets.Instance) __BongoClaim_SetLabel_v1(__BongoClaim_ScoreRow_v1, "Pets: " + Pets.Instance.__BongoClaim_GetScore_v1().ToString() + " | edit");
            }
            return menuOpen && (editing || __BongoClaim_EditingScore_v1);
        }
    }
}
namespace Steam {
    public partial class ChestExchanger {
        public static bool __BongoClaim_IsBusy_v1() { return IsExchanging; }
        public bool __BongoClaim_CanExchange_v1() { return _exchange != null && _exchange.CanExchange(); }
    }
}
