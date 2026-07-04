[<kbd><br>🇷🇺 Russian README<br><br></kbd>](./README_RU.md)

# Shop Core
A shop system where you can buy items with credits
Modularity is supported

# Commands
```
css_shop - Main shop menu (Default)

// These commands are available to the user specified by the “AdminFlag” parameter
css_add_credits - Add credits to a player (Example: css_add_credits <nickname/user_id/Steam_ID2/#Steam_ID64> <number of credits>)
css_set_credits - Set credits for a player (Example: css_set_credits <nickname/user_id/Steam_ID2/#Steam_ID64> <number_of_credits>)
css_take_credits - Take credits from a player (Example: css_take_credits <nickname/user_id/Steam_ID2/#Steam_ID64> <number_of_credits>)
css_add_item - Add an item to a player (Example: css_add_item <nickname/user_id/Steam_ID2/#Steam_ID64> <unique_item_name> <duration/amount>)
css_take_item - Take an item from a player (Example: css_take_item <nickname/user_id/Steam_ID2/#Steam_ID64> <unique_item_name> [amount])
``` 

# Config
```json
{
    “DatabaseHost”: “” // Database host
    “DatabasePort”: 3306 // Database port
    “DatabaseUser”: “” /// Database user
    “DatabasePassword”: “” // Database user password
	“DatabaseName”: “” // Database name
    “Commands”: “css_shop;css_store” // Commands to open the main store menu
    “UseCenterMenu”: false // Use CenterMenu or ChatMenu
	“StartCredits”: 0 // Player's starting number of credits
    “TransCreditsPercent”: 5 // Commission percentage for credit transfers (-1 to disable transfers)
    “AdminFlag”: “@css/root” // Flag required for the player to have admin privileges
}
```

# Installation
Install [Metamod:Source](https://www.sourcemm.net/downloads.php?branch=dev) and [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases)  
Install [MenuManager] (https://github.com/Stimayk/MenuManagerCS2) to enable the WASD menu (Optional)  
Copy the contents of the archive to the /game/csgo/addons/counterstrikesharp folder
Edit the config file to suit your needs
Restart the server or force-load the plugin using the command css_plugins load