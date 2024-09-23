using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using ShopAPI;

namespace Shop.Api;
public class ApiShop : IShopApi
{
    public event Action<CCSPlayerController, int, string, string, int, int, int, int>? ClientBuyItem;
    public event Action<CCSPlayerController, int, string, int>? ClientSellItem;
    public event Action<CCSPlayerController, int, string, int>? ClientToggleItem;
    public event Action<CCSPlayerController, int, string, int>? ClientUseItem;
    public event Action<CCSPlayerController, int, string, string>? ClientPreview;
    public event Func<CCSPlayerController, int, IShopApi.WhoChangeCredits, int?>? CreditsSet;
    public event Action<CCSPlayerController, int, IShopApi.WhoChangeCredits>? CreditsSetPost;
    public event Func<CCSPlayerController, int, IShopApi.WhoChangeCredits, int?>? CreditsAdd;
    public event Action<CCSPlayerController, int, IShopApi.WhoChangeCredits>? CreditsAddPost;
    public event Func<CCSPlayerController, int, IShopApi.WhoChangeCredits, int?>? CreditsTake;
    public event Action<CCSPlayerController, int, IShopApi.WhoChangeCredits>? CreditsTakePost;

    public class ItemCallbacks
    {
        public ItemCallbacks(int _ItemID, Func<CCSPlayerController, int, string, string, int, int, int, int, HookResult>? _OnBuyItem, Func<CCSPlayerController, int, string, int, HookResult>? _OnSellItem, Func<CCSPlayerController, int, string, int, HookResult>? _OnToggleItem, Func<CCSPlayerController, int, string, int, HookResult>? _OnUseItem, Action<CCSPlayerController, int, string, string>? _OnClientPreview)
        {
            ItemID = _ItemID;
            OnBuyItem = _OnBuyItem;
            OnSellItem = _OnSellItem;
            OnToggleItem = _OnToggleItem;
            OnUseItem = _OnUseItem;
            OnClientPreview = _OnClientPreview;
        }
        public int ItemID { get; set; }
        public Func<CCSPlayerController, int, string, string, int, int, int, int, HookResult>? OnBuyItem { get; set; }
        public Func<CCSPlayerController, int, string, int, HookResult>? OnSellItem { get; set; }
        public Func<CCSPlayerController, int, string, int, HookResult>? OnToggleItem { get; set; }
        public Func<CCSPlayerController, int, string, int, HookResult>? OnUseItem { get; set; }
        public Action<CCSPlayerController, int, string, string>? OnClientPreview { get; set; }
    }
    public List<ItemCallbacks> ItemCallback = new();
    public class FunctionsCallbacks
    {
        public FunctionsCallbacks(string _Display, Action<CCSPlayerController> _Callback)
        {
            Display = _Display;
            Callback = _Callback;
        }
        public string Display { get; set; }
        public Action<CCSPlayerController> Callback { get; set; }
    }
    public List<FunctionsCallbacks> FunctionsCallback = new();
    private readonly Shop _shop;
    public string dbConnectionString => _shop.dbConnectionString;
    public ApiShop(Shop shop)
    {
        _shop = shop;
        ItemCallback.Clear();
        FunctionsCallback.Clear();
    }
    
    public int GetClientID(CCSPlayerController player)
    {
        return _shop.playerInfo[player.Slot].DatabaseID;
    }

    public int GetClientCredits(CCSPlayerController player)
    {
        return _shop.GetClientCredits(player);
    }

    public void SetClientCredits(CCSPlayerController player, int сredits, IShopApi.WhoChangeCredits by_who = IShopApi.WhoChangeCredits.ByFunction)
    {
        _shop.SetClientCredits(player, сredits, by_who);
    }

    public void AddClientCredits(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who = IShopApi.WhoChangeCredits.ByFunction)
    {
        _shop.AddClientCredits(player, credits, by_who);
    }

    public void TakeClientCredits(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who = IShopApi.WhoChangeCredits.ByFunction)
    {
        _shop.TakeClientCredits(player, credits, by_who);
    }

