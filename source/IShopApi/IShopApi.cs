using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace ShopAPI;

public interface IShopApi
{
	public static PluginCapability<IShopApi> Capability { get; } = new("Shop_Core:API");
	string dbConnectionString { get; } // Строка для подключения к БД
	
	//
    // Summary:
    //     Узнает количество кредитов у игрока
	//	   player - CCSPlayerController игрока
    //
	//
    // Returns:
    //     Количество кредитов
    int GetClientCredits(CCSPlayerController player);
	
	//
    // Summary:
    //     Установка определенного количества кредитов игроку
	//	   player - CCSPlayerController игрока
	//	   Credits - Количество кредитов которое надо выдать
    //
	//
    // Returns:
    //     Ничего не возвращает
    void SetClientCredits(CCSPlayerController player, int Credits);

	//
    // Summary:
    //     Узнает айди игрока из базы данных
	//	   player - CCSPlayerController игрока
    //
	//
    // Returns:
    //     Айди игрока с БД
    int GetClientID(CCSPlayerController player);
	
	//
    // Summary:
    //     Создание категории для товаров магазина
	//	   CategoryName - Уникальное название категории (Которое указывать при создании предмета)
	//	   DisplayName - Название которое будет отображаться в меню
    //
	//
    // Returns:
    //     Ничего не возвращает
	void CreateCategory(string CategoryName, string DisplayName);
	
	//
    // Summary:
    //     Создание предмета для магазина (Выполнять натив через Task.Run())
	//	   UniqueName - Уникальное название предмета (Для баз данных)
	//	   ItemName - Название которое будет отображаться в меню
	//     CategoryName - Название категории в котором должен быть предмет (Категория должны быть предварительно создана)
    //     BuyPrice - Цена покупки предмета
	//     SellPrice - Цена продажи предмета
	//     Duration - Длительность предмета, т.е сколько будет активен предмет у игрока (ничего не указывать если хотите создать ограниченный предмет)
	//     Count - Количество товара которое выдается при покупке (ничего не указывать если хотите создать используемый предмет)
	//
    //
    // Returns:
    //     Айди предмета
	Task<int> AddItem(string UniqueName, string ItemName, string CategoryName, int BuyPrice, int SellPrice, int Duration = -1, int Count = -1);

    //
    // Summary:
    //     Создание отдельных обратных вызовов для определенного предмета
	//	   ItemID - Айди предмета
	//	   OnBuyItem - Обратный вызов при покупке этого предмета
    //	   OnSellItem - Обратный вызов при продаже этого предмета
    //	   OnToggleItem - Обратный вызов при изменении состояния предмета
    //
	//
    // Returns:
    //     Ничего не возвращает
    void SetItemCallbacks(int ItemID, Action<CCSPlayerController, int, string, string, int, int, int, int>? OnBuyItem = null, Action<CCSPlayerController, int, string, int>? OnSellItem = null, Action<CCSPlayerController, int, string, int>? OnToggleItem = null);

	//
    // Summary:
    //     Узнать есть ли предмет в магазине
	//	   ItemID - Айди предмета
	//
    //
    // Returns:
    //     Есть предмет или нет
	bool IsItemExists(int ItemID);

	//
    // Summary:
    //     Узнать цену предмета
	//	   ItemID - Айди предмета
	//
    //
    // Returns:
    //     Цена предмета
	int GetItemPrice(int ItemID);

	//
    // Summary:
    //     Установить цену предмета
	//	   ItemID - Айди предмета
	//	   Price - Новая цена на предмет
	//
    //
    // Returns:
    //     Поменялась цена или нет
	bool SetItemPrice(int ItemID, int Price);

	//
    // Summary:
    //     Узнать цену продажи предмета
	//	   ItemID - Айди предмета
	//
    //
    // Returns:
    //     Цена продажи предмета
	int GetItemSellPrice(int ItemID);

	//
    // Summary:
    //     Установить цену продажи предмета
	//	   ItemID - Айди предмета
	//	   SellPrice - Новая цена продажи
	//
    //
    // Returns:
    //     Поменялась цена или нет
	bool SetItemSellPrice(int ItemID, int SellPrice);

	//
    // Summary:
    //     Узнать срок действия предмета (в случае если он не поштучный)
	//	   ItemID - Айди предмета
	//
    //
    // Returns:
    //     Срок действия предмета
	int GetItemDuration(int ItemID);

	//
    // Summary:
    //     Установить срок действия предмета (в случае если он не поштучный)
	//	   ItemID - Айди предмета
	//	   Duration - Новый срок действия
	//
    //
    // Returns:
    //     Поменялся срок действия или нет
	bool SetItemDuration(int ItemID, int Duration);

	//
    // Summary:
    //     Узнать количество покупаемых предметов (в случае если он не временный)
	//	   ItemID - Айди предмета
	//
    //
    // Returns:
    //     Срок действия предмета
	int GetItemCount(int ItemID);

	//
    // Summary:
    //     Установить количество покупаемых предметов (в случае если он не временный)
	//	   ItemID - Айди предмета
	//	   Count - Новое количество
	//
    //
    // Returns:
    //     Поменялось количество предметов или нет
	bool SetItemCount(int ItemID, int Count);
	
	// Игрок, Айди предмета, Название категории, Уникальное имя предмета, Цена покупки, Цена продажи, Длительность предмета, Кол-во предмета
	event Action<CCSPlayerController, int, string, string, int, int, int, int>? ClientBuyItem;
	
	// Игрок, Айди предмета, Уникальное имя предмета, Цена продажи
	event Action<CCSPlayerController, int, string, int>? ClientSellItem;
	
	// Игрок, Айди предмета, Уникальное имя предмета, Состояние
	event Action<CCSPlayerController, int, string, int>? ClientToggleItem;
}