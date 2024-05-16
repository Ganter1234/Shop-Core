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
    public override string ModuleVersion => "1.6";
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
            player.PrintToChat($"Ваши данные загружаются! Пожалуйста подождите...");
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
        var slot = player.Slot;
        var Item = ItemsList.Find(x => x.UniqueName == UniqueName)!;
        int itemid = Item.ItemID;

        var list = playerInfo[slot].ItemList.Find(x => x.item_id == itemid);
        var state = playerInfo[slot].ItemStates.Find(x => x.ItemID == itemid);

        //playerInfo[slot].ItemList.FindIndex(x => x.item_id == itemid);

        var menu = CreateMenu(ItemName);

        menu.AddMenuOption($"Цена: {Item.BuyPrice} кредитов", null!, true);
        if(Item.Count <= -1)
        {
            var timeSpan = TimeSpan.FromSeconds(Item.Duration);
            menu.AddMenuOption($"Время действия: {(Item.Duration == 0 ? "Навсегда" : $"{timeSpan.TotalDays}д. {timeSpan.Hours}ч. {timeSpan.Minutes}м.")}", null!, true);
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
        var slot = player.Slot;

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
                    var command = connection.CreateCommand();

                    string sql = "";
                    if(playerList == null || playerList.count < 1)
                    {
                        sql = $@"INSERT INTO `shop_bought` (`player_id`, `item_id`, `count`, `duration`, `timeleft`, `buy_price`, `sell_price`, `buy_time`) VALUES ('{playerInfo[slot].DatabaseID}', '{ItemID}', '{Item.Count}',
                                        '{Item.Duration}', '{Item.Duration}', '{Item.BuyPrice}', '{Item.SellPrice}', '{DateTimeOffset.Now.ToUnixTimeSeconds()}');";
                        playerInfo[slot].ItemList.Add(new ItemInfo( ItemID, Item.Count, Item.Duration, Item.Duration, Item.BuyPrice,
                                                    Item.SellPrice, (int)DateTimeOffset.Now.ToUnixTimeSeconds() ));;
                    }
                    else
                    {
                        int index = playerInfo[slot].ItemList.IndexOf(playerList);
                        int new_count = playerInfo[slot].ItemList[index].count += 1;
                        sql = $"UPDATE `%sboughts` SET `count` = '{new_count}' WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{ItemID}'";
                    }

                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();

                    if(playerList == null && Item.Count <= -1) 
                    {
                        if(Item.Duration > 0)
                        {
                            Server.NextFrame(() =>
                            {
                                var timer = AddTimer(Item.Duration, () => TimerDeleteTimeleftItem(player, playerInfo[slot].ItemList.Find(x => x.item_id == ItemID)!));
                                playerInfo[slot].ItemTimeleft.Add(new ItemTimeleft( ItemID, timer ));
                            });
                        }

                        sql = $"INSERT INTO `shop_toggles` (`player_id`, `item_id`, `state`) VALUES ('{playerInfo[slot].DatabaseID}', '{ItemID}', '1')";
                        command.CommandText = sql;
                        await command.ExecuteNonQueryAsync();
                        playerInfo[slot].ItemStates.Add(new ItemStates( ItemID, 1 ));
                        DequipAllItemsOnCategory(player, Item.Category, ItemID);
                    }

                    playerInfo[slot].Credits -= Item.BuyPrice;
                    sql = $"UPDATE `shop_players` SET `money` = '{GetClientCredits(player)}' WHERE `id` = '{playerInfo[slot].DatabaseID}'";
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();

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
        var slot = player.Slot;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    var command = connection.CreateCommand();

                    int NewState = 0;
                    if(playerList.count <= -1)
                    {
                        string sql = "";
                        var Itemlist = ItemsList.Find(x => x.ItemID == ItemID);
                        int Index = playerInfo[slot].ItemStates.FindIndex(x => x.ItemID == ItemID)!;
                        NewState = playerInfo[slot].ItemStates[Index].State == 0 ? 1 : 0;
                        if(playerList.duration > 0 && Itemlist != null)
                        {
                            int timeleft = playerList.duration+playerList.buy_time-(int)DateTimeOffset.Now.ToUnixTimeSeconds();
                            if(timeleft > 0)
                            {
                                sql = $"UPDATE `shop_bought` SET `timeleft` = '{timeleft}' WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{playerList.item_id}'";
                                command.CommandText = sql;
                                await command.ExecuteNonQueryAsync();

                                if(NewState == 1)
                                {
                                    Server.NextFrame(() =>
                                    {
                                        var timer = AddTimer(timeleft, () => TimerDeleteTimeleftItem(player, playerList));
                                        playerInfo[slot].ItemTimeleft.Add(new ItemTimeleft( ItemID, timer ));
                                    });
                                }
                                else
                                {
                                    Server.NextFrame(() =>
                                    {
                                        int ind = playerInfo[slot].ItemTimeleft.FindIndex(x => x.ItemID == ItemID);
                                        playerInfo[slot].ItemTimeleft[ind].TimeleftTimer.Kill();
                                        playerInfo[slot].ItemTimeleft.RemoveAll(x => x.ItemID == playerList.item_id);
                                    });
                                }
                            }
                            else
                            {
                                sql = $"DELETE FROM `shop_bought` WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{playerList.item_id}'";
                                command.CommandText = sql;
                                await command.ExecuteNonQueryAsync();

                                Server.NextFrame(() =>
                                {
                                    int ind = playerInfo[slot].ItemTimeleft.FindIndex(x => x.ItemID == ItemID);
                                    playerInfo[slot].ItemTimeleft[ind].TimeleftTimer.Kill();
                                    playerInfo[slot].ItemTimeleft.RemoveAll(x => x.ItemID == playerList.item_id);
                                });

                                playerInfo[slot].ItemList.Remove(playerList);

                                sql = $"DELETE FROM `shop_toggles` WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{playerList.item_id}'";
                                command.CommandText = sql;
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        if(NewState == 1) DequipAllItemsOnCategory(player, Category, ItemID);

                        playerInfo[slot].ItemStates[Index].State = NewState;

                        sql = $"UPDATE `shop_toggles` SET `state` = '{NewState}' WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{ItemID}';";
                        command.CommandText = sql;
                        await command.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        int index = playerInfo[slot].ItemList.IndexOf(playerList);
                        int new_count = playerInfo[slot].ItemList[index].count -= 1;
                        string sql = $"UPDATE `%sboughts` SET `count` = '{new_count}' WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{ItemID}'";
                        command.CommandText = sql;
                        await command.ExecuteNonQueryAsync();
                    }

                    Server.NextFrame(() => 
                    {
                        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
                        if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, ItemID, UniqueName, NewState);
                        _api!.OnClientToggleItem(player, ItemID, UniqueName, NewState);
                        OnChooseItem(player, ItemName, UniqueName);
                    });
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
        var playerinfo = playerInfo[player.Slot];
        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    var command = connection.CreateCommand();
                    foreach(var list in playerinfo.ItemStates.FindAll(x => x.State == 1 && x.ItemID != newItemID))
                    {
                        int oldIndex = playerinfo.ItemStates.IndexOf(list);
                        if(oldIndex != -1)
                        {
                            var oldItem = ItemsList.Find(x => x.Category == category && x.ItemID == playerinfo.ItemStates[oldIndex].ItemID);
                            if(oldItem != null)
                            {
                                playerinfo.ItemStates[oldIndex].State = 0;

                                Server.NextFrame(() => 
                                {
                                    var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == oldItem.ItemID);
                                    if(CallbackList != null && CallbackList.OnToggleItem != null) CallbackList.OnToggleItem.Invoke(player, oldItem.ItemID, oldItem.UniqueName, 0);
                                    _api!.OnClientToggleItem(player, oldItem.ItemID, oldItem.UniqueName, 0);
                                });

                                string sql = $"UPDATE `shop_toggles` SET `state` = '0' WHERE `player_id` = '{playerinfo.DatabaseID}' AND `item_id` = '{oldItem.ItemID}';";
                                command.CommandText = sql;
                                await command.ExecuteNonQueryAsync();
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
        var slot = player.Slot;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    var command = connection.CreateCommand();

                    string sql = "";
                    int new_count = 0;
                    if(playerList != null)
                    {
                        int index = playerInfo[slot].ItemList.IndexOf(playerList);
                        new_count = playerInfo[slot].ItemList[index].count -= 1;
                    }
                    if(playerList == null || new_count < 1)
                    {
                        sql = $"DELETE FROM `shop_bought` WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{ItemID}'";
                        command.CommandText = sql;
                        await command.ExecuteNonQueryAsync();

                        if(playerList == null)
                        {
                            sql = $"DELETE FROM `shop_toggles` WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{ItemID}'";
                            command.CommandText = sql;
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    playerInfo[slot].Credits += Convert.ToInt32(SellPrice);
                    sql = $"UPDATE `shop_players` SET `money` = '{GetClientCredits(player)}' WHERE `id` = '{playerInfo[slot].DatabaseID}'";
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();

                    Server.NextFrame(() => {
                        var CallbackList = _api!.ItemCallback.Find(x => x.ItemID == ItemID);
                        if(CallbackList != null && CallbackList.OnSellItem != null) CallbackList.OnSellItem.Invoke(player, ItemID, UniqueName, SellPrice);
                        _api!.OnClientSellItem(player, ItemID, UniqueName, SellPrice);
                        playerInfo[slot].ItemList.RemoveAll(x => x.item_id == ItemID);
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
                    var command = connection.CreateCommand();
                    string sql = $"SELECT `id`, `money` FROM `shop_players` WHERE `auth` = '{steamid}';";
                    command.CommandText = sql;
                    var reader = await command.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        playerInfo[playerSlot] = new PlayerInformation
                        {
                            SteamID = steamid,
                            Credits = reader.GetInt32(1),
                            DatabaseID = reader.GetInt32(0),
                            ItemList = new(),
                            ItemStates = new(),
                            ItemTimeleft = new()
                        };

                        reader.Close();

                        sql = $"UPDATE `shop_players` SET `name` = @Name WHERE `auth` = '{steamid}';";
                        command.Parameters.AddWithValue("@Name", nickname); //TODO: Переделать отправки запросов, пользоваться Dapper
                        Console.WriteLine($"DEBUGG {sql}");
                        command.CommandText = sql;
                        await command.ExecuteNonQueryAsync();
                    }
                    else
                    {
                        reader.Close();

                        sql = $"INSERT INTO `shop_players` (`auth`, `name`) VALUES ('{steamid}', @Name);";
                        command.Parameters.AddWithValue("@Name", nickname); 
                        command.CommandText = sql;
                        await command.ExecuteNonQueryAsync();
                        sql = $"SELECT `id` FROM `shop_players` WHERE `auth` = '{steamid}';";
                        command.CommandText = sql;
                        var reader2 = await command.ExecuteReaderAsync();
                        if (await reader2.ReadAsync())
                        {
                            playerInfo[playerSlot] = new PlayerInformation
                            {
                                SteamID = steamid,
                                Credits = 0,
                                DatabaseID = reader.GetInt32(0),
                                ItemList = new(),
                                ItemStates = new(),
                                ItemTimeleft = new()
                            };
                        }
                        reader2.Close();
                    }

                    // Загрузка предметов
                    sql = $"SELECT * FROM `shop_bought` WHERE `player_id` = '{playerInfo[playerSlot].DatabaseID}';";
                    command.CommandText = sql;
                    reader = await command.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        playerInfo[playerSlot].ItemList.Add(new ItemInfo(
                            reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
                            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6),
                            reader.GetInt32(7) ));
                    }
                    reader.Close();

                    // fix: Collection was modified; enumeration operation may not execute
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
                                sql = $"UPDATE `shop_bought` SET `timeleft` = '{timeleft}' WHERE `player_id` = '{playerInfo[playerSlot].DatabaseID}' AND `item_id` = '{item.item_id}';";
                                command.CommandText = sql;
                                await command.ExecuteNonQueryAsync();

                                Server.NextFrame(() => 
                                {
                                    var timer = AddTimer(timeleft, () => TimerDeleteTimeleftItem(player, item));
                                    playerInfo[playerSlot].ItemTimeleft.Add(new ItemTimeleft( item.item_id, timer ));
                                });
                            }
                            else
                            {
                                sql = $"DELETE FROM `shop_bought` WHERE `player_id` = '{playerInfo[playerSlot].DatabaseID}' AND `item_id` = '{item.item_id}';";
                                command.CommandText = sql;
                                await command.ExecuteNonQueryAsync();

                                playerInfo[playerSlot].ItemList.Remove(item);

                                sql = $"DELETE FROM `shop_toggles` WHERE `player_id` = '{playerInfo[playerSlot].DatabaseID}' AND `item_id` = '{item.item_id}';";
                                command.CommandText = sql;
                                await command.ExecuteNonQueryAsync();
                            }
                        }
                    }

                    sql = $"SELECT * FROM `shop_toggles` WHERE `player_id` = '{playerInfo[playerSlot].DatabaseID}';";
                    command.CommandText = sql;
                    reader = await command.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        // TODO
                        int itemid = reader.GetInt32(2);
                        int state = reader.GetInt32(3);
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

                    reader.Close();
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

        var slot = player.Slot;

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
                    var command = connection.CreateCommand();

                    string sql = $"DELETE FROM `shop_bought` WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{Item.item_id}'";
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();

                    playerInfo[slot].ItemTimeleft.RemoveAll(x => x.ItemID == Item.item_id);
                    playerInfo[slot].ItemList.Remove(Item);

                    sql = $"DELETE FROM `shop_toggles` WHERE `player_id` = '{playerInfo[slot].DatabaseID}' AND `item_id` = '{Item.item_id}'";
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
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

                    sql = @"CREATE TABLE IF NOT EXISTS `shop_bought` (
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
                string sql = $"SELECT `id` FROM `shop_items` WHERE `category` = '{Category}' AND `item` = '{UniqueName}' LIMIT 1;";
                var command = connection.CreateCommand();
                command.CommandText = sql;
                var reader = await command.ExecuteReaderAsync();
                if(await reader.ReadAsync()) {
                    int id = reader.GetInt32(0);
                    reader.Close();
                    item_id = id;
                }
                else {
                    reader.Close();
                    sql = $"INSERT INTO `shop_items` (`category`, `item`) VALUES ('{Category}', '{UniqueName}');";
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();

                    sql = $"SELECT `id` FROM `shop_items` WHERE `category` = '{Category}' AND `item` = '{UniqueName}' LIMIT 1;";
                    command = connection.CreateCommand();
                    command.CommandText = sql;
                    reader = await command.ExecuteReaderAsync();
                    if(await reader.ReadAsync()) {
                        int id = reader.GetInt32(0);
                        reader.Close();
                        item_id = id;
                    }
                    else
                    {
                        reader.Close();
                        return -1;
                    }
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
            //int counter = 0;
            ItemsList.Add(new Items( item_id, Category, UniqueName, ItemName, BuyPrice, SellPrice, Duration, Count ));
            //foreach(var item in ItemsList)
            //{
            //    Logger.LogError($"ITEM LIST: {counter} | {item_id} | {item.ItemName} | {item.ItemID} |  {item.UniqueName}, {item.Category}");
            //    Logger.LogDebug($"ITEM LIST: {counter} | {item_id} | {item.ItemName} | {item.ItemID} |  {item.UniqueName}, {item.Category}");
            //    counter++;
            //}
        });
        return item_id;
    }

    #region Commands

    [CommandHelper(minArgs: 3, usage: "<name/userid> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
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

		var targets = commandInfo.GetArgTargetResult(1);

        foreach(var target in targets)
        {
            if(target == null || playerInfo[target.Slot] == null) continue;

            SetClientCredits(target, GetClientCredits(target) + Convert.ToInt32(commandInfo.GetArg(2)));
            Server.PrintToChatAll($"[Shop] Админ {(player == null ? "Console" : player.PlayerName)} выдал {commandInfo.GetArg(2)} кредитов игроку {target.PlayerName}");
        }
	}

    [CommandHelper(minArgs: 3, usage: "<name/userid> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
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

		var targets = commandInfo.GetArgTargetResult(1);

        foreach(var target in targets)
        {
            if(target == null || playerInfo[target.Slot] == null) continue;

            SetClientCredits(target, Convert.ToInt32(commandInfo.GetArg(2)));
            Server.PrintToChatAll($"[Shop] Админ {(player == null ? "Console" : player.PlayerName)} установил {Convert.ToInt32(commandInfo.GetArg(2))} кредитов игроку {target.PlayerName}");
        }
	}

    [CommandHelper(minArgs: 3, usage: "<name/userid> <credits_count>", whoCanExecute: CommandUsage.CLIENT_AND_SERVER)]
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

		var targets = commandInfo.GetArgTargetResult(1);

        foreach(var target in targets)
        {
            if(target == null || playerInfo[target.Slot] == null) continue;

            SetClientCredits(target, GetClientCredits(target) - Convert.ToInt32(commandInfo.GetArg(2)));
            Server.PrintToChatAll($"[Shop] Админ {(player == null ? "Console" : player.PlayerName)} отобрал {Convert.ToInt32(commandInfo.GetArg(2))} кредитов у игрока {target.PlayerName}");
        }
    }

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
        var slot = player.Slot;
        if(player == null || player.IsBot || player.IsHLTV ||  playerInfo[slot] == null || playerInfo[slot].DatabaseID == -1) return -1;

        return playerInfo[player.Slot].Credits;
    }
    public void SetClientCredits(CCSPlayerController player, int Credits)
    {
        var slot = player.Slot;
        if(player == null || player.IsBot || player.IsHLTV || playerInfo[slot] == null || playerInfo[slot].DatabaseID == -1) return;

        if(Credits < 0) Credits = 0;

        playerInfo[slot].Credits = Credits;

        Task.Run(async () => 
        {
            try
            {
                await using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();
                    var command = connection.CreateCommand();

                    string sql = $"UPDATE `shop_players` SET `money` = '{Credits}' WHERE `id` = '{playerInfo[slot].DatabaseID}'";
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
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