    public void CreateCategory(string CategoryName, string DisplayName)
    {
        if(_shop.CategoryList.TryGetValue(CategoryName, out string? value) == false)
            _shop.CategoryList.Add(CategoryName, DisplayName);
    }

    public async Task<int> AddItem(string UniqueName, string ItemName, string CategoryName, int BuyPrice, int SellPrice, int Duration, int Count)
    {
        int id = -1;
        int index = -1;
        if((index = _shop.ItemsList.FindIndex(x => x.UniqueName == UniqueName && x.Category == CategoryName)) == -1)
        {
            id = await _shop.AddItemInDB(CategoryName, UniqueName, ItemName, BuyPrice, SellPrice, Duration, Count);
        }
        else
        {
            _shop.ItemsList[index].ItemName = ItemName;
            _shop.ItemsList[index].BuyPrice = BuyPrice;
            _shop.ItemsList[index].SellPrice = SellPrice;
            _shop.ItemsList[index].Duration = Duration;
            _shop.ItemsList[index].Count = Count;
            id = _shop.ItemsList[index].ItemID;
        }
        return id;
    }

    public bool IsItemExists(int ItemID)
    {
        return _shop.ItemsList.Find(x => x.ItemID == ItemID) != null;
    }

    public int GetItemPrice(int ItemID)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID);
        return index == -1 ? -1 : _shop.ItemsList[index].BuyPrice;
    }

    public bool SetItemPrice(int ItemID, int Price)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID);
        if(index == -1) return false;
        _shop.ItemsList[index].BuyPrice = Price;
        return true;
    }

    public int GetItemSellPrice(int ItemID)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID);
        return index == -1 ? -1 : _shop.ItemsList[index].SellPrice;
    }

    public bool SetItemSellPrice(int ItemID, int SellPrice)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID);
        if(index == -1) return false;
        _shop.ItemsList[index].SellPrice = SellPrice;
        return true;
    }

    public int GetItemDuration(int ItemID)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID);
        return index == -1 ? -1 : _shop.ItemsList[index].Duration;
    }

    public bool SetItemDuration(int ItemID, int Duration)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID && x.Count <= -1);
        if(index == -1) return false;
        _shop.ItemsList[index].Duration = Duration;
        return true;
    }

    public int GetItemCount(int ItemID)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID);
        return index == -1 ? -1 : _shop.ItemsList[index].Count;
    }

    public bool SetItemCount(int ItemID, int Count)
    {
        int index = _shop.ItemsList.FindIndex(x => x.ItemID == ItemID && x.Duration <= -1);
        if(index == -1) return false;
        _shop.ItemsList[index].Count = Count;
        return true;
    }

    public void OnClientBuyItem(CCSPlayerController player, int ItemID, string CategoryName, string UniqueName, int BuyPrice, int SellPrice, int Duration, int Count)
    {
        ClientBuyItem?.Invoke(player, ItemID, CategoryName, UniqueName, BuyPrice, SellPrice, Duration, Count);
    }

    public void OnClientSellItem(CCSPlayerController player, int ItemID, string UniqueName, int SellPrice)
    {
        ClientSellItem?.Invoke(player, ItemID, UniqueName, SellPrice);
    }

    public void OnClientToggleItem(CCSPlayerController player, int ItemID, string UniqueName, int State)
    {
        ClientToggleItem?.Invoke(player, ItemID, UniqueName, State);
    }

    public void OnClientUseItem(CCSPlayerController player, int ItemID, string UniqueName, int NewCount)
    {
        ClientUseItem?.Invoke(player, ItemID, UniqueName, NewCount);
    }

    public void OnClientPreview(CCSPlayerController player, int ItemID, string UniqueName, string Category)
    {
        ClientPreview?.Invoke(player, ItemID, UniqueName, Category);
    }

    public void SetItemCallbacks(int ItemID, Func<CCSPlayerController, int, string, string, int, int, int, int, HookResult>? OnBuyItem = null, Func<CCSPlayerController, int, string, int, HookResult>? OnSellItem = null, Func<CCSPlayerController, int, string, int, HookResult>? OnToggleItem = null, Func<CCSPlayerController, int, string, int, HookResult>? OnUseItem = null, Action<CCSPlayerController, int, string, string>? OnClientPreview = null)
    {
        ItemCallbacks? CallbackList;
        if((CallbackList = ItemCallback.Find(x => x.ItemID == ItemID)) != null) ItemCallback.Remove(CallbackList);
        ItemCallback.Add(new ItemCallbacks( ItemID, OnBuyItem, OnSellItem, OnToggleItem, OnUseItem, OnClientPreview ));
    }

    public bool IsClientAuthorized(CCSPlayerController player)
    {
        return _shop.playerInfo[player.Slot].DatabaseID != -1;
    }

    public int GetItemIdByUniqueName(string uniqueName)
    {
        Items? Item = _shop.ItemsList.Find(x => x.UniqueName == uniqueName);
        return Item == null ? -1 : Item.ItemID;
    }

    public bool GiveClientItem(CCSPlayerController player, int itemID, int customDuration)
    {
        Items? Item = _shop.ItemsList.Find(x => x.ItemID == itemID);
        if(Item == null) return false;

        return _shop.GivePlayerItem(player, Item, customDuration);
    }

    public bool RemoveClientItem(CCSPlayerController player, int itemID, int count = -1)
    {
        Items? Item = _shop.ItemsList.Find(x => x.ItemID == itemID);
        if(Item == null) return false;

        return _shop.TakePlayerItem(player, Item, count);
    }

    public bool IsAdmin(CCSPlayerController player)
    {
        return AdminManager.PlayerHasPermissions(player, _shop.Config.AdminFlag);
    }

    public void AddToFunctionsMenu(string display, Action<CCSPlayerController> openMenu)
    {
        FunctionsCallbacks? CallbackList;
        if((CallbackList = FunctionsCallback.Find(x => x.Callback == openMenu)) != null) FunctionsCallback.Remove(CallbackList);
        FunctionsCallback.Add(new FunctionsCallbacks( display, openMenu ));
    }

    public void RemoveFromFunctionsMenu(Action<CCSPlayerController> openMenu)
    {
        FunctionsCallback.RemoveAll(p => p.Callback == openMenu);
    }

    public void ShowFunctionsMenu(CCSPlayerController player)
    {
        _shop.OpenFunctionMenu(player);
    }

    public void OpenMainMenu(CCSPlayerController player)
    {
        _shop.OpenShopMenu(player);
    }

    public void UnregisterItem(int itemID)
    {
        _shop.ItemsList.RemoveAll(p => p.ItemID == itemID);
        ItemCallback.RemoveAll(p => p.ItemID == itemID);
    }

    public void UnregisterCategory(string categoryName, bool removeAllItems)
    {
        _shop.CategoryList.Remove(categoryName);

        if(removeAllItems)
        {
            var list = _shop.ItemsList.FindAll(p => p.Category == categoryName);
            foreach(var item in list)
            {
                UnregisterItem(item.ItemID);
            }
        }
    }

    public int? OnCreditsSet(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who)
    {
        return CreditsSet?.Invoke(player, credits, by_who);
    }
    public void OnCreditsSetPost(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who)
    {
        CreditsSetPost?.Invoke(player, credits, by_who);
    }
    public int? OnCreditsAdd(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who)
    {
        return CreditsAdd?.Invoke(player, credits, by_who);
    }
    public void OnCreditsAddPost(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who)
    {
        CreditsAddPost?.Invoke(player, credits, by_who);
    }
    public int? OnCreditsTake(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who)
    {
        return CreditsTake?.Invoke(player, credits, by_who);
    }
    public void OnCreditsTakePost(CCSPlayerController player, int credits, IShopApi.WhoChangeCredits by_who)
    {
        CreditsTakePost?.Invoke(player, credits, by_who);
    }
}