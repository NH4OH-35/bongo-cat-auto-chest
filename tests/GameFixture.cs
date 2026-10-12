// Source-owned behavioral fixture, not decompiled game code.
using System.Collections;

namespace UnityEngine
{
    public sealed class WaitForSecondsRealtime { public WaitForSecondsRealtime(float seconds) { } }
}

namespace BongoCat
{
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
