using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;

namespace ShopAPI;

public interface IShopApi
{
    ///
    /// <summary>
    /// Capability плагина
    /// </summary>
    ///
	public static PluginCapability<IShopApi> Capability { get; } = new("Shop_Core:API");

    ///
    /// <summary>
    /// Строка для подключения к БД
    /// </summary>
    ///
	string dbConnectionString { get; }
	
	///
    /// <summary>
    /// Узнать количество кредитов у игрока
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <returns>Количество кредитов</returns>
    ///
    int GetClientCredits(CCSPlayerController player);

    ///
    /// <summary>
    /// Установка определенного количества кредитов игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="credits">Количество кредитов которое надо выдать</param>
    ///
    void SetClientCredits(CCSPlayerController player, int credits);

    ///
    /// <summary>
    /// Узнает айди игрока из базы данных
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <returns>Айди игрока с БД</returns>
    ///
    int GetClientID(CCSPlayerController player);

    ///
    /// <summary>
    /// Авторизирован ли игрок на сервере
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <returns>Авторизирован или нет</returns>
    ///
	bool IsClientAuthorized(CCSPlayerController player);

    ///
    /// <summary>
    /// Получить айди предмета по уникальному имени предмета
    /// </summary>
    /// <param name="uniqueName">Уникальное имя предмета</param>
    /// <returns>Айди предмета или -1 если предмет не найден</returns>
    ///
	int GetItemIdByUniqueName(string uniqueName);

    ///
    /// <summary>
    /// Выдать предмет игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="customDuration">Длительность предмета или количество</param>
    /// <returns>Выдался предмет или нет</returns>
    ///
	bool GiveClientItem(CCSPlayerController player, int itemID, int customDuration);

    ///
    /// <summary>
    /// Удалить предмет у игрока
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="count">Количество если это поштучный предмет</param>
    /// <returns>Удалился предмет или нет</returns>
    ///
	bool RemoveClientItem(CCSPlayerController player, int itemID, int count = -1);

    ///
    /// <summary>
    /// Создание категории для товаров магазина
    /// </summary>
    /// <param name="categoryName">Уникальное название категории (Которое указывать при создании предмета)</param>
    /// <param name="displayName">Название которое будет отображаться в меню</param>
    ///
	void CreateCategory(string categoryName, string displayName);

    ///
    /// <summary>
    /// Создание предмета для магазина (Выполнять натив через Task.Run())
    /// </summary>
    /// <param name="uniqueName">Уникальное название предмета (Для баз данных)</param>
    /// <param name="itemName">Название которое будет отображаться в меню</param>
    /// <param name="categoryName">Название категории в котором должен быть предмет (Категория должны быть предварительно создана)</param>
    /// <param name="buyPrice">Цена покупки предмета</param>
    /// <param name="sellPrice">Цена продажи предмета</param>
    /// <param name="duration">Длительность предмета, т.е сколько будет активен предмет у игрока (ничего не указывать если хотите создать ограниченный предмет)</param>
    /// <param name="count">Количество товара которое выдается при покупке (ничего не указывать если хотите создать используемый предмет)</param>
    /// <returns>Айди предмета</returns>
    ///
	Task<int> AddItem(string uniqueName, string itemName, string categoryName, int buyPrice, int sellPrice, int duration = -1, int count = -1);

    [Obsolete("Используйте версию с onUseItem")]
    void SetItemCallbacks(int itemID, Action<CCSPlayerController, int, string, string, int, int, int, int>? onBuyItem = null, Action<CCSPlayerController, int, string, int>? onSellItem = null, Action<CCSPlayerController, int, string, int>? onToggleItem = null);

    ///
    /// <summary>
    /// Создание отдельных обратных вызовов для определенного предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="onBuyItem">Обратный вызов при покупке этого предмета</param>
    /// <param name="onSellItem">Обратный вызов при продаже этого предмета</param>
    /// <param name="onToggleItem">Обратный вызов при изменении состояния предмета</param>
    /// <param name="onUseItem">Обратный вызов при использовании предмета</param>
    ///
    void SetItemCallbacks(int itemID, Action<CCSPlayerController, int, string, string, int, int, int, int>? onBuyItem = null, Action<CCSPlayerController, int, string, int>? onSellItem = null, Action<CCSPlayerController, int, string, int>? onToggleItem = null, Action<CCSPlayerController, int, string, int>? onUseItem = null);

    ///
    /// <summary>
    /// Узнать есть ли предмет в магазине
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Есть предмет или нет</returns>
    ///
	bool IsItemExists(int itemID);

    ///
    /// <summary>
    /// Узнать цену предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Цена предмета</returns>
    ///
	int GetItemPrice(int itemID);

    ///
    /// <summary>
    /// Установить цену предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="price">Новая цена на предмет</param>
    /// <returns>Поменялась цена или нет</returns>
    ///
	bool SetItemPrice(int itemID, int price);

    ///
    /// <summary>
    /// Узнать цену продажи предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Цена продажи предмета</returns>
    ///
	int GetItemSellPrice(int itemID);

    ///
    /// <summary>
    /// Установить цену продажи предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="sellPrice">Новая цена предмета</param>
    /// <returns>Поменялась цена или нет</returns>
    ///
	bool SetItemSellPrice(int itemID, int sellPrice);

    ///
    /// <summary>
    /// Узнать срок действия предмета (в случае если он не поштучный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Срок действия предмета</returns>
    ///
	int GetItemDuration(int itemID);

    ///
    /// <summary>
    /// Установить срок действия предмета (в случае если он не поштучный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="duration">Новый срок действия</param>
    /// <returns>Поменялся срок действия или нет</returns>
    ///
	bool SetItemDuration(int itemID, int duration);

    ///
    /// <summary>
    /// Узнать количество покупаемых предметов (в случае если он не временный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Количество покупаемых предметов</returns>
    ///
	int GetItemCount(int itemID);

    ///
    /// <summary>
    /// Установить количество покупаемых предметов (в случае если он не временный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="count">Новое количество</param>
    /// <returns>Поменялось количество предметов или нет</returns>
    ///
	bool SetItemCount(int itemID, int count);
	
	// Игрок, Айди предмета, Название категории, Уникальное имя предмета, Цена покупки, Цена продажи, Длительность предмета, Кол-во предмета
	event Action<CCSPlayerController, int, string, string, int, int, int, int>? ClientBuyItem;
	
	// Игрок, Айди предмета, Уникальное имя предмета, Цена продажи
	event Action<CCSPlayerController, int, string, int>? ClientSellItem;
	
	// Игрок, Айди предмета, Уникальное имя предмета, Состояние
	event Action<CCSPlayerController, int, string, int>? ClientToggleItem;

    // Игрок, Айди предмета, Уникальное имя предмета, Новое кол-во предметов
	event Action<CCSPlayerController, int, string, int>? ClientUseItem;

    // Событие что ядро загружено
    event Action? OnCoreLoaded;
}