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

namespace Shop;
public class Shop : BasePlugin, IPluginConfig<ShopConfig>
{
    public override string ModuleName => "Shop Core";
    public override string ModuleAuthor => "Ganter1234";
    public override string ModuleVersion => "1.7";
    public ShopConfig Config { get; set; } = new();
    public PlayerInformation[] playerInfo = new PlayerInformation[65];
    public List<Items> ItemsList = new();
    public Dictionary<string, string> CategoryList = new();
    public string dbConnectionString = string.Empty;
    public CCSPlayerController[] transPlayer = new CCSPlayerController[65];
    private Api.ApiShop? _api;
    public override void Load(bool hotReload)
    {
        _api = new Api.ApiShop(this); 
        Capabilities.RegisterPluginCapability(IShopApi.Capability, () => _api);

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
        AddCommand("css_shop", "", (player, _) => OpenShopMenu(player));
        AddCommand("css_add_credits", "", CommandAddCredits);
		AddCommand("css_set_credits", "", CommandSetCredits);
		AddCommand("css_take_credits", "", CommandTakeCredits);
        //AddCommand("css_add_item", "", CommandAddItem);
		//AddCommand("css_take_item", "", CommandTakeItem);
    }

    #region Menus
    public IMenu CreateMenu(string title)
    {
        if(Config.UseCenterMenu == false)
        {
            ChatMenu menu = new ChatMenu(title);
            menu.ExitButton = true;
            return menu;
        }
        else
        {
            CenterHtmlMenu menu = new CenterHtmlMenu(title, this);
            return menu;
        }
    }
    public void OpenShopMenu(CCSPlayerController? player)
    {
        if(player == null) return;

        if(playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1)
        {
            player.PrintToChat("Ваши данные загружаются! Пожалуйста подождите..."); // Localizer["YourDataLoad"]
            return;
        }

        var menu = CreateMenu($"Магазин | Кредитов: {GetClientCredits(player)}");
        menu.AddMenuOption("Купить", (player, _) => OpenBuyMenu(player));
        menu.AddMenuOption("Инвентарь", (player, _) => OpenInventoryMenu(player));
        menu.AddMenuOption("Функции", (player, _) => OpenFunctionMenu(player));
        menu.Open(player);
    }

    public void OpenBuyMenu(CCSPlayerController player)
    {
        var menu = CreateMenu($"Категории:");
        CreateCategoryMenu(menu, false);
        menu.Open(player);
    }

    public void OpenInventoryMenu(CCSPlayerController player)
    {
        var menu = CreateMenu($"Категории:");
        CreateCategoryMenu(menu, true);
        menu.Open(player);
    }

