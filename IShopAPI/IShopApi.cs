using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;

namespace ShopAPI;

public interface IShopApi
{
    /// <summary>
    /// Enum для определения как именно изменилось количество кредитов
    /// </summary>
    /// <remarks>
    /// ByAdminCommand = Через встроенные команды в магазине по типу css_add_credits
    /// ByFunction = Через функцию в API плагина
    /// ByBuyOrSell = Через покупку или продажу предмета
    /// ByTransfer = Через передачу кредитов
    /// IgnoreCallbackHook = При использовании функции изменения кредитов можно заблокировать вызов Callback
    /// </remarks>
    enum WhoChangeCredits
    {
        ByAdminCommand,
        ByFunction,
        ByBuyOrSell,
        ByTransfer,
        IgnoreCallbackHook
    }
    /// <summary>
    /// Capability плагина
    /// </summary>
	public static PluginCapability<IShopApi> Capability { get; } = new("Shop_Core:API");

    /// <summary>
    /// Строка для подключения к БД
    /// </summary>
	string dbConnectionString { get; }

    /// <summary>
    /// Узнать количество кредитов у игрока
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <returns>Количество кредитов</returns>
    int GetClientCredits(CCSPlayerController player);

    /// <summary>
    /// Узнать если у игрока определенный предмет
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Есть предмет или нет. Возвращает null, если предмет или игрок не найдены.</returns>
    bool? IsClientHasItem(CCSPlayerController player, int itemID);

    /// <summary>
    /// Использовать предмет игрока в инвентаре (Только Finite предметы) (Проверку IsClientHasItem делать не нужно)
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Использовался предмет или нет</returns>
    bool UseClientItem(CCSPlayerController player, int itemID);

    /// <summary>
    /// Установка определенного количества кредитов игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="credits">Количество кредитов которое надо выдать</param>
    /// <param name="by_who">Как произошло изменение кредитов</param>
    void SetClientCredits(CCSPlayerController player, int credits, WhoChangeCredits by_who = WhoChangeCredits.ByFunction);

    /// <summary>
    /// Добавление определенного количества кредитов игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="credits">Количество кредитов которое надо добавить</param>
    /// <param name="by_who">Как произошло изменение кредитов</param>
    void AddClientCredits(CCSPlayerController player, int credits, WhoChangeCredits by_who = WhoChangeCredits.ByFunction);

    /// <summary>
    /// Отбирание определенного количества кредитов игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="credits">Количество кредитов которое надо отобрать</param>
    /// <param name="by_who">Как произошло изменение кредитов</param>
    void TakeClientCredits(CCSPlayerController player, int credits, WhoChangeCredits by_who = WhoChangeCredits.ByFunction);

    /// <summary>
    /// Узнает айди игрока из базы данных
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <returns>Айди игрока с БД</returns>
    int GetClientID(CCSPlayerController player);

    /// <summary>
    /// Авторизирован ли игрок на сервере
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <returns>Авторизирован или нет</returns>
	bool IsClientAuthorized(CCSPlayerController player);

    /// <summary>
    /// Является ли игрок администратором (Определяется по флагу с конфига)
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <returns>Админ или нет</returns>
	bool IsAdmin(CCSPlayerController player);

	/// <summary>
	/// Создает меню на основе конфигурации.
	/// Если UseCenterMenu равно false, создается ChatMenu; если true, то CenterHtmlMenu.
    /// Если используется MenuManager то меню создается на основе конфигурации этого плагина
	/// </summary>
	/// <param name="title">Заголовок меню.</param>
	/// <returns>Экземпляр IMenu.</returns>
	IMenu CreateMenu(string title);
	
    /// <summary>
    /// Добавить подменю в меню функций
    /// </summary>
    /// <param name="display">Отображаемый текст в подменю (либо фразу для переводов (перевод надо писать в файл ядра) )</param>
    /// <param name="openMenu">Обратный вызов при открытии этого подменю</param>
    void AddToFunctionsMenu(string display, Action<CCSPlayerController> openMenu);

    /// <summary>
    /// Удалить подменю в меню функций
    /// </summary>
    /// <param name="openMenu">Обратный вызов который использовался для открытия подменю</param>
    void RemoveFromFunctionsMenu(Action<CCSPlayerController> openMenu);

    /// <summary>
    /// Отобразить меню функций игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    void ShowFunctionsMenu(CCSPlayerController player);

    /// <summary>
    /// Открыть главное меню магазина игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    void OpenMainMenu(CCSPlayerController player);

