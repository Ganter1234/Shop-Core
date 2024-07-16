using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace Shop
{
	public class ShopConfig : BasePluginConfig
	{
		[JsonPropertyName("DatabaseHost")]
		public string DatabaseHost { get; set; } = "";

		[JsonPropertyName("DatabasePort")]
		public int DatabasePort { get; set; } = 3306;

		[JsonPropertyName("DatabaseUser")]
		public string DatabaseUser { get; set; } = "";

		[JsonPropertyName("DatabasePassword")]
		public string DatabasePassword { get; set; } = "";

		[JsonPropertyName("DatabaseName")]
		public string DatabaseName { get; set; } = "";

		[JsonPropertyName("Commands")]
		public string Commands { get; set; } = "css_shop;css_store";

		[JsonPropertyName("UseCenterMenu")]
		public bool UseCenterMenu { get; set; } = false;

		[JsonPropertyName("StartCredits")]
		public int StartCredits { get; set; } = 0;
		
		[JsonPropertyName("TransCreditsPercent")]
		public int TransCreditsPercent { get; set; } = 5;

		[JsonPropertyName("AdminFlag")]
		public string AdminFlag { get; set; } = "@css/root";
	}
}