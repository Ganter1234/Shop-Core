using CounterStrikeSharp.API.Core;
using ShopAPI;

namespace Shop.Api;
public class ApiShop : IShopApi
{
    public event Action<CCSPlayerController, int, string, string, int, int, int, int>? ClientBuyItem;
    public event Action<CCSPlayerController, int, string, int>? ClientSellItem;
    public event Action<CCSPlayerController, int, string, int>? ClientToggleItem;
    public event Action<CCSPlayerController, int, string, int>? ClientUseItem;
    public class ItemCallbacks
    {
        public ItemCallbacks(int _ItemID, Action<CCSPlayerController, int, string, string, int, int, int, int>? _OnBuyItem, Action<CCSPlayerController, int, string, int>? _OnSellItem, Action<CCSPlayerController, int, string, int>? _OnToggleItem, Action<CCSPlayerController, int, string, int>? _OnUseItem)
        {
            ItemID = _ItemID;
            OnBuyItem = _OnBuyItem;
            OnSellItem = _OnSellItem;
            OnToggleItem = _OnToggleItem;
            OnUseItem = _OnUseItem;
        }
        public int ItemID { get; set; }
        public Action<CCSPlayerController, int, string, string, int, int, int, int>? OnBuyItem { get; set; }
        public Action<CCSPlayerController, int, string, int>? OnSellItem { get; set; }
        public Action<CCSPlayerController, int, string, int>? OnToggleItem { get; set; }
        public Action<CCSPlayerController, int, string, int>? OnUseItem { get; set; }
    }
    public List<ItemCallbacks> ItemCallback = new();
    private readonly Shop _shop;
    public string dbConnectionString { get; }

    public ApiShop(Shop shop)
    {
        _shop = shop;
        dbConnectionString = shop.dbConnectionString;
        ItemCallback.Clear();
    }
    public int GetClientID(CCSPlayerController player)
    {
        return _shop.playerInfo[player.Slot].DatabaseID;
    }
    public int GetClientCredits(CCSPlayerController player)
    {
        return _shop.GetClientCredits(player);
    }
    public void SetClientCredits(CCSPlayerController player, int Credits)
    {
        _shop.SetClientCredits(player, Credits);
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
    public void SetItemCallbacks(int ItemID, Action<CCSPlayerController, int, string, string, int, int, int, int>? OnBuyItem = null, Action<CCSPlayerController, int, string, int>? OnSellItem = null, Action<CCSPlayerController, int, string, int>? OnToggleItem = null)
    {
        ItemCallbacks? CallbackList;
        if((CallbackList = ItemCallback.Find(x => x.ItemID == ItemID)) != null) ItemCallback.Remove(CallbackList);
        ItemCallback.Add(new ItemCallbacks( ItemID, OnBuyItem, OnSellItem, OnToggleItem, null ));
    }
    public void SetItemCallbacks(int ItemID, Action<CCSPlayerController, int, string, string, int, int, int, int>? OnBuyItem = null, Action<CCSPlayerController, int, string, int>? OnSellItem = null, Action<CCSPlayerController, int, string, int>? OnToggleItem = null, Action<CCSPlayerController, int, string, int>? OnUseItem = null)
    {
        ItemCallbacks? CallbackList;
        if((CallbackList = ItemCallback.Find(x => x.ItemID == ItemID)) != null) ItemCallback.Remove(CallbackList);
        ItemCallback.Add(new ItemCallbacks( ItemID, OnBuyItem, OnSellItem, OnToggleItem, OnUseItem ));
    }
}