    /// <summary>
    /// Получить состояние предмета у игрока.
    /// </summary>
    /// <param name="uniqueName">Уникальное имя предмета.</param>
    /// <param name="player">Игрок, у которого нужно проверить предмет.</param>
    /// <returns>True, если предмет включен, иначе False. Возвращает null, если предмет или игрок не найдены.</returns>
    bool? GetItemState(string uniqueName, CCSPlayerController player);    
    
    /// <summary>
    /// Получить айди предмета по уникальному имени предмета
    /// </summary>
    /// <param name="uniqueName">Уникальное имя предмета</param>
    /// <returns>Айди предмета или -1 если предмет не найден</returns>
	int GetItemIdByUniqueName(string uniqueName);

    /// <summary>
    /// Выдать предмет игроку
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="customDuration">Длительность предмета или количество</param>
    /// <returns>Выдался предмет или нет</returns>
	bool GiveClientItem(CCSPlayerController player, int itemID, int customDuration);

    /// <summary>
    /// Удалить предмет у игрока
    /// </summary>
    /// <param name="player">CCSPlayerController игрока</param>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="count">Количество если это поштучный предмет</param>
    /// <returns>Удалился предмет или нет</returns>
	bool RemoveClientItem(CCSPlayerController player, int itemID, int count = -1);

    /// <summary>
    /// Создание категории для товаров магазина
    /// </summary>
    /// <param name="categoryName">Уникальное название категории (Которое указывать при создании предмета)</param>
    /// <param name="displayName">Название которое будет отображаться в меню</param>
	void CreateCategory(string categoryName, string displayName);

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
	Task<int> AddItem(string uniqueName, string itemName, string categoryName, int buyPrice, int sellPrice, int duration = -1, int count = -1);

    /// <summary>
    /// Создание отдельных обратных вызовов для определенного предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="onBuyItem">Обратный вызов при покупке этого предмета</param>
    /// <param name="onSellItem">Обратный вызов при продаже этого предмета</param>
    /// <param name="onToggleItem">Обратный вызов при изменении состояния предмета</param>
    /// <param name="onUseItem">Обратный вызов при использовании предмета</param>
    /// <param name="onPreview">Обратный вызов при активации превью</param>
    void SetItemCallbacks(int itemID, Func<CCSPlayerController, int, string, string, int, int, int, int, HookResult>? onBuyItem = null, Func<CCSPlayerController, int, string, int, HookResult>? onSellItem = null, Func<CCSPlayerController, int, string, int, HookResult>? onToggleItem = null, Func<CCSPlayerController, int, string, int, HookResult>? onUseItem = null, Action<CCSPlayerController, int, string, string>? onPreview = null);

    /// <summary>
    /// Отгрузить предмет из магазина
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
	void UnregisterItem(int itemID);

    /// <summary>
    /// Отгрузить категорию предметов из магазина
    /// </summary>
    /// <param name="categoryName">Уникальное название категории</param>
    /// <param name="removeAllItems">Убрать ли все предметы из категории после отгрузки?</param>
	void UnregisterCategory(string categoryName, bool removeAllItems);

    /// <summary>
    /// Узнать есть ли предмет в магазине
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Есть предмет или нет</returns>
	bool IsItemExists(int itemID);

    /// <summary>
    /// Узнать цену предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Цена предмета</returns>
	int GetItemPrice(int itemID);

    /// <summary>
    /// Установить цену предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="price">Новая цена на предмет</param>
    /// <returns>Поменялась цена или нет</returns>
	bool SetItemPrice(int itemID, int price);

    /// <summary>
    /// Узнать цену продажи предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Цена продажи предмета</returns>
	int GetItemSellPrice(int itemID);

    /// <summary>
    /// Установить цену продажи предмета
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="sellPrice">Новая цена предмета</param>
    /// <returns>Поменялась цена или нет</returns>
	bool SetItemSellPrice(int itemID, int sellPrice);

    /// <summary>
    /// Узнать срок действия предмета (в случае если он не поштучный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Срок действия предмета</returns>
	int GetItemDuration(int itemID);

    /// <summary>
    /// Установить срок действия предмета (в случае если он не поштучный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="duration">Новый срок действия</param>
    /// <returns>Поменялся срок действия или нет</returns>
	bool SetItemDuration(int itemID, int duration);

    /// <summary>
    /// Узнать количество покупаемых предметов (в случае если он не временный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <returns>Количество покупаемых предметов</returns>
	int GetItemCount(int itemID);

