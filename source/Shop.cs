using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Dapper;
using CounterStrikeSharp.API.Core.Capabilities;
using ShopAPI;
using Timer = CounterStrikeSharp.API.Modules.Timers.Timer;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Core.Translations;
using Microsoft.Extensions.Localization;

namespace Shop;
public class Shop : BasePlugin, IPluginConfig<ShopConfig>
{
    public override string ModuleName => "Shop Core";
    public override string ModuleDescription => "Modular shop system";
    public override string ModuleAuthor => "Ganter1234";
    public override string ModuleVersion => "2.3";
    public ShopConfig Config { get; set; } = new();
    public PlayerInformation[] playerInfo = new PlayerInformation[65];
    public List<Items> ItemsList = new();
    public Dictionary<string, string> CategoryList = new();
    public string dbConnectionString = string.Empty;
    public CCSPlayerController[] transPlayer = new CCSPlayerController[65];
    private Api.ApiShop? _api;
    public new IStringLocalizer Localizer => base.Localizer;
    
    public override void Load(bool hotReload)
    {
        _api = new Api.ApiShop(this); 
        Capabilities.RegisterPluginCapability(IShopApi.Capability, () => _api);

        CategoryList.Clear();

        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);

		if(hotReload)
		{
			foreach(var player in Utilities.GetPlayers())
			{
				if(player.AuthorizedSteamID != null)
					OnClientAuthorized(player.Slot, player.AuthorizedSteamID);
			}
		}

