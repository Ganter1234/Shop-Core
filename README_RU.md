[<kbd><br>en English README<br><br></kbd>](./README.md)

# Shop Core
Система магазина где можно купить какие либо предметы за кредиты
Имеется поддержка модульности

# Команды
```
css_shop - Главное меню магазина (По умолчанию)

// Данные команды доступны флагу который указан в параметре "AdminFlag"
css_add_credits - Добавить кредиты игроку (Пример css_add_credits <ник/юзер_айди/стим_айди2/#стим_айди64> <кол-во кредитов>)
css_set_credits - Установить кредиты игроку (Пример css_set_credits <ник/юзер_айди/стим_айди2/#стим_айди64> <кол-во кредитов>)
css_take_credits - Отобрать кредиты у игрока (Пример css_take_credits <ник/юзер_айди/стим_айди2/#стим_айди64> <кол-во кредитов>)
css_add_item - Добавить предмет игроку (Пример css_add_item <ник/юзер_айди/стим_айди2/#стим_айди64> <уникальное_имя_предмета> <длительность/количество>)
css_take_item - Отобрать предмет у игрока (Пример css_take_item <ник/юзер_айди/стим_айди2/#стим_айди64> <уникальное_имя_предмета> [количество])
```

# Конфиг
```json
{
	"DatabaseHost": "" // Хост базы данных
	"DatabasePort": 3306 // Порт базы данных
	"DatabaseUser": "" /// Пользователь базы данных
	"DatabasePassword": "" // Пароль от пользователя базы данных
	"DatabaseName": "" // Название базы данных
	"Commands": "css_shop;css_store" // Команды для открытия главного меню магазина
	"UseCenterMenu": false // Использовать CenterMenu или ChatMenu
	"StartCredits": 0 // Стартовое количество кредитов у игрока
	"TransCreditsPercent": 5 // Процент комиссии при передачи кредитов (-1 выключить передачу)
	"AdminFlag": "@css/root" // Флаг который нужен игроку для прав администратора
}
```

# Установка
Установить [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) и [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases)  
Установить [MenuManager](https://github.com/Stimayk/MenuManagerCS2) для работы WASD меню (Необязательно)  
Скопировать содержимое архива в папку /game/csgo/addons/counterstrikesharp  
Отредактировать конфиг под себя  
Перезапустить сервер или принудительно загрузить плагин с помощью команды css_plugins load