    /// <summary>
    /// Установить количество покупаемых предметов (в случае если он не временный)
    /// </summary>
    /// <param name="itemID">Айди предмета</param>
    /// <param name="count">Новое количество</param>
    /// <returns>Поменялось количество предметов или нет</returns>
	bool SetItemCount(int itemID, int count);

    /// <summary>
    /// Получить переведенный текст по ключу.
    /// </summary>
    /// <param name="name">Ключ перевода.</param>
    /// <param name="args">Аргументы форматирования.</param>
    /// <returns>Переведенная строка.</returns>
    string GetTranslatedText(string name, params object[] args);

    /// <summary>
    /// Событие покупки предмета
    /// </summary>
    /// <remarks>
    /// Игрок, Айди предмета, Название категории, Уникальное имя предмета, Цена покупки, Цена продажи, Длительность предмета, Кол-во предмета.
    /// </remarks>
    event Action<CCSPlayerController, int, string, string, int, int, int, int>? ClientBuyItem;

    /// <summary>
    /// Событие перед покупкой предмета
    /// </summary>
    /// <remarks>
    /// Игрок, Айди предмета, Название категории, Уникальное имя предмета, Цена покупки, Цена продажи, Длительность предмета, Кол-во предмета. Return: Continue = Продолжить без изменений, другое заблокирует покупку
    /// </remarks>
    event Func<CCSPlayerController, int, string, string, int, int, int, int, HookResult?>? ClientBuyItemPre;

    /// <summary>
    /// Событие продажи предмета
    /// </summary>
    /// <remarks>
    /// Игрок, Айди предмета, Уникальное имя предмета, Цена продажи.
    /// </remarks>
    event Action<CCSPlayerController, int, string, int>? ClientSellItem;

    /// <summary>
    /// Событие смены состояния у предмета
    /// </summary>
    /// <remarks>
    /// Игрок, Айди предмета, Уникальное имя предмета, Состояние.
    /// </remarks>
    event Action<CCSPlayerController, int, string, int>? ClientToggleItem;

    /// <summary>
    /// Событие использования предмета
    /// </summary>
    /// <remarks>
    /// Игрок, Айди предмета, Уникальное имя предмета, Новое кол-во предметов.
    /// </remarks>
    event Action<CCSPlayerController, int, string, int>? ClientUseItem;

    /// <summary>
    /// Событие активации превью режима
    /// </summary>
    /// <remarks>
    /// Игрок, Айди предмета, Уникальное имя предмета, Название категории.
    /// </remarks>
    event Action<CCSPlayerController, int, string, string>? ClientPreview;

    /// <summary>
    /// Событие установки кредитов
    /// </summary>
    /// <remarks>
    /// Игрок, Новое количество кредитов, От чего произошло изменение кредитов. Return: null = Продолжить без изменений, остальное это новое количество кредитов
    /// </remarks>
    event Func<CCSPlayerController, int, WhoChangeCredits, int?>? CreditsSet;

    /// <summary>
    /// Событие после установки кредитов
    /// </summary>
    /// <remarks>
    /// Игрок, Новое количество кредитов, От чего произошло изменение кредитов.
    /// </remarks>
    event Action<CCSPlayerController, int, WhoChangeCredits>? CreditsSetPost;

    /// <summary>
    /// Событие выдачи кредитов
    /// </summary>
    /// <remarks>
    /// Игрок, Новое количество кредитов, От чего произошло изменение кредитов. Return: null = Продолжить без изменений, остальное это новое количество кредитов
    /// </remarks>
    event Func<CCSPlayerController, int, WhoChangeCredits, int?>? CreditsAdd;

    /// <summary>
    /// Событие после выдачи кредитов
    /// </summary>
    /// <remarks>
    /// Игрок, Новое количество кредитов, От чего произошло изменение кредитов.
    /// </remarks>
    event Action<CCSPlayerController, int, WhoChangeCredits>? CreditsAddPost;

    /// <summary>
    /// Событие отбирания кредитов
    /// </summary>
    /// <remarks>
    /// Игрок, Новое количество кредитов, От чего произошло изменение кредитов. Return: null = Продолжить без изменений, остальное это новое количество кредитов
    /// </remarks>
    event Func<CCSPlayerController, int, WhoChangeCredits, int?>? CreditsTake;

	/// <summary>
    /// Событие после отбирания кредитов
    /// </summary>
    /// <remarks>
    /// Игрок, Новое количество кредитов, От чего произошло изменение кредитов.
    /// </remarks>
    event Action<CCSPlayerController, int, WhoChangeCredits>? CreditsTakePost;
}