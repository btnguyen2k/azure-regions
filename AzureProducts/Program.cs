
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using HttpClient client = new();

const string baseApiUrl = "https://prices.azure.com/api/retail/prices?$filter=armRegionName eq 'eastus' and type eq 'Consumption' and armSkuName ne ''";
var apiUrl = baseApiUrl;
while (true)
{
    var httpResp = await client.GetAsync(apiUrl);
    var jsonResp = await httpResp.Content.ReadFromJsonAsync<RetailPrice>() ?? throw new InvalidOperationException("Failed to read JSON response");
    apiUrl = jsonResp.NextPageLink;
    Console.WriteLine($"{jsonResp.Count}: {apiUrl}");
    if (string.IsNullOrEmpty(apiUrl)) break;
}

internal class PriceItem
{
	[JsonPropertyName("currencyCode")]
	public string CurrencyCode { get; set; } = default!;

	[JsonPropertyName("tierMinimumUnits")]
	public double TierMinimumUnits { get; set; }

	[JsonPropertyName("retailPrice")]
	public double RetailPrice { get; set; }

	[JsonPropertyName("unitPrice")]
	public double UnitPrice { get; set; }

	[JsonPropertyName("armRegionName")]
	public string ArmRegionName { get; set; } = default!;

	[JsonPropertyName("location")]
	public string Location { get; set; } = default!;

	[JsonPropertyName("effectiveStartDate")]
	public DateTimeOffset EffectiveStartDate { get; set; }

	[JsonPropertyName("meterId")]
	public string MeterId { get; set; } = default!;

	[JsonPropertyName("meterName")]
	public string MeterName { get; set; } = default!;

	[JsonPropertyName("productId")]
	public string ProductId { get; set; } = default!;

	[JsonPropertyName("skuId")]
	public string SkuId { get; set; } = default!;

	[JsonPropertyName("productName")]
	public string ProductName { get; set; } = default!;

	[JsonPropertyName("skuName")]
	public string SkuName { get; set; } = default!;

	[JsonPropertyName("serviceName")]
	public string ServiceName { get; set; } = default!;

	[JsonPropertyName("serviceId")]
	public string ServiceId { get; set; } = default!;

	[JsonPropertyName("serviceFamily")]
	public string ServiceFamily { get; set; } = default!;

	[JsonPropertyName("unitOfMeasure")]
	public string UnitOfMeasure { get; set; } = default!;

	[JsonPropertyName("type")]
	public string Type { get; set; } = default!;

	[JsonPropertyName("isPrimaryMeterRegion")]
	public bool IsPrimaryMeterRegion { get; set; }

	[JsonPropertyName("armSkuName")]
	public string ArmSkuName { get; set; } = default!;
}

internal class RetailPrice
{
	[JsonPropertyName("BillingCurrency")]
	public string BillingCurrency { get; set; } = default!;

	[JsonPropertyName("CustomerEntityId")]
	public string CustomerEntityId { get; set; } = default!;

	[JsonPropertyName("CustomerEntityType")]
	public string CustomerEntityType { get; set; } = default!;

	[JsonPropertyName("NextPageLink")]
	public string NextPageLink { get; set; } = default!;

	[JsonPropertyName("Count")]
	public int Count { get; set; } = default!;

	[JsonPropertyName("Items")]
	public List<PriceItem> Items { get; set; } = default!;
}