    public void OpenFunctionMenu(CCSPlayerController player)
    {
        var menu = CreateMenu($"Функции:");
        menu.AddMenuOption($"Передача кредитов {(Config.TransCreditsPercent == -1 ? "[Выключена]" : Config.TransCreditsPercent == 0 ? "" : $"[Комиссия: {Config.TransCreditsPercent}%]")}", (player, _) => OpenTransferMenu(player), Config.TransCreditsPercent == -1);
        menu.Open(player);
    }
    public void OpenTransferMenu(CCSPlayerController player)
    {
        var menu = CreateMenu($"Выберите игрока:");

        foreach (var players in Utilities.GetPlayers().Where(x => !x.IsBot && !x.IsHLTV && x.IsValid && x.Connected == PlayerConnectedState.PlayerConnected && x != player))
        {
            menu.AddMenuOption($"{players.PlayerName} [{GetClientCredits(players)}]", (player, _) =>
            {
                if(players == null || !players.IsValid) 
                {
                    player.PrintToChat($"Игрок недоступен!");
                    return;
                }

                transPlayer[player.Slot] = players;
                player.PrintToChat($"Введите в чате количество кредитов для отправки или \"cancel\" для отмены!");
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

        if(count == 0) menu.AddMenuOption("К сожалению нету доступных товаров!", null!, true);
    }

    public void OpenCategoryMenu(CCSPlayerController player, string CategoryName)
    {
        var menu = CreateMenu($"Выберите товар:");

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
        var menu = CreateMenu($"Выберите товар:");

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
            player.PrintToChat($"Предмет {ItemName} не найден!");
            return;
        }

        int itemid = Item.ItemID;

        var list = playerInfo[player.Slot].ItemList.Find(x => x.item_id == itemid);
        var state = playerInfo[player.Slot].ItemStates.Find(x => x.ItemID == itemid);

        if(list != null && state == null)
        {
            player.PrintToChat($"Состояние предмета {ItemName} не найдено!");
            return;
        }

        var menu = CreateMenu(ItemName);

        menu.AddMenuOption($"Цена: {Item.BuyPrice} кредитов", null!, true);
        if(Item.Count <= -1)
        {
            if(list == null)
            {
                var timeSpan = TimeSpan.FromSeconds(Item.Duration);
                menu.AddMenuOption($"Время действия: {(Item.Duration == 0 ? "Навсегда" : $"{timeSpan.Days}д. {timeSpan.Hours}ч. {timeSpan.Minutes}м.")}", null!, true);
            }
            else
            {
                var timeSpan = TimeSpan.FromSeconds(list.timeleft);
                menu.AddMenuOption($"Время действия: {(list.duration == 0 ? "Навсегда" : $"{timeSpan.Days}д. {timeSpan.Hours}ч. {timeSpan.Minutes}м.")}", null!, true);
            }
        }
        else
        {
            menu.AddMenuOption($"Количество: {Item.Count}{'\u2029'}", null!, true);
            menu.AddMenuOption($"У вас: {(list == null ? 0 : list.count)}", null!, true);
        }

        if(Item.Count <= -1)
        {
            menu.AddMenuOption(list == null ? Item.BuyPrice > GetClientCredits(player) ? "Купить [Недостаточно средств]" : "Купить" :
            state!.State == 1 ? "Выключить" : "Включить", (player, _) =>
            {
                if(list == null) OnChooseBuy(player, ItemName, UniqueName, itemid, Item, list);
                else OnChooseAction(player, ItemName, UniqueName, itemid, Item.Category, list);
            }, list == null && Item.BuyPrice > GetClientCredits(player));
        }
        if(list != null && list.count > 0)
        {
            menu.AddMenuOption("Использовать", (player, _) =>
            {
                OnChooseAction(player, ItemName, UniqueName, itemid, Item.Category, list);
            });
        }
        menu.AddMenuOption($"Продать ({Item.SellPrice} кредитов)", (player, _) =>
        {
            OnChooseSell(player, ItemName, UniqueName, itemid, Item.SellPrice, list);
        }, list == null);

        menu.Open(player);
    }
    public void OnChooseBuy(CCSPlayerController player, string ItemName, string UniqueName, int ItemID, Items Item, ItemInfo? playerList)
    {
        if(Item.BuyPrice > GetClientCredits(player))
        {
            player.PrintToChat("Недостаточно средств!");
            return;
        }

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    if(playerList == null || playerList.count < 1)
                    {
                        await connection.QueryAsync(@"INSERT INTO `shop_boughts` (`player_id`, `item_id`, `count`, `duration`, `timeleft`, `buy_price`, `sell_price`, `buy_time`) VALUES 
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

                        await connection.QueryAsync("UPDATE `shop_boughts` SET `count` = @Count WHERE `player_id` = @playerID AND `item_id` = @itemID;", new
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

                        await connection.QueryAsync("INSERT INTO `shop_toggles` (`player_id`, `item_id`, `state`) VALUES (@playerID, @itemID, '1');", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });
                        playerInfo[player.Slot].ItemStates.Add(new ItemStates( ItemID, 1 ));
                        DequipAllItemsOnCategory(player, Item.Category, ItemID);
                    }

                    SetClientCredits(player, GetClientCredits(player) - Item.BuyPrice);

                    Server.NextFrame(() => {
                        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
                        if(CallbackList != null && CallbackList.OnBuyItem != null) CallbackList.OnBuyItem.Invoke(player, ItemID, Item.Category, UniqueName, Item.BuyPrice, Item.SellPrice, Item.Duration, Item.Count);
                        _api!.OnClientBuyItem(player, ItemID, Item.Category, UniqueName, Item.BuyPrice, Item.SellPrice, Item.Duration, Item.Count);
                        OnChooseItem(player, ItemName, UniqueName);
                        player.PrintToChat($"Вы купили предмет \"{ItemName}\" за {Item.BuyPrice} кредитов!");
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
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    if(playerList.count <= -1)
                    {
                        var Itemlist = ItemsList.Find(x => x.ItemID == ItemID);
                        int Index = playerInfo[player.Slot].ItemStates.FindIndex(x => x.ItemID == ItemID)!;
                        int NewState = playerInfo[player.Slot].ItemStates[Index].State == 0 ? 1 : 0;
                        if(playerList.duration > 0 && Itemlist != null)
                        {
                            int timeleft = playerList.duration+playerList.buy_time-(int)DateTimeOffset.Now.ToUnixTimeSeconds();
                            if(timeleft > 0)
                            {
                                await connection.QueryAsync("UPDATE `shop_boughts` SET `timeleft` = @Timeleft WHERE `player_id` = @playerID AND `item_id` = @itemID", new
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
                                        playerInfo[player.Slot].ItemTimeleft.RemoveAll(x => x.ItemID == playerList.item_id);
                                    });
                                }
                            }
                            else
                            {
                                await connection.QueryAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
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

                                await connection.QueryAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[player.Slot].DatabaseID,
                                    itemID = ItemID
                                });
                            }
                        }

                        if(NewState == 1) DequipAllItemsOnCategory(player, Category, ItemID);

                        playerInfo[player.Slot].ItemStates[Index].State = NewState;

                        await connection.QueryAsync("UPDATE `shop_toggles` SET `state` = @State WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            State = NewState,
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });
                        
                        Server.NextFrame(() => 
                        {
                            var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
                            if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, ItemID, UniqueName, NewState);
                            _api!.OnClientToggleItem(player, ItemID, UniqueName, NewState);
                            OnChooseItem(player, ItemName, UniqueName);
                        });
                    }
                    else
                    {
                        int index = playerInfo[player.Slot].ItemList.IndexOf(playerList);
                        int new_count = playerInfo[player.Slot].ItemList[index].count -= 1;

                        await connection.QueryAsync("UPDATE `shop_boughts` SET `count` = @Count WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            Count = new_count,
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });

                        // Переделать под новую функцию OnUseItem
                        Server.NextFrame(() => 
                        {
                            var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
                            //if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, ItemID, UniqueName, NewState);
                            //_api!.OnClientToggleItem(player, ItemID, UniqueName, NewState);
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

                                await connection.QueryAsync("UPDATE `shop_toggles` SET `state` = '0' WHERE `player_id` = @playerID AND `item_id` = @itemID", new
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
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    int new_count = 0;
                    if(playerList != null)
                    {
                        int index = playerInfo[player.Slot].ItemList.IndexOf(playerList);
                        new_count = playerInfo[player.Slot].ItemList[index].count -= 1;
                    }
                    if(playerList == null || new_count < 1)
                    {
                        await connection.QueryAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                        {
                            playerID = playerInfo[player.Slot].DatabaseID,
                            itemID = ItemID
                        });

                        if(playerList == null)
                        {
                            await connection.QueryAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                            {
                                playerID = playerInfo[player.Slot].DatabaseID,
                                itemID = ItemID
                            });
                        }
                    }

                    SetClientCredits(player, GetClientCredits(player)+Convert.ToInt32(SellPrice));

                    Server.NextFrame(() => {
                        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
                        if(CallbackList != null && CallbackList.OnSellItem != null) CallbackList.OnSellItem.Invoke(player, ItemID, UniqueName, SellPrice);
                        _api!.OnClientSellItem(player, ItemID, UniqueName, SellPrice);
                        playerInfo[player.Slot].ItemList.RemoveAll(x => x.item_id == ItemID);
                        OnChooseItem(player, ItemName, UniqueName);
                        player.PrintToChat($"Вы продали предмет \"{ItemName}\" за {SellPrice} кредитов!");
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
                    var data = await connection.QueryAsync("SELECT `id`, `money` FROM `shop_players` WHERE `auth` = @Auth;", new
                    {
                        Auth = steamid
                    });

                    if(data != null)
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

                        await connection.QueryAsync("UPDATE `shop_players` SET `name` = @Name WHERE `auth` = @Auth;", new
                        {
                            Auth = steamid,
                            Name = nickname
                        });
                    }
                    else
                    {
                        await connection.QueryAsync("INSERT INTO `shop_players` (`auth`, `name`) VALUES (@Auth, @Name);", new
                        {
                            Auth = steamid,
                            Name = nickname
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
                                Credits = 0,
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
                    
                    if(data != null)
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
                        if(item.duration > 0 && Itemlist != null)
                        {
                            int timeleft = item.duration+item.buy_time-(int)DateTimeOffset.Now.ToUnixTimeSeconds();
                            if(timeleft > 0)
                            {
                                await connection.QueryAsync("UPDATE `shop_boughts` SET `timeleft` = @Timeleft WHERE `player_id` = @playerID AND `item_id` = @itemID", new
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
                                await connection.QueryAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[playerSlot].DatabaseID,
                                    itemID = item.item_id
                                });

                                playerInfo[playerSlot].ItemList.Remove(item);

                                await connection.QueryAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                                {
                                    playerID = playerInfo[playerSlot].DatabaseID,
                                    itemID = item.item_id
                                });
                            }
                        }
                    }

                    data = await connection.QueryAsync("SELECT * FROM `shop_toggles` WHERE `player_id` = @playerID;", new
                    {
                        playerID = playerInfo[playerSlot].DatabaseID
                    });

                    if(data != null)
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

                    await connection.QueryAsync("DELETE FROM `shop_boughts` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
                    {
                        playerID = playerInfo[player.Slot].DatabaseID,
                        itemID = Item.item_id
                    });

                    playerInfo[player.Slot].ItemTimeleft.RemoveAll(x => x.ItemID == Item.item_id);
                    playerInfo[player.Slot].ItemList.Remove(Item);

                    await connection.QueryAsync("DELETE FROM `shop_toggles` WHERE `player_id` = @playerID AND `item_id` = @itemID", new
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

        CategoryList.Clear();

		MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
		{
			Server = config.DatabaseHost,
			Database = config.DatabaseName,
			UserID = config.DatabaseUser,
			Password = config.DatabasePassword,
			Port = (uint)config.DatabasePort,
		};

        dbConnectionString = builder.ConnectionString;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    connection.Open();

                    string sql = @"CREATE TABLE IF NOT EXISTS `shop_players` (
                                    `id` int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                                    `auth` VARCHAR(32) NOT NULL,
                                    `name` VARCHAR(64) NOT NULL default 'unknown',
                                    `money` int NOT NULL default 0
                                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

                    await connection.ExecuteAsync(sql);

                    //Console.WriteLine("FIRST");

                    sql = @"CREATE TABLE IF NOT EXISTS `shop_boughts` (
                                    `player_id` int NOT NULL,
                                    `item_id` int NOT NULL,
                                    `count` int,
                                    `duration` int NOT NULL,
                                    `timeleft` int NOT NULL,
                                    `buy_price` int NOT NULL,
                                    `sell_price` int NOT NULL,
                                    `buy_time` int
                                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

                    await connection.ExecuteAsync(sql);

                    sql = @"CREATE TABLE IF NOT EXISTS `shop_toggles` (
                                    `id` int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                                    `player_id` int NOT NULL,
                                    `item_id` int NOT NULL,
                                    `state` tinyint NOT NULL DEFAULT 0
                                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

                    await connection.ExecuteAsync(sql);

                    sql = @"CREATE TABLE IF NOT EXISTS `shop_items` (
                                    `id` int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                                    `category` VARCHAR(64) NOT NULL,
                                    `item` VARCHAR(64) NOT NULL
                                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;";

                    await connection.ExecuteAsync(sql);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("{OnConfigParsed} Unable to connect to database! | " + ex.Message);
                Logger.LogDebug(ex.Message);
                throw new Exception("[SHOP] Unable to connect to Database! | " + ex.Message);
            }
        });