        AddCommandListener("say", OnClientSay, HookMode.Post);
        AddCommandListener("say_team", OnClientSay, HookMode.Post);
        AddCommand("css_add_credits", "Add credits to a player", CommandAddCredits);
		AddCommand("css_set_credits", "Set credits to a player", CommandSetCredits);
		AddCommand("css_take_credits", "Take credits to a player", CommandTakeCredits);
        AddCommand("css_add_item", "Add item to a player", CommandAddItem);
		AddCommand("css_take_item", "Take item to a player", CommandTakeItem);
    }

    #region Menus
    public IMenu CreateMenu(string title)
    {
        if (Config.UseCenterMenu == false)
        {
            ChatMenu menu = new ChatMenu(title) { ExitButton = true };
            return menu;
        }
        else
        {
            CenterHtmlMenu menu = new CenterHtmlMenu(title, this) { ExitButton = true };
            return menu;
        }
    }
    public void OpenShopMenu(CCSPlayerController? player)
    {
        if(player == null) return;

        if(playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1)
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["YourDataLoad"]));
            return;
        }

        var menu = CreateMenu(Localizer["Menu_ShopTitle", GetClientCredits(player)]);
        menu.AddMenuOption(Localizer["Menu_Buy"], (player, _) => OpenBuyMenu(player));
        menu.AddMenuOption(Localizer["Menu_Inventory"], (player, _) => OpenInventoryMenu(player));
        menu.AddMenuOption(Localizer["Menu_Functions"], (player, _) => OpenFunctionMenu(player));
        menu.Open(player);
    }

    public void OpenBuyMenu(CCSPlayerController player)
    {
        var menu = CreateMenu(Localizer["Menu_BuyCategoriesTitle"]);
        CreateCategoryMenu(menu, false);
        menu.Open(player);
    }

    public void OpenInventoryMenu(CCSPlayerController player)
    {
        var menu = CreateMenu(Localizer["Menu_InventoryCategoriesTitle"]);
        CreateCategoryMenu(menu, true);
        menu.Open(player);
    }

    public void OpenFunctionMenu(CCSPlayerController player)
    {
        var menu = CreateMenu(Localizer["Menu_FunctionsTitle"]);
        menu.AddMenuOption(Localizer["Menu_FunctionsTransferCredits", Config.TransCreditsPercent == -1 ? Localizer["Menu_FunctionsTransferCreditsOFF"] : Config.TransCreditsPercent == 0 ? "" : Localizer["Menu_FunctionsTransferCreditsComission", Config.TransCreditsPercent]],
        (player, _) => OpenTransferMenu(player), Config.TransCreditsPercent == -1);
        foreach(var functions in _api!.FunctionsCallback)
        {
            menu.AddMenuOption(Localizer[functions.Display], (players, _) => {
                MenuManager.CloseActiveMenu(players);
                Server.NextFrame(() => functions.Callback.Invoke(players) );
            });
        }
        menu.Open(player);
    }
    public void OpenTransferMenu(CCSPlayerController player)
    {
        var menu = CreateMenu(Localizer["Menu_TransferCreditsSelectPlayer"]);

        foreach (var players in Utilities.GetPlayers().Where(x => !x.IsBot && !x.IsHLTV && x != player))
        {
            menu.AddMenuOption($"{players.PlayerName} [{GetClientCredits(players)}]", (player, _) =>
            {
                if(players == null || !players.IsValid) 
                {
                    player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["PlayerNotAvailable"]));
                    return;
                }

                transPlayer[player.Slot] = players;
                player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["TransferCreditsInfo"]));
            });
        }

        menu.PostSelectAction = PostSelectAction.Close;
        menu.Open(player);
    }

    public void CreateCategoryMenu(IMenu menu, bool IsInventory)
    {
        int count = 0;
        foreach(var category in CategoryList)
        {
            count++;
            menu.AddMenuOption(category.Value, (player, _) =>
            {
                if(!IsInventory) OpenCategoryMenu(player, category.Key);
                else OpenCategoryInventoryMenu(player, category.Key);
            });
        }

        if(count == 0) menu.AddMenuOption(Localizer["Menu_CategoriesItemsNotFound"], null!, true);
    }

    public void OpenCategoryMenu(CCSPlayerController player, string CategoryName)
    {
        //StringExtensions.ReplaceColorTags(Localizer["Menu_ChooseItem"])
        var menu = CreateMenu(Localizer["Menu_ChooseItem"]);

        foreach(var Item in ItemsList)
        {
            if(Item.Category == CategoryName)
            {
                string translate = Item.ItemName;
                menu.AddMenuOption(translate, (player, _) => OnChooseItem(player, translate, Item.UniqueName));
            }
        }
        
        menu.Open(player);
    }
    public void OpenCategoryInventoryMenu(CCSPlayerController player, string CategoryName)
    {
        var menu = CreateMenu(Localizer["Menu_ChooseItemInventory"]);

        foreach(var il in playerInfo[player.Slot].ItemList)
        {
            var info = ItemsList.Find(x => x.ItemID == il.item_id);
            if(info != null)
            {
                if(info.Category == CategoryName)
                {
                    string translate = info.ItemName;
                    menu.AddMenuOption(translate, (player, _) => OnChooseItem(player, translate, info.UniqueName));
                }
            }
        }

        menu.Open(player);
    }
    public void OnChooseItem(CCSPlayerController player, string ItemName, string UniqueName)
    {
        var Item = ItemsList.Find(x => x.UniqueName == UniqueName);

        if(Item == null)
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["ItemNotFound", ItemName]));
            return;
        }

        int itemid = Item.ItemID;

        // Список предметов
        var list = playerInfo[player.Slot].ItemList.Find(x => x.item_id == itemid);
        // Список состояний предметов
        var state = playerInfo[player.Slot].ItemStates.Find(x => x.ItemID == itemid);

        if(!IsItemFinite(Item) && list != null && state == null)
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["StateItemNotFound", ItemName]));
            return;
        }

        var menu = CreateMenu(ItemName);

        menu.AddMenuOption(Localizer["Menu_ChooseItemPrice", Item.BuyPrice], null!, true);
        if(Item.Count <= -1)
        {
            if(list == null)
            {
                var timeSpan = TimeSpan.FromSeconds(Item.Duration);
                menu.AddMenuOption(Localizer["Menu_ChooseItemTimeOfActive", Item.Duration == 0 ? Localizer["Menu_ChooseItemTimeOfActive_Forever"] : Localizer["Menu_ChooseItemTimeOfActive_Other", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes]], null!, true);
            }
            else
            {
                var timeSpan = TimeSpan.FromSeconds(list.timeleft);
                menu.AddMenuOption(Localizer["Menu_ChooseItemTimeOfActive", list.timeleft == 0 ? Localizer["Menu_ChooseItemTimeOfActive_Forever"] : Localizer["Menu_ChooseItemTimeOfActive_Other", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes]], null!, true);
            }
        }
        else
        {
            menu.AddMenuOption(Localizer["Menu_ChooseItemCount", Item.Count], null!, true);
            menu.AddMenuOption(Localizer["Menu_ChooseItemYourCount", list == null ? 0 : list.count], null!, true);
        }

        if(Item.Duration >= 0)
        {
            menu.AddMenuOption(list == null ? Item.BuyPrice > GetClientCredits(player) ? Localizer["Menu_ChooseItemBuyNoMoney"] : Localizer["Menu_ChooseItemBuy"] :
            state!.State == 1 ? Localizer["Menu_ChooseItemStateOFF"] : Localizer["Menu_ChooseItemStateON"], (player, _) =>
            {
                if(list == null) OnChooseBuy(player, ItemName, UniqueName, itemid, Item, list);
                else OnChooseAction(player, ItemName, UniqueName, itemid, Item.Category, list);
            }, list == null && Item.BuyPrice > GetClientCredits(player));

            if(list == null)
            {
                var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == itemid);
                menu.AddMenuOption(CallbackList != null && CallbackList.OnClientPreview != null ? Localizer["Menu_ChooseItemPreview"] : Localizer["Menu_ChooseItemPreviewUnavailable"], (player, _) =>
                {
                    OnChoosePreview(player, itemid, UniqueName, Item.Category);
                }, CallbackList == null || CallbackList.OnClientPreview == null);
            }
        }
        else if(Item.Count > 0)
        {
            menu.AddMenuOption(Item.BuyPrice > GetClientCredits(player) ? Localizer["Menu_ChooseItemBuyNoMoney"] : Localizer["Menu_ChooseItemBuy"], (player, _) =>
            {
                OnChooseBuy(player, ItemName, UniqueName, itemid, Item, list);
            }, Item.BuyPrice > GetClientCredits(player));
            if(list != null)
            {
                menu.AddMenuOption(Localizer["Menu_ChooseItemUSE"], (player, _) =>
                {
                    OnChooseAction(player, ItemName, UniqueName, itemid, Item.Category, list);
                });
            }
        }
        menu.AddMenuOption(Localizer["Menu_ChooseItemSell", Item.SellPrice], (player, _) =>
        {
            OnChooseSell(player, ItemName, UniqueName, itemid, Item.SellPrice, list);
        }, list == null);

        menu.Open(player);
    }
    public void OnChoosePreview(CCSPlayerController player, int ItemID, string UniqueName, string CategoryName)
    {
        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
        if(CallbackList != null && CallbackList.OnClientPreview != null) 
            CallbackList.OnClientPreview.Invoke(player, ItemID, UniqueName, CategoryName);

        _api!.OnClientPreview(player, ItemID, UniqueName, CategoryName);
    }
    public void OnChooseBuy(CCSPlayerController player, string ItemName, string UniqueName, int ItemID, Items Item, ItemInfo? playerList)
    {
        if(Item.BuyPrice > GetClientCredits(player))
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["NotEnoughMoney"]));
            return;
        }

        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
        if(CallbackList != null && CallbackList.OnBuyItem != null) 
            if(CallbackList.OnBuyItem.Invoke(player, ItemID, Item.Category, UniqueName, Item.BuyPrice, Item.SellPrice, Item.Duration, Item.Count) != HookResult.Continue)
                return;
        _api!.OnClientBuyItem(player, ItemID, Item.Category, UniqueName, Item.BuyPrice, Item.SellPrice, Item.Duration, Item.Count);

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    if(playerList == null || playerList.count < 1)
                    {
                        await connection.ExecuteAsync(@"INSERT INTO `shop_boughts` (`player_id`, `item_id`, `count`, `duration`, `timeleft`, `buy_price`, `sell_price`, `buy_time`) VALUES 
                                                (@playerID, @itemID, @count, @duration, @timeleft, @buyPrice, @sellPrice, @buyTime);", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID,
                            count = Item.Count,
                            duration = Item.Duration,
                            timeleft = Item.Duration,
                            buyPrice = Item.BuyPrice,
                            sellPrice = Item.SellPrice,
                            buyTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                        });
                        playerInfo[player.Slot].ItemList.Add(new ItemInfo( ItemID, Item.Count, Item.Duration, Item.Duration, Item.BuyPrice,
                                                    Item.SellPrice, (int)DateTimeOffset.Now.ToUnixTimeSeconds() ));
                    }
                    else
                    {
                        int index = playerInfo[player.Slot].ItemList.IndexOf(playerList);
                        int new_count = playerInfo[player.Slot].ItemList[index].count += 1;

                        await connection.ExecuteAsync("UPDATE `shop_boughts` SET `count` = @Count WHERE `player_id` = @playerID AND `item_id` = @itemID;", new
                        {
                            Count = new_count,
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });
                    }

                    if(playerList == null && Item.Count <= -1) 
                    {
                        if(Item.Duration > 0)
                        {
                            Server.NextFrame(() =>
                            {
                                var timer = AddTimer(Item.Duration, () => TimerDeleteTimeleftItem(player, playerInfo[player.Slot].ItemList.Find(x => x.item_id == ItemID)!));
                                playerInfo[player.Slot].ItemTimeleft.Add(new ItemTimeleft( ItemID, timer ));
                            });
                        }

                        await connection.ExecuteAsync("INSERT INTO `shop_toggles` (`player_id`, `item_id`, `state`) VALUES (@playerID, @itemID, '1');", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });
                        playerInfo[player.Slot].ItemStates.Add(new ItemStates( ItemID, 1 ));
                        DequipAllItemsOnCategory(player, Item.Category, ItemID);
                    }

                    Server.NextFrame(() => {
                        TakeClientCredits(player, Item.BuyPrice, IShopApi.WhoChangeCredits.ByBuyOrSell);
                        OnChooseItem(player, ItemName, UniqueName);
                        player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["YouBuyItem", ItemName, Item.BuyPrice]));
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{OnChooseBuy} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    public void OnChooseAction(CCSPlayerController player, string ItemName, string UniqueName, int ItemID, string Category, ItemInfo playerList)
    {
        // state item
        var Itemlist = ItemsList.Find(x => x.ItemID == ItemID);
        int Index = playerInfo[player.Slot].ItemStates.FindIndex(x => x.ItemID == ItemID)!;
        int NewState = 0;

        // count item
        int index = playerInfo[player.Slot].ItemList.IndexOf(playerList);
        int new_count = 0;

        if(playerList.count <= -1)
        {
            var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
            NewState = playerInfo[player.Slot].ItemStates[Index].State == 0 ? 1 : 0;
            if(CallbackList != null && CallbackList.OnToggleItem != null) 
                if(CallbackList.OnToggleItem.Invoke(player, ItemID, UniqueName, NewState) != HookResult.Continue) 
                    return;
            _api!.OnClientToggleItem(player, ItemID, UniqueName, NewState);
        }
        else
        {
            var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
            new_count = playerInfo[player.Slot].ItemList[index].count -= 1;
            if(CallbackList != null && CallbackList.OnUseItem != null) 
                if(CallbackList.OnUseItem.Invoke(player, ItemID, UniqueName, new_count) != HookResult.Continue)
                    return;
            _api!.OnClientUseItem(player, ItemID, UniqueName, new_count);
        }

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    if(playerList.count <= -1)
                    {
                        // TODO: Проверить эту функцию
                        if(playerList.duration > 0 && Itemlist != null)
                        {
                            int timeleft = playerList.duration+playerList.buy_time-(int)DateTimeOffset.Now.ToUnixTimeSeconds();
                            if(timeleft > 0)
                            {
                                await connection.ExecuteAsync("UPDATE `shop_boughts` SET `timeleft` = @Timeleft WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    Timeleft = timeleft,
                                    playerID = playerInfo[player.Slot].DatabaseID,
                                    itemID = ItemID
                                });

                                if(NewState == 1)
                                {
                                    Server.NextFrame(() =>
                                    {
                                        var timer = AddTimer(timeleft, () => TimerDeleteTimeleftItem(player, playerList));
                                        playerInfo[player.Slot].ItemTimeleft.Add(new ItemTimeleft( ItemID, timer ));
                                    });
                                }
                                else
                                {
                                    int ind = playerInfo[player.Slot].ItemList.FindIndex(x => x.item_id == ItemID);
                                    playerInfo[player.Slot].ItemList[ind].timeleft = timeleft;
                                    Server.NextFrame(() =>
                                    {
                                        ind = playerInfo[player.Slot].ItemTimeleft.FindIndex(x => x.ItemID == ItemID);
                                        playerInfo[player.Slot].ItemTimeleft[ind].TimeleftTimer.Kill();
                                        playerInfo[player.Slot].ItemTimeleft.RemoveAt(ind);
                                    });
                                }
                            }
                            else
                            {
                                await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[player.Slot].DatabaseID,
                                    itemID = ItemID
                                });

                                Server.NextFrame(() =>
                                {
                                    int ind = playerInfo[player.Slot].ItemTimeleft.FindIndex(x => x.ItemID == ItemID);
                                    playerInfo[player.Slot].ItemTimeleft[ind].TimeleftTimer.Kill();
                                    playerInfo[player.Slot].ItemTimeleft.RemoveAll(x => x.ItemID == playerList.item_id);
                                });

                                playerInfo[player.Slot].ItemList.Remove(playerList);

                                await connection.ExecuteAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[player.Slot].DatabaseID,
                                    itemID = ItemID
                                });

                                playerInfo[player.Slot].ItemStates.RemoveAll(x => x.ItemID == playerList.item_id);
                            }
                        }

                        if(NewState == 1) DequipAllItemsOnCategory(player, Category, ItemID);

                        playerInfo[player.Slot].ItemStates[Index].State = NewState;

                        await connection.ExecuteAsync("UPDATE `shop_toggles` SET `state` = @State WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            State = NewState,
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });
                        
                        Server.NextFrame(() => 
                        {
                            OnChooseItem(player, ItemName, UniqueName);
                        });
                    }
                    else
                    {
                        if(new_count > 0)
                        {
                            await connection.ExecuteAsync("UPDATE `shop_boughts` SET `count` = @Count WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                            {
                                Count = new_count,
                                playerID = playerInfo[player.Slot].DatabaseID,
                                itemID = ItemID
                            });
                        }
                        else
                        {
                            await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                            {
                                playerID = playerInfo[player.Slot].DatabaseID,
                                itemID = ItemID
                            });

                            playerInfo[player.Slot].ItemList.Remove(playerList);
                        }

                        Server.NextFrame(() => 
                        {
                            OnChooseItem(player, ItemName, UniqueName);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{OnChooseAction} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    public void DequipAllItemsOnCategory(CCSPlayerController player, string category, int newItemID)
    {
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    foreach(var list in playerInfo[player.Slot].ItemStates.FindAll(x => x.State == 1 && x.ItemID != newItemID))
                    {
                        int oldIndex = playerInfo[player.Slot].ItemStates.IndexOf(list);
                        if(oldIndex != -1)
                        {
                            var oldItem = ItemsList.Find(x => x.Category == category && x.ItemID == playerInfo[player.Slot].ItemStates[oldIndex].ItemID);
                            if(oldItem != null)
                            {
                                playerInfo[player.Slot].ItemStates[oldIndex].State = 0;

                                Server.NextFrame(() => 
                                {
                                    var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == oldItem.ItemID);
                                    if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, oldItem.ItemID, oldItem.UniqueName, 0);
                                    _api!.OnClientToggleItem(player, oldItem.ItemID, oldItem.UniqueName, 0);
                                });

                                await connection.ExecuteAsync("UPDATE `shop_toggles` SET `state` = '0' WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[player.Slot].DatabaseID,
                                    itemID = oldItem.ItemID
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{DequipAllItemsOnCategory} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    public void OnChooseSell(CCSPlayerController player, string ItemName, string UniqueName, int ItemID, int SellPrice, ItemInfo? playerList)
    {
        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
        if(CallbackList != null && CallbackList.OnSellItem != null) 
            if(CallbackList.OnSellItem.Invoke(player, ItemID, UniqueName, SellPrice) != HookResult.Continue)
                return;
        _api!.OnClientSellItem(player, ItemID, UniqueName, SellPrice);
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    int new_count = 0;

                    // Если это поштучный предмет то забираем одну штуку
                    if(playerList != null)
                    {
                        int index = playerInfo[player.Slot].ItemList.IndexOf(playerList);
                        new_count = playerInfo[player.Slot].ItemList[index].count -= 1;
                    }
                    // Если это поштучный предмет и его кол-во 0 или это временный предмет то удаляем его с базы
                    if(playerList == null || new_count < 1)
                    {
                        await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });

                        // Если предмет временный то удаляем состояние с базы
                        if(playerList == null)
                        {
                            await connection.ExecuteAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                            {
                                playerID = playerInfo[player.Slot].DatabaseID,
                                itemID = ItemID
                            });
                        }
                    }
                    else if(playerList != null) // Если это поштучный предмет и его больше 0, то просто обновляем его кол-во
                    {
                        await connection.ExecuteAsync("UPDATE `shop_boughts` SET `count` = @Count WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            Count = new_count,
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });
                    }

                    Server.NextFrame(() => {
                        AddClientCredits(player, Convert.ToInt32(SellPrice), IShopApi.WhoChangeCredits.ByBuyOrSell);
                        playerInfo[player.Slot].ItemList.RemoveAll(x => x.item_id == ItemID);

                        OnChooseItem(player, ItemName, UniqueName);
                        player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["YouSellItem", ItemName, SellPrice]));
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{OnChooseSell} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    #endregion

    public void OnClientAuthorized(int playerSlot, SteamID steamID)
	{
		CCSPlayerController? player = Utilities.GetPlayerFromSlot(playerSlot);

		if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
			return;

        string nickname = player.PlayerName;
        string steamid = steamID.SteamId2;
        transPlayer[playerSlot] = null!;
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    // Проверка на наличие игрока в базе
                    var data = await connection.QueryAsync("SELECT `id`, `money` FROM `shop_players` WHERE `auth` = @Auth;", new
                    {
                        Auth = steamid
                    });

                    if(data != null && data.Count() > 0)
                    {
                        foreach(var row in data.ToList())
                        {
                            playerInfo[playerSlot] = new PlayerInformation
                            {
                                SteamID = steamid,
                                Credits = (int)row.money,
                                DatabaseID = (int)row.id,
                                ItemList = new(),
                                ItemStates = new(),
                                ItemTimeleft = new()
                            };
                        }

                        // Обновление информации об игроке
                        await connection.ExecuteAsync("UPDATE `shop_players` SET `name` = @Name WHERE `auth` = @Auth;", new
                        {
                            Auth = steamid,
                            Name = nickname
                        });
                    }
                    else
                    {
                        // Добавление игрока в базу в случае его отсутствия
                        await connection.ExecuteAsync("INSERT INTO `shop_players` (`auth`, `name`, `money`) VALUES (@Auth, @Name, @Money);", new
                        {
                            Auth = steamid,
                            Name = nickname,
                            Money = Config.StartCredits
                        });

                        var id = await connection.QueryFirstOrDefaultAsync<int?>("SELECT `id` FROM `shop_players` WHERE `auth` = @Auth;", new
                        {
                            Auth = steamid
                        });
                        if(id != null)
                        {
                            playerInfo[playerSlot] = new PlayerInformation
                            {
                                SteamID = steamid,
                                Credits = Config.StartCredits,
                                DatabaseID = (int)id,
                                ItemList = new(),
                                ItemStates = new(),
                                ItemTimeleft = new()
                            };
                        }
                    }

                    // Загрузка предметов 
                    data = await connection.QueryAsync("SELECT * FROM `shop_boughts` WHERE `player_id` = @playerID;", new
                    {
                        playerID = playerInfo[playerSlot].DatabaseID
                    });
                    
                    if(data != null && data.Count() > 0)
                    {
                        foreach(var row in data.ToList())
                        {
                            playerInfo[playerSlot].ItemList.Add(new ItemInfo(
                                (int)row.item_id, (int)row.count, (int)row.duration,
                                (int)row.timeleft, (int)row.buy_price, (int)row.sell_price,
                                (int)row.buy_time ));
                        }
                    }

                    var copyPlayerInfo = playerInfo[playerSlot].ItemList.ToList();
                    // Проверка на длительность предмета
                    foreach(var item in copyPlayerInfo)
                    {
                        var Itemlist = ItemsList.Find(x => x.ItemID == item.item_id);
                        if(item.duration > 0 && item.count <= 0 && Itemlist != null)
                        {
                            int timeleft = item.duration+item.buy_time-(int)DateTimeOffset.Now.ToUnixTimeSeconds();
                            if(timeleft > 0)
                            {
                                await connection.ExecuteAsync("UPDATE `shop_boughts` SET `timeleft` = @Timeleft WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    Timeleft = timeleft,
                                    playerID = playerInfo[playerSlot].DatabaseID,
                                    itemID = item.item_id
                                });

                                Server.NextFrame(() => 
                                {
                                    var timer = AddTimer(timeleft, () => TimerDeleteTimeleftItem(player, item));
                                    playerInfo[playerSlot].ItemTimeleft.Add(new ItemTimeleft( item.item_id, timer ));
                                });
                            }
                            else
                            {
                                await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[playerSlot].DatabaseID,
                                    itemID = item.item_id
                                });

                                playerInfo[playerSlot].ItemList.Remove(item);

                                await connection.ExecuteAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[playerSlot].DatabaseID,
                                    itemID = item.item_id
                                });
                            }
                        }
                    }

                    // Загрузка состояний предметов
                    data = await connection.QueryAsync("SELECT * FROM `shop_toggles` WHERE `player_id` = @playerID;", new
                    {
                        playerID = playerInfo[playerSlot].DatabaseID
                    });

                    if(data != null && data.Count() > 0)
                    {
                        foreach(var row in data.ToList())
                        {
                            int itemid = (int)row.item_id;
                            int state = (int)row.state;
                            playerInfo[playerSlot].ItemStates.Add(new ItemStates( itemid, state ));
                            if(state == 1 && playerInfo[playerSlot].ItemList.Find(x => x.item_id == itemid) != null)
                            {
                                var Item = ItemsList.Find(x => x.ItemID == itemid);
                                if(Item != null)
                                {
                                    Server.NextFrame(() => {
                                        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == itemid);
                                        if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, itemid, Item.UniqueName, state);
                                        _api!.OnClientToggleItem(player, itemid, Item.UniqueName, state); 
                                    });
                                }
                            }
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.LogError("{OnClientAuthorized} Failed get info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed get info in database! | " + ex.Message);
            }
        });
	}
    public bool? GetItemState(string uniqueName, CCSPlayerController player)
    {
        if (player == null)
            return null;
        
        if (player.Slot < 0 || player.Slot >= playerInfo.Length || playerInfo[player.Slot] == null)
            return null;
        
        var item = ItemsList.Find(x => x.UniqueName.Equals(uniqueName, StringComparison.OrdinalIgnoreCase));
        if (item == null)
            return null;
        
        var itemState = playerInfo[player.Slot].ItemStates.Find(x => x.ItemID == item.ItemID);
        if (itemState == null)
            return null;

        return itemState.State == 1;
    }
    public void TimerDeleteTimeleftItem(CCSPlayerController player, ItemInfo Item)
    {
        if(!player.IsValid)
            return;

        var Itemlist = ItemsList.Find(x => x.ItemID == Item.item_id)!;
        if(Itemlist != null)
        {
            Server.NextFrame(() => {
                var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == Item.item_id);
                if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, Item.item_id, Itemlist.UniqueName, 0);
                _api!.OnClientToggleItem(player, Item.item_id, Itemlist.UniqueName, 0); 
            });
        }

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                    {
                        playerID = playerInfo[player.Slot].DatabaseID,
                        itemID = Item.item_id
                    });

                    playerInfo[player.Slot].ItemTimeleft.RemoveAll(x => x.ItemID == Item.item_id);
                    playerInfo[player.Slot].ItemList.Remove(Item);

                    await connection.ExecuteAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                    {
                        playerID = playerInfo[player.Slot].DatabaseID,
                        itemID = Item.item_id
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{TimerDeleteTimeleftItem} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    #region Config
    public void OnConfigParsed(ShopConfig config)
	{
		if (config.DatabaseHost.Length < 1 || config.DatabaseName.Length < 1 || config.DatabaseUser.Length < 1)
		{
			throw new Exception("[SHOP] You need to setup Database credentials in config!");
		}

		MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = config.DatabaseHost,
			Database = config.DatabaseName,
			UserID = config.DatabaseUser,
			Password = config.DatabasePassword,
			Port = (uint)config.DatabasePort,
		};

        dbConnectionString = builder.ConnectionString + ";Allow User Variables=True"; // Исправление ошибки Parameter '@' must be defined. To use this as a variable, set 'Allow User Variables=true' in the connection string.

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    connection.Open();

                    await connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS `shop_players` (
                                                        `id` int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                                                        `auth` VARCHAR(32) NOT NULL,
                                                        `name` VARCHAR(64) NOT NULL default 'unknown',
                                                        `money` int NOT NULL default 0
                                                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");

                    await connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS `shop_boughts` (
                                                            `player_id` int NOT NULL,
                                                            `item_id` int NOT NULL,
                                                            `count` int,
                                                            `duration` int NOT NULL,
                                                            `timeleft` int NOT NULL,
                                                            `buy_price` int NOT NULL,
                                                            `sell_price` int NOT NULL,
                                                            `buy_time` int
                                                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");

                    await connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS `shop_toggles` (
                                                            `id` int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                                                            `player_id` int NOT NULL,
                                                            `item_id` int NOT NULL,
                                                            `state` tinyint NOT NULL DEFAULT 0
                                                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");

                    await connection.ExecuteAsync(@"CREATE TABLE IF NOT EXISTS `shop_items` (
                                                            `id` int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                                                            `category` VARCHAR(64) NOT NULL,
                                                            `item` VARCHAR(64) NOT NULL
                                                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{OnConfigParsed} Unable to connect to database! | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Unable to connect to Database! | " + ex.Message);
            }
        });

        foreach(var Commands in config.Commands.Split(";"))
        {
            AddCommand(Commands, "Open main menu shop", (player, _) => OpenShopMenu(player));
        }

		Config = config;
	}
    #endregion

    public async Task<int> AddItemInDB(string Category, string UniqueName, string ItemName, int BuyPrice, int SellPrice, int Duration, int Count)
    {
        if(ItemsList.Find(item => item.UniqueName == UniqueName) != null)
        {
            Logger.LogError($"[SHOP] An item with this unique name ({UniqueName}) already exists in the other category!");
            return -1;
        }

        int item_id;
        try
        {
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                await connection.OpenAsync();
                var id = await connection.QueryFirstOrDefaultAsync<int?>("SELECT `id` FROM `shop_items` WHERE `category` = @category AND `item` = @uniqueName LIMIT 1;", new
                {
                    category = Category,
                    uniqueName = UniqueName
                });
                if(id != null)
                    item_id = (int)id;
                else {
                    await connection.ExecuteAsync("INSERT INTO `shop_items` (`category`, `item`) VALUES (@category, @uniqueName)", new
                    {
                        category = Category,
                        uniqueName = UniqueName
                    });

                    item_id = await connection.QueryFirstOrDefaultAsync<int>("SELECT `id` FROM `shop_items` WHERE `category` = @category AND `item` = @uniqueName LIMIT 1;", new
                    {
                        category = Category,
                        uniqueName = UniqueName
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("{AddItemInDB} Failed send info in database | " + ex.Message);
            Logger.LogDebug(ex.Message);
            throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
        }

        // Я НА ЭТО БЛЯТЬ ПОЛДНЯ УБИЛ ЧТОБЫ ПОФИКСИТЬ ДОБАВЛЕНИЕ ПРЕДМЕТОВ!!!!
        Server.NextFrame(() =>
        {
            ItemsList.Add(new Items( item_id, Category, UniqueName, ItemName, BuyPrice, SellPrice, Duration, Count ));
        });
        return item_id;
    }

    #region Commands

    [CommandHelper(minArgs: 2, usage: "<name/userid/steamid2/#steamid64> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandAddCredits(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["Command_NoAccess"]));
            return;
        }

        if(Convert.ToInt32(commandInfo.GetArg(2)) <= 0)
        {
            CustomReplyToCommand(commandInfo, Localizer["Command_IncorrectMoneyCount"]);
            return;
        }

        if(!commandInfo.GetArg(1).StartsWith("STEAM_") && !commandInfo.GetArg(1).StartsWith("#"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                AddClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)), IShopApi.WhoChangeCredits.ByAdminCommand);
                Server.PrintToChatAll(StringExtensions.ReplaceColorTags(Localizer["Command_AddCredits", player == null ? "Console" : player.PlayerName, commandInfo.GetArg(2), target.PlayerName]));
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1).StartsWith("STEAM_") ? commandInfo.GetArg(1).Replace("STEAM_1", "STEAM_0") : commandInfo.GetArg(1).Replace("#", "");
            var target = Utilities.GetPlayers().FirstOrDefault(x => !x.IsBot && !x.IsHLTV 
                && commandInfo.GetArg(1).StartsWith("STEAM_") ? x.AuthorizedSteamID!.SteamId2 == steamid : Convert.ToString(x.AuthorizedSteamID!.SteamId64) == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                {
                    AddClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)), IShopApi.WhoChangeCredits.ByAdminCommand);
                }
            }
            else
            {
                if(!commandInfo.GetArg(1).StartsWith("STEAM_")) 
                {
                    ulong steam64 = Convert.ToUInt64(steamid);
                    steamid = $"STEAM_0:{(steam64 - 76561197960265728) % 2}:{(steam64 - 76561197960265728) / 2}";
                }

                AddClientCredits(steamid, Convert.ToInt32(commandInfo.GetArg(2)));
            }
        }
	}

    [CommandHelper(minArgs: 2, usage: "<name/userid/steamid2/#steamid64> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
	public void CommandSetCredits(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["Command_NoAccess"]));
            return;
        }

        if(Convert.ToInt32(commandInfo.GetArg(2)) < 0)
        {
            CustomReplyToCommand(commandInfo, Localizer["Command_IncorrectMoneyCount"]);
            return;
        }

        if(!commandInfo.GetArg(1).StartsWith("STEAM_") && !commandInfo.GetArg(1).StartsWith("#"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                SetClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)), IShopApi.WhoChangeCredits.ByAdminCommand);
                Server.PrintToChatAll(StringExtensions.ReplaceColorTags(Localizer["Command_SetCredits", player == null ? "Console" : player.PlayerName, commandInfo.GetArg(2), target.PlayerName]));
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1).StartsWith("STEAM_") ? commandInfo.GetArg(1).Replace("STEAM_1", "STEAM_0") : commandInfo.GetArg(1).Replace("#", "");
            var target = Utilities.GetPlayers().FirstOrDefault(x => !x.IsBot && !x.IsHLTV 
                && commandInfo.GetArg(1).StartsWith("STEAM_") ? x.AuthorizedSteamID!.SteamId2 == steamid : Convert.ToString(x.AuthorizedSteamID!.SteamId64) == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                {
                    SetClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)), IShopApi.WhoChangeCredits.ByAdminCommand);
                }
            }
            else
            {
                if(!commandInfo.GetArg(1).StartsWith("STEAM_")) 
                {
                    ulong steam64 = Convert.ToUInt64(steamid);
                    steamid = $"STEAM_0:{(steam64 - 76561197960265728) % 2}:{(steam64 - 76561197960265728) / 2}";
                }

                SetClientCredits(steamid, Convert.ToInt32(commandInfo.GetArg(2)));
            }
        }
	}

    [CommandHelper(minArgs: 2, usage: "<name/userid/steamid2/#steamid64> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
	public void CommandTakeCredits(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["Command_NoAccess"]));
            return;
        }

        if(Convert.ToInt32(commandInfo.GetArg(2)) <= 0)
        {
            CustomReplyToCommand(commandInfo, Localizer["Command_IncorrectMoneyCount"]);
            return;
        }

        if(!commandInfo.GetArg(1).StartsWith("STEAM_") && !commandInfo.GetArg(1).StartsWith("#"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                TakeClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)), IShopApi.WhoChangeCredits.ByAdminCommand);
                Server.PrintToChatAll(StringExtensions.ReplaceColorTags(Localizer["Command_TakeCredits", player == null ? "Console" : player.PlayerName, commandInfo.GetArg(2), target.PlayerName]));
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1).StartsWith("STEAM_") ? commandInfo.GetArg(1).Replace("STEAM_1", "STEAM_0") : commandInfo.GetArg(1).Replace("#", "");
            var target = Utilities.GetPlayers().FirstOrDefault(x => !x.IsBot && !x.IsHLTV 
                && commandInfo.GetArg(1).StartsWith("STEAM_") ? x.AuthorizedSteamID!.SteamId2 == steamid : Convert.ToString(x.AuthorizedSteamID!.SteamId64) == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                {
                    TakeClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)), IShopApi.WhoChangeCredits.ByAdminCommand);
                }
            }
            else
            {
                if(!commandInfo.GetArg(1).StartsWith("STEAM_")) 
                {
                    ulong steam64 = Convert.ToUInt64(steamid);
                    steamid = $"STEAM_0:{(steam64 - 76561197960265728) % 2}:{(steam64 - 76561197960265728) / 2}";
                }

                TakeClientCredits(steamid, Convert.ToInt32(commandInfo.GetArg(2)));
            }
        }
    }

    [CommandHelper(minArgs: 3, usage: "<name/userid/steamid2/#steamid64> <unique_name> <duration/count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandAddItem(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["Command_NoAccess"]));
            return;
        }

        Items? Item;

        if(commandInfo.GetArg(2).Length == 0 || (Item = ItemsList.Find(x => x.UniqueName == commandInfo.GetArg(2))) == null)
        {
            CustomReplyToCommand(commandInfo, Localizer["Command_IncorrectItemName"]);
            return;
        }

        if(IsItemFinite(Item) && Convert.ToInt32(commandInfo.GetArg(3)) <= 0 || Convert.ToInt32(commandInfo.GetArg(3)) < 0)
        {
            CustomReplyToCommand(commandInfo, Localizer["Command_IncorrectItemCount"]);
            return;
        }

        var customDuration = Convert.ToInt32(commandInfo.GetArg(3));
        if(!commandInfo.GetArg(1).StartsWith("STEAM_") && !commandInfo.GetArg(1).StartsWith("#"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                GivePlayerItem(target, Item, customDuration);
                var timeSpan = TimeSpan.FromSeconds(customDuration);
                if(!IsItemFinite(Item))
                {
                    Server.PrintToChatAll(StringExtensions.ReplaceColorTags(Localizer["Command_AddItem", player == null ? "Console" : player.PlayerName, Item.ItemName,
                    customDuration == 0 ? Localizer["Command_AddItemTimeForever"] : Localizer["Command_AddItemTime", timeSpan.Days, timeSpan.Hours, timeSpan.Minutes], target.PlayerName ]));
                }
                else
                {
                    Server.PrintToChatAll(StringExtensions.ReplaceColorTags(Localizer["Command_AddItem", player == null ? "Console" : player.PlayerName, 
                    Item.ItemName, Localizer["Command_AddItemCount", customDuration], target.PlayerName]));
                }
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1).StartsWith("STEAM_") ? commandInfo.GetArg(1).Replace("STEAM_1", "STEAM_0") : commandInfo.GetArg(1).Replace("#", "");
            var target = Utilities.GetPlayers().FirstOrDefault(x => !x.IsBot && !x.IsHLTV 
                && commandInfo.GetArg(1).StartsWith("STEAM_") ? x.AuthorizedSteamID!.SteamId2 == steamid : Convert.ToString(x.AuthorizedSteamID!.SteamId64) == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                    GivePlayerItem(target, Item, customDuration);
            }
            else
            {
                if(!commandInfo.GetArg(1).StartsWith("STEAM_")) 
                {
                    ulong steam64 = Convert.ToUInt64(steamid);
                    steamid = $"STEAM_0:{(steam64 - 76561197960265728) % 2}:{(steam64 - 76561197960265728) / 2}";
                }

                GivePlayerItem(steamid, Item, customDuration);
            }
        }
	}

    [CommandHelper(minArgs: 2, usage: "<name/userid/steamid2/#steamid64> <unique_name> [count]", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandTakeItem(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["Command_NoAccess"]));
            return;
        }

        Items? Item;

        if(commandInfo.GetArg(2).Length == 0 || (Item = ItemsList.Find(x => x.UniqueName == commandInfo.GetArg(2))) == null)
        {
            CustomReplyToCommand(commandInfo, Localizer["Command_IncorrectItemName"]);
            return;
        }

        if(IsItemFinite(Item) && Convert.ToInt32(commandInfo.GetArg(3)) <= 0)
        {
            CustomReplyToCommand(commandInfo, Localizer["Command_IncorrectItemCount"]);
            return;
        }

        var customCount = Convert.ToInt32(commandInfo.GetArg(3));
        if(!commandInfo.GetArg(1).StartsWith("STEAM_") && !commandInfo.GetArg(1).StartsWith("#"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                TakePlayerItem(target, Item, customCount);
                Server.PrintToChatAll(StringExtensions.ReplaceColorTags(Localizer["Command_TakeItem", player == null ? "Console" : player.PlayerName, Item.ItemName, target.PlayerName]));
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1).StartsWith("STEAM_") ? commandInfo.GetArg(1).Replace("STEAM_1", "STEAM_0") : commandInfo.GetArg(1).Replace("#", "");
            var target = Utilities.GetPlayers().FirstOrDefault(x => !x.IsBot && !x.IsHLTV 
                && commandInfo.GetArg(1).StartsWith("STEAM_") ? x.AuthorizedSteamID!.SteamId2 == steamid : Convert.ToString(x.AuthorizedSteamID!.SteamId64) == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                    TakePlayerItem(target, Item, customCount);
            }
            else
            {
                if(!commandInfo.GetArg(1).StartsWith("STEAM_")) 
                {
                    ulong steam64 = Convert.ToUInt64(steamid);
                    steamid = $"STEAM_0:{(steam64 - 76561197960265728) % 2}:{(steam64 - 76561197960265728) / 2}";
                }

                TakePlayerItem(steamid, Item, customCount);
            }
        }
    }

    public HookResult OnClientSay(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || transPlayer[player.Slot] == null || !transPlayer[player.Slot].IsValid) return HookResult.Continue;

        if (string.IsNullOrWhiteSpace(command.ArgString))
            return HookResult.Handled;

        if(command.ArgString.Contains("cancel"))
        {
            player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["TransferCreditsCanceled"]));
            transPlayer[player.Slot] = null!;
            return HookResult.Handled;
        }

        if(int.TryParse(command.ArgString.Replace("\"", ""), out var CreditsCount))
        {
            if(CreditsCount > 0)
            {
                var menu = CreateMenu(Localizer["Menu_TransferCreditsTitle"]);
                menu.AddMenuOption(Localizer["Menu_TransferCreditsHavePlayer", GetClientCredits(player)], null!, true);
                menu.AddMenuOption(Localizer["Menu_TransferCreditsWillBeSend", CreditsCount], null!, true);
                int PriceSend = CreditsCount * Config.TransCreditsPercent / 100;
                menu.AddMenuOption(Localizer["Menu_TransferCreditsPriceSend", PriceSend], null!, true);
                menu.AddMenuOption(Localizer["Menu_TransferCreditsRemainder", GetClientCredits(player) - (PriceSend + CreditsCount)], null!, true);
                bool HaveCredits = GetClientCredits(player) - (PriceSend + CreditsCount) >= 0;
                var target = transPlayer[player.Slot];

                menu.AddMenuOption(HaveCredits ? Localizer["Menu_TransferCreditsConfirm"] : Localizer["Menu_TransferCreditsConfirmNoMoney", (GetClientCredits(player) - (PriceSend + CreditsCount)) * -1], (player, _) => 
                {
                    if(target == null || !target.IsValid)
                    {
                        player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["PlayerNotAvailable"]));
                        return;
                    }

                    HaveCredits = GetClientCredits(player) - (PriceSend + CreditsCount) >= 0;
                    if(!HaveCredits)
                    {
                        player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["NotEnoughMoney"]));
                        return;
                    }

                    TakeClientCredits(player, PriceSend + CreditsCount, IShopApi.WhoChangeCredits.ByTransfer);
                    AddClientCredits(target, CreditsCount, IShopApi.WhoChangeCredits.ByTransfer);
                    
                    player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["TransferCreditsSuccessSender", CreditsCount, target.PlayerName]));
                    target.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["TransferCreditsSuccessTarget", CreditsCount, player.PlayerName]));
                }, !HaveCredits);
                transPlayer[player.Slot] = null!;
                menu.PostSelectAction = PostSelectAction.Close;
                menu.Open(player);
            }
            else player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["TransferCreditsIncorrectCreditsCount"]));
        }
        else player.PrintToChat(StringExtensions.ReplaceColorTags(Localizer["TransferCreditsOnlyDigits"]));

        return HookResult.Handled;
    }

    public void CustomReplyToCommand(CommandInfo info, string message)
    {
        if (info.CallingPlayer != null)
        {
            if (info.CallingContext == CommandCallingContext.Console)
            {
                info.CallingPlayer.PrintToConsole(message);
            }
            else
            {
                info.CallingPlayer.PrintToChat(StringExtensions.ReplaceColorTags(message));
            }
        }
        else
        {
            Server.PrintToConsole(message);
        }
    }

    #endregion

    #region Functions

    public int GetClientCredits(CCSPlayerController player)
    {
        var playerinfo = playerInfo[player.Slot];
        if(player == null || player.IsBot || player.IsHLTV || playerinfo == null || playerinfo.DatabaseID == -1) return -1;

        return playerinfo.Credits;
    }
    public async Task<int> GetClientCredits(string steamID)
    {
        try
        {
            await using (var connection = new MySqlConnection(dbConnectionString))
            {
                var money = await connection.QueryFirstOrDefaultAsync<int?>("SELECT `money` FROM `shop_players` WHERE `auth` = @Steam;", new
                {
                    Steam = steamID
                });
                
                if(money != null)
                    return (int)money;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("{GetClientCreditsDB} Failed get info in database | " + ex.Message);
            Logger.LogDebug(ex.Message);
            throw new Exception("[SHOP] Failed get info in database! | " + ex.Message);
        }

        return -1;
    }
    public void SetClientCredits(CCSPlayerController player, int Credits, IShopApi.WhoChangeCredits by_who)
    {
        if(player == null || player.IsBot || player.IsHLTV || playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1) return;

        if(by_who != IShopApi.WhoChangeCredits.IgnoreCallbackHook)
        {
            int? buffer = _api!.OnCreditsSet(player, Credits, by_who);
            if(buffer != null) Credits = (int)buffer!;
        }

        if(Credits < 0) Credits = 0;

        playerInfo[player.Slot].Credits = Credits;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync("UPDATE `shop_players` SET `money` = @Money WHERE `id` = @ID", new
                    {
                        Money = Credits,
                        ID = playerInfo[player.Slot].DatabaseID
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{SetClientCredits} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });

        if(by_who != IShopApi.WhoChangeCredits.IgnoreCallbackHook)
            _api!.OnCreditsSetPost(player, Credits, by_who);
    }

    public void SetClientCredits(string steamID, int Credits)
    {
        if(Credits < 0) Credits = 0;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync("UPDATE `shop_players` SET `money` = @Money WHERE `auth` = @Steam", new
                    {
                        Money = Credits,
                        Steam = steamID
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{SetClientCreditsDB} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    public void AddClientCredits(CCSPlayerController player, int Credits, IShopApi.WhoChangeCredits by_who)
    {
        if(player == null || player.IsBot || player.IsHLTV || playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1) return;

        if(by_who != IShopApi.WhoChangeCredits.IgnoreCallbackHook)
        {
            int? buffer = _api!.OnCreditsAdd(player, Credits, by_who);
            if(buffer != null) Credits = (int)buffer!;
        }

        if(Credits < 0) Credits = 0;

        playerInfo[player.Slot].Credits += Credits;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync("UPDATE `shop_players` SET `money` = `money` + @Money WHERE `id` = @ID", new
                    {
                        Money = Credits,
                        ID = playerInfo[player.Slot].DatabaseID
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{AddClientCredits} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });

        if(by_who != IShopApi.WhoChangeCredits.IgnoreCallbackHook)
            _api!.OnCreditsAddPost(player, Credits, by_who);
    }

    public void AddClientCredits(string steamID, int Credits)
    {
        if(Credits < 0) Credits = 0;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync("UPDATE `shop_players` SET `money` = `money` + @Money WHERE `auth` = @Steam", new
                    {
                        Money = Credits,
                        Steam = steamID
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{AddClientCreditsDB} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    public void TakeClientCredits(CCSPlayerController player, int Credits, IShopApi.WhoChangeCredits by_who)
    {
        if(player == null || player.IsBot || player.IsHLTV || playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1) return;

        if(by_who != IShopApi.WhoChangeCredits.IgnoreCallbackHook)
        {
            int? buffer = _api!.OnCreditsTake(player, Credits, by_who);
            if(buffer != null) Credits = (int)buffer!;
        }

        if(Credits < 0) Credits = 0;

        if(playerInfo[player.Slot].Credits - Credits < 0)
            playerInfo[player.Slot].Credits = 0;
        else
            playerInfo[player.Slot].Credits -= Credits;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync("UPDATE `shop_players` SET `money` = GREATEST(`money` - @Money, 0) WHERE `id` = @ID", new
                    {
                        Money = Credits,
                        ID = playerInfo[player.Slot].DatabaseID
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{TakeClientCredits} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });

        if(by_who != IShopApi.WhoChangeCredits.IgnoreCallbackHook)
            _api!.OnCreditsTakePost(player, Credits, by_who);
    }

    public void TakeClientCredits(string steamID, int Credits)
    {
        if(Credits < 0) Credits = 0;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync("UPDATE `shop_players` SET `money` = GREATEST(`money` - @Money, 0) WHERE `auth` = @Steam", new
                    {
                        Money = Credits,
                        Steam = steamID
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{TakeClientCreditsDB} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });
    }

    public bool GivePlayerItem(CCSPlayerController player, Items Item, int customDuration)
    {
        if(player.IsBot || player.IsHLTV || playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1) return false;

        var playerList = playerInfo[player.Slot].ItemList.Find(x => x.item_id == Item.ItemID);

        if(IsItemFinite(Item) && customDuration <= 0 || !IsItemFinite(Item) && customDuration <= -1) return false;
        
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    
                    // Если предмет не найден у игрока в покупках то добавляет в список
                    if(playerList == null)
                    {
                        await connection.ExecuteAsync(@"INSERT INTO `shop_boughts` (`player_id`, `item_id`, `count`, `duration`, `timeleft`, `buy_price`, `sell_price`, `buy_time`) VALUES 
                                                (@playerID, @itemID, @count, @duration, @timeleft, @buyPrice, @sellPrice, @buyTime);", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = Item.ItemID,
                            count = IsItemFinite(Item) ? customDuration : -1,
                            duration = !IsItemFinite(Item) ? customDuration : -1,
                            timeleft = !IsItemFinite(Item) ? customDuration : -1,
                            buyPrice = Item.BuyPrice,
                            sellPrice = Item.SellPrice,
                            buyTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                        });
                        playerInfo[player.Slot].ItemList.Add(new ItemInfo( Item.ItemID, IsItemFinite(Item) ? customDuration : -1, !IsItemFinite(Item) ? customDuration : -1,
                        !IsItemFinite(Item) ? customDuration : -1, Item.BuyPrice, Item.SellPrice, (int)DateTimeOffset.Now.ToUnixTimeSeconds() ));
                    }
                    else if(IsItemFinite(Item)) // Если это поштучный предмет, и он уже был приобретен, то добавляем нужное кол-во
                    {
                        int index = playerInfo[player.Slot].ItemList.IndexOf(playerList);
                        int new_count = playerInfo[player.Slot].ItemList[index].count += customDuration;

                        await connection.ExecuteAsync("UPDATE `shop_boughts` SET `count` = @Count, `buy_time` = @buyTime WHERE `player_id` = @playerID AND `item_id` = @itemID;", new
                        {
                            Count = new_count,
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = Item.ItemID,
                            buyTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                        });
                    }

                    // Если это не поштучный предмет и не был до этого в покупках, то добавляем переключение функции
                    if(playerList == null && !IsItemFinite(Item)) 
                    {
                        // Создаем таймер в случае если предмет не навсегда
                        if(Item.Duration > 0)
                        {
                            Server.NextFrame(() =>
                            {
                                var timer = AddTimer(Item.Duration, () => TimerDeleteTimeleftItem(player, playerInfo[player.Slot].ItemList.Find(x => x.item_id == Item.ItemID)!));
                                playerInfo[player.Slot].ItemTimeleft.Add(new ItemTimeleft( Item.ItemID, timer ));
                            });
                        }

                        await connection.ExecuteAsync("INSERT INTO `shop_toggles` (`player_id`, `item_id`, `state`) VALUES (@playerID, @itemID, '0');", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = Item.ItemID
                        });
                        playerInfo[player.Slot].ItemStates.Add(new ItemStates( Item.ItemID, 0 ));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{GivePlayerItem} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });

        return true;
    }

    public bool GivePlayerItem(string SteamID, Items Item, int customDuration)
    {
        if(IsItemFinite(Item) && customDuration <= 0 || !IsItemFinite(Item) && customDuration <= -1) return false;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    // Получаем Player ID игрока
                    var playerid = await connection.QueryFirstOrDefaultAsync<int?>("SELECT `id` FROM `shop_players` WHERE `auth` = @Auth;", new
                    {
                        Auth = SteamID
                    });

                    if(playerid != null)
                    {
                        // Проверка на наличие предмета у игрока в базе
                        var data = await connection.QueryAsync("SELECT * FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID;", new
                        {
                            playerID = playerid,
                            item_id = Item.ItemID
                        });

                        bool item_found = data != null && data.Count() > 0;

                        // Если предмет не найден у игрока в покупках то добавляет в список
                        if(!item_found)
                        {
                            await connection.ExecuteAsync(@"INSERT INTO `shop_boughts` (`player_id`, `item_id`, `count`, `duration`, `timeleft`, `buy_price`, `sell_price`, `buy_time`) VALUES 
                                                    (@playerID, @itemID, @count, @duration, @timeleft, @buyPrice, @sellPrice, @buyTime);", new
                            {
                                playerID = playerid,
                                itemID = Item.ItemID,
                                count = IsItemFinite(Item) ? customDuration : -1,
                                duration = !IsItemFinite(Item) ? customDuration : -1,
                                timeleft = !IsItemFinite(Item) ? customDuration : -1,
                                buyPrice = Item.BuyPrice,
                                sellPrice = Item.SellPrice,
                                buyTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                            });
                        }
                        else if(IsItemFinite(Item)) // Если это поштучный предмет, и он уже был приобретен, то добавляем нужное кол-во
                        {
                            await connection.ExecuteAsync("UPDATE `shop_boughts` SET `count` = `count` + @Count, `buy_time` = @buyTime WHERE `player_id` = @playerID AND `item_id` = @itemID;", new
                            {
                                Count = customDuration,
                                playerID = playerid,
                                itemID = Item.ItemID,
                                buyTime = DateTimeOffset.Now.ToUnixTimeSeconds()
                            });
                        }

                        // Если это не поштучный предмет и не был до этого в покупках, то добавляем переключение функции
                        if(!item_found && !IsItemFinite(Item)) 
                        {
                            await connection.ExecuteAsync("INSERT INTO `shop_toggles` (`player_id`, `item_id`, `state`) VALUES (@playerID, @itemID, '0');", new
                            {
                                playerID = playerid,
                                itemID = Item.ItemID
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{GivePlayerItemDB} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });

        return true;
    }

    public bool TakePlayerItem(CCSPlayerController player, Items Item, int customCount)
    {
        if(player.IsBot || player.IsHLTV || playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1) return false;

        var playerList = playerInfo[player.Slot].ItemList.Find(x => x.item_id == Item.ItemID);

        if(playerList == null) return true;

        if(IsItemFinite(Item) && customCount <= 0) return false;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    // Если это не поштучный предмет то удаляем всю информацию с базы и отключаем предмет
                    if(!IsItemFinite(Item))
                    {
                        await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = Item.ItemID
                        });

                        Server.NextFrame(() =>
                        {
                            int ind = playerInfo[player.Slot].ItemTimeleft.FindIndex(x => x.ItemID == Item.ItemID);
                            playerInfo[player.Slot].ItemTimeleft[ind].TimeleftTimer.Kill();
                            playerInfo[player.Slot].ItemTimeleft.RemoveAt(ind);

                            var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == Item.ItemID);
                            if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, Item.ItemID, Item.UniqueName, 0);
                            _api!.OnClientToggleItem(player, Item.ItemID, Item.UniqueName, 0); 
                        });

                        await connection.ExecuteAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = Item.ItemID
                        });

                        playerInfo[player.Slot].ItemStates.RemoveAll(x => x.ItemID == Item.ItemID);
                        // Удаляем у игрока предмет
                        playerInfo[player.Slot].ItemList.RemoveAll(x => x.item_id == Item.ItemID);
                    }
                    else // Если это поштучный предмет и их количество меньше или равно 0 то удаляем информацию с базы, если нет то просто обновляем кол-во
                    {
                        if(customCount <= 0) return;

                        int index = playerInfo[player.Slot].ItemList.FindIndex(x => x.item_id == Item.ItemID);
                        if(index != -1)
                        {
                            int new_count = playerInfo[player.Slot].ItemList[index].count -= customCount;

                            if(new_count > 0)
                            {
                                await connection.ExecuteAsync("UPDATE `shop_boughts` SET `count` = @Count WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    Count = new_count,
                                    playerID = playerInfo[player.Slot].DatabaseID,
                                    itemID = Item.ItemID
                                });
                            }
                            else
                            {
                                await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[player.Slot].DatabaseID,
                                    itemID = Item.ItemID
                                });

                                // Удаляем у игрока предмет
                                playerInfo[player.Slot].ItemList.RemoveAll(x => x.item_id == Item.ItemID);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{TakePlayerItem} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });

        return true;
    }

    public bool TakePlayerItem(string steamid, Items Item, int customCount)
    {
        if(IsItemFinite(Item) && customCount <= 0) return false;
        
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    // Получаем Player ID игрока
                    var playerid = await connection.QueryFirstOrDefaultAsync<int?>("SELECT `id` FROM `shop_players` WHERE `auth` = @Auth;", new
                    {
                        Auth = steamid
                    });

                    if(playerid != null)
                    {
                        // Проверка на наличие предмета у игрока в базе
                        var data = await connection.QueryAsync("SELECT * FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID;", new
                        {
                            playerID = playerid,
                            item_id = Item.ItemID
                        });

                        if(data == null || data.Count() == 0) return;

                        // Если это не поштучный предмет то удаляем всю информацию с базы и отключаем предмет
                        if(!IsItemFinite(Item))
                        {
                            await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                            {
                                playerID = playerid,
                                itemID = Item.ItemID
                            });

                            await connection.ExecuteAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                            {
                                playerID = playerid,
                                itemID = Item.ItemID
                            });
                        }
                        else // Если это поштучный предмет и их количество меньше или равно 0 то удаляем информацию с базы
                        {
                            var count = await connection.QueryFirstOrDefaultAsync<int>("SELECT `count` FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID;", new
                            {
                                playerID = playerid,
                                itemID = Item.ItemID
                            });

                            if(count - customCount > 0)
                            {
                                await connection.ExecuteAsync("UPDATE `shop_boughts` SET `count` = @Count WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    Count = count - customCount,
                                    playerID = playerid,
                                    itemID = Item.ItemID
                                });
                            }
                            else
                            {
                                await connection.ExecuteAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerid,
                                    itemID = Item.ItemID
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{TakePlayerItemDB} Failed send info in database | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
            }
        });

        return true;
    }

    public bool IsItemFinite(Items Item)
    {
        if(Item.Count > 0) return true;
        if(Item.Duration >= 0) return false;
        return false;
    }

    #endregion
}

#region Classes
public class Items
{
    public Items(int _ItemID, string _Category, string _UniqueName, string _ItemName, int _BuyPrice, int _SellPrice, int _Duration, int _Count)
    {
        ItemID = _ItemID;
        Category = _Category;
        UniqueName = _UniqueName;
        ItemName = _ItemName;
        BuyPrice = _BuyPrice;
        SellPrice = _SellPrice;
        Duration = _Duration;
        Count = _Count;
    }
    public int ItemID { get; set; } 
    public string Category { get; set; } 
    public string UniqueName { get; set; }
    public string ItemName { get; set; } 
    public int BuyPrice { get; set; }
    public int SellPrice { get; set; }
    public int Duration { get; set; }
    public int Count { get; set; }
};
public class ItemStates
{
    public ItemStates(int _ItemID, int _State)
    {
        ItemID = _ItemID;
        State = _State;
    }
    public int ItemID { get; set; }
    public int State { get; set; }
}
public class ItemInfo
{
    public ItemInfo(int _item_id, int _count, int _duration, int _timeleft, int _buy_price, int _sell_price, int _buy_time)
    {
        item_id = _item_id;
        count = _count;
        duration = _duration;
        timeleft = _timeleft;
        buy_price = _buy_price;
        sell_price = _sell_price;
        buy_time = _buy_time;
    }

    public int item_id { get; set; }
    public int count { get; set; }
    public int duration { get; set; }
    public int timeleft { get; set; } 
    public int buy_price { get; set; }
    public int sell_price { get; set; }
    public int buy_time { get; set; }
}
public class ItemTimeleft
{
    public ItemTimeleft(int _ItemID, Timer _TimeleftTimer)
    {
        ItemID = _ItemID;
        TimeleftTimer = _TimeleftTimer;
    }
    public int ItemID { get; set; }
    public Timer TimeleftTimer { get; set; }
}
public class PlayerInformation
{
    public required string SteamID { get; set; }
    public int DatabaseID { get; set; } = -1;
    public int Credits { get; set; }
    public required List<ItemInfo> ItemList { get; set; }
    public required List<ItemStates> ItemStates { get; set; }
    public required List<ItemTimeleft> ItemTimeleft { get; set; }
}

#endregion