		Config = config;
	}
    #endregion

    public async Task<int> AddItemInDB(string Category, string UniqueName, string ItemName, int BuyPrice, int SellPrice, int Duration, int Count)
    {
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
                    await connection.QueryAsync("INSERT INTO `shop_items` (`category`, `item`) VALUES (@category, @uniqueName)", new
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

    [CommandHelper(minArgs: 2, usage: "<name/userid/steamid> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandAddCredits(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat($"У вас нет доступа к этой команде!");
            return;
        }

        if(Convert.ToInt32(commandInfo.GetArg(2)) <= 0)
        {
            commandInfo.ReplyToCommand($"Некорректное количество кредитов!");
            return;
        }

        if(!commandInfo.GetArg(1).StartsWith("STEAM_"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                SetClientCredits(target, GetClientCredits(target) + Convert.ToInt32(commandInfo.GetArg(2)));
                Server.PrintToChatAll($"[Shop] Админ {(player == null ? "Console" : player.PlayerName)} выдал {commandInfo.GetArg(2)} кредитов игроку {target.PlayerName}");
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1);
            var target = Utilities.GetPlayers().FirstOrDefault(x => x.IsBot && x.IsHLTV && x.AuthorizedSteamID!.SteamId2 == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                    SetClientCredits(target, GetClientCredits(target) + Convert.ToInt32(commandInfo.GetArg(2)));
            }
            else
            {
                int money = -1;
                int newCredits = Convert.ToInt32(commandInfo.GetArg(2));
                Task.Run(async () => 
                {
                    if((money = await GetClientCredits(steamid)) != -1)
                        SetClientCredits(steamid, money + newCredits);
                    else
                        Server.NextFrame(() => commandInfo.ReplyToCommand($"Невозможно получить количество денег, возможно неправильный стим айди!") );
                });
            }
        }
	}

    [CommandHelper(minArgs: 2, usage: "<name/userid/steamid> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
	public void CommandSetCredits(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat($"У вас нет доступа к этой команде!");
            return;
        }

        if(Convert.ToInt32(commandInfo.GetArg(2)) < 0)
        {
            commandInfo.ReplyToCommand($"Некорректное количество кредитов!");
            return;
        }

        if(!commandInfo.GetArg(1).StartsWith("STEAM_"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                SetClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)));
                Server.PrintToChatAll($"[Shop] Админ {(player == null ? "Console" : player.PlayerName)} установил {Convert.ToInt32(commandInfo.GetArg(2))} кредитов игроку {target.PlayerName}");
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1);
            var target = Utilities.GetPlayers().FirstOrDefault(x => x.IsBot && x.IsHLTV && x.AuthorizedSteamID!.SteamId2 == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                    SetClientCredits(target, GetClientCredits(target) + Convert.ToInt32(commandInfo.GetArg(2)));
            }
            else
            {
                SetClientCredits(steamid, Convert.ToInt32(commandInfo.GetArg(2)));
            }
        }
	}

    [CommandHelper(minArgs: 2, usage: "<name/userid> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
	public void CommandTakeCredits(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat($"У вас нет доступа к этой команде!");
            return;
        }

        if(Convert.ToInt32(commandInfo.GetArg(2)) <= 0)
        {
            commandInfo.ReplyToCommand($"Некорректное количество кредитов!");
            return;
        }

        if(!commandInfo.GetArg(1).StartsWith("STEAM_"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                SetClientCredits(target, GetClientCredits(target) - Convert.ToInt32(commandInfo.GetArg(2)));
                Server.PrintToChatAll($"[Shop] Админ {(player == null ? "Console" : player.PlayerName)} отобрал {Convert.ToInt32(commandInfo.GetArg(2))} кредитов у игрока {target.PlayerName}");
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1);
            var target = Utilities.GetPlayers().FirstOrDefault(x => x.IsBot && x.IsHLTV && x.AuthorizedSteamID!.SteamId2 == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                    SetClientCredits(target, GetClientCredits(target) - Convert.ToInt32(commandInfo.GetArg(2)));
            }
            else
            {
                int money = -1;
                int newCredits = Convert.ToInt32(commandInfo.GetArg(2));
                Task.Run(async () => 
                {
                    if((money = await GetClientCredits(steamid)) != -1)
                        SetClientCredits(steamid, money - newCredits);
                    else
                        Server.NextFrame(() => commandInfo.ReplyToCommand($"Невозможно получить количество денег, возможно неправильный стим айди!") );
                });
            }
        }
    }

    /*[CommandHelper(minArgs: 3, usage: "<name/userid/steamid> <unique_name> <duration/count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
    public void CommandAddItem(CCSPlayerController? player, CommandInfo commandInfo)
	{
        if(player != null && !AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            player.PrintToChat($"У вас нет доступа к этой команде!");
            return;
        }

        Items? Item;

        if(commandInfo.GetArg(2).Length == 0 || (Item = ItemsList.Find(x => x.UniqueName == commandInfo.GetArg(2))) == null)
        {
            commandInfo.ReplyToCommand($"Некорректное название предмета!");
            return;
        }

        if(!commandInfo.GetArg(1).StartsWith("STEAM_"))
        {
            var targets = commandInfo.GetArgTargetResult(1);

            foreach(var target in targets)
            {
                if(target == null || playerInfo[target.Slot] == null) continue;

                SetClientCredits(target, GetClientCredits(target) + Convert.ToInt32(commandInfo.GetArg(2)));
                Server.PrintToChatAll($"[Shop] Админ {(player == null ? "Console" : player.PlayerName)} выдал {commandInfo.GetArg(2)} кредитов игроку {target.PlayerName}");
            }
        }
        else
        {
            string steamid = commandInfo.GetArg(1);
            var target = Utilities.GetPlayers().FirstOrDefault(x => x.IsBot && x.IsHLTV && x.AuthorizedSteamID!.SteamId2 == steamid);
            if(target != null)
            {
                var playerinfo = playerInfo[target.Slot];
                if(playerinfo != null && playerinfo.DatabaseID != -1)
                    SetClientCredits(target, GetClientCredits(target) + Convert.ToInt32(commandInfo.GetArg(2)));
            }
            else
            {
                int money = -1;
                int newCredits = Convert.ToInt32(commandInfo.GetArg(2));
                Task.Run(async () => 
                {
                    if((money = await GetClientCredits(steamid)) != -1)
                        SetClientCredits(steamid, money + newCredits);
                    else
                        Server.NextFrame(() => commandInfo.ReplyToCommand($"Невозможно получить количество денег, возможно неправильный стим айди!") );
                });
            }
        }
	}*/

    public HookResult OnClientSay(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || transPlayer[player.Slot] == null || !transPlayer[player.Slot].IsValid) return HookResult.Continue;

        if (string.IsNullOrWhiteSpace(command.ArgString))
            return HookResult.Handled;

        if(command.ArgString.Contains("cancel"))
        {
            player.PrintToChat("Вы отменили передачу кредитов!");
            transPlayer[player.Slot] = null!;
            return HookResult.Handled;
        }

        if(int.TryParse(command.ArgString.Replace("\"", ""), out var CreditsCount))
        {
            ChatMenu menu = new ChatMenu($"Передача кредитов");
            menu.AddMenuOption($"Количество кредитов: {GetClientCredits(player)}", null!, true);
            menu.AddMenuOption($"Будет отправлено: {CreditsCount}", null!, true);
            int PriceSend = CreditsCount * Config.TransCreditsPercent / 100;
            menu.AddMenuOption($"Цена отправки: {PriceSend}", null!, true);
            menu.AddMenuOption($"Останется: {GetClientCredits(player) - (PriceSend + CreditsCount)}", null!, true);
            bool HaveCredits = GetClientCredits(player) - (PriceSend + CreditsCount) >= 0;
            var target = transPlayer[player.Slot];
            menu.AddMenuOption(HaveCredits ? "Подтвердить" : $"Нехватает {(GetClientCredits(player) - (PriceSend + CreditsCount)) * -1}", (player, _) => 
            {
                if(target == null || !target.IsValid)
                {
                    player.PrintToChat("Игрок недоступен!");
                    return;
                }

                SetClientCredits(player, GetClientCredits(player) - (PriceSend + CreditsCount));
                SetClientCredits(target, CreditsCount+GetClientCredits(target));

                player.PrintToChat($"Вы успешно отправили {CreditsCount} кредит(-ов) игроку {target.PlayerName}!");
                target.PrintToChat($"Вы успешно получили {CreditsCount} кредит(-ов) от игрока {player.PlayerName}!");
            }, !HaveCredits);
            transPlayer[player.Slot] = null!;
            menu.ExitButton = true;
            menu.Open(player);
        }
        else player.PrintToChat("Вы можете писать только цифры! Повторите попытку.");

        return HookResult.Handled;
    }
    #endregion

    public int GetClientCredits(CCSPlayerController player)
    {
        var playerinfo = playerInfo[player.Slot];
        if(player == null || player.IsBot || player.IsHLTV || playerinfo == null || playerinfo.DatabaseID == -1) return -1;

        return playerinfo.Credits;
    }
    public async Task<int> GetClientCredits(string steamID)
    {
        //css_add_credits "STEAM_0:1:119197706" 10000
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
            Logger.LogError("{GetClientCreditsDB} Failed send info in database | " + ex.Message);
            Logger.LogDebug(ex.Message);
            throw new Exception("[SHOP] Failed send info in database! | " + ex.Message);
        }

        return -1;
    }
    public void SetClientCredits(CCSPlayerController player, int Credits)
    {
        if(player == null || player.IsBot || player.IsHLTV || playerInfo[player.Slot] == null || playerInfo[player.Slot].DatabaseID == -1) return;

        if(Credits < 0) Credits = 0;

        playerInfo[player.Slot].Credits = Credits;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    await connection.QueryAsync("UPDATE `shop_players` SET `money` = @Money WHERE `id` = @ID", new
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
                    await connection.QueryAsync("UPDATE `shop_players` SET `money` = @Money WHERE `auth` = @Steam", new
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