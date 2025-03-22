
using System.CommandLine;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

async static Task<IDictionary<string, ServiceFamilyRecord>> GetProductCategory(string region)
{
    using HttpClient client = new();
    var serviceFamilyMap = new Dictionary<string, ServiceFamilyRecord>();
    // var apiUrl = $"https://prices.azure.com/api/retail/prices?$filter=armRegionName eq '{region}' and type eq 'Consumption' and armSkuName ne ''";
    var apiUrl = $"https://prices.azure.com/api/retail/prices?$filter=armRegionName eq '{region}' and type eq 'Consumption'";
    Console.WriteLine($"===== Fetching product category from {region}...");
    while (true)
    {
        var httpResp = await client.GetAsync(apiUrl);
        if (!httpResp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"[ERROR: {httpResp.StatusCode}] Failed to fetch product category from {apiUrl}");
        }
        var respBody = await httpResp.Content.ReadAsStringAsync();
        RetailPrice? jsonResp;
        try
        {
            jsonResp = JsonSerializer.Deserialize<RetailPrice>(respBody) ?? throw new InvalidOperationException("Failed to deserialize JSON response");
        }
        catch (JsonException)
        {
            Console.WriteLine($"[DEBUG]: {respBody[..Math.Min(100, respBody.Length)]}");
            throw new InvalidOperationException("Failed to deserialize JSON response");
        }
        foreach (var item in jsonResp.Items)
        {
            if (!serviceFamilyMap.TryGetValue(item.ServiceFamily, out var serviceFamily))
            {
                serviceFamily = new ServiceFamilyRecord { Name = item.ServiceFamily };
                serviceFamilyMap[item.ServiceFamily] = serviceFamily;
            }

            if (!serviceFamily.Services.TryGetValue(item.ServiceId, out var service))
            {
                service = new ServiceRecord { Id = item.ServiceId, Name = item.ServiceName };
                serviceFamily.AddService(service);
            }

            var product = new ProductRecord
            {
                Id = item.ProductId,
                Name = item.ProductName,
                // SkuId = item.SkuId,
                SkuName = item.SkuName,
                // MeterId = item.MeterId,
                MeterName = item.MeterName,
            };
            service.AddProduct(product);
        }
        Console.WriteLine($"Fetched {jsonResp.Count} items from {apiUrl}");
        apiUrl = jsonResp.NextPageLink;
        if (string.IsNullOrEmpty(apiUrl)) break;

        Thread.Sleep(1000); // slow down to avoid throttling
    }

    return serviceFamilyMap;
}

const string DEFAULT_OUTPUT_DIR = "./";
var optOutProductList = new Option<string>(
    aliases: ["-o", "--output-dir"],
    description: "Output directory to store product category",
    getDefaultValue: () => DEFAULT_OUTPUT_DIR
);
const string DEFAULT_BASE_REGIONS = "eastus,westus,centralus,canadacentral,brazilsouth,auseast,japaneast,koreacentral,eastasia,southeastasia,northeurope,westeurope";
var optBaseRegions = new Option<string>(
    aliases: ["-r", "--regions"],
    description: "Comma-separated list of Azure regions to query for product category",
    getDefaultValue: () => DEFAULT_BASE_REGIONS
);
var rootCommand = new RootCommand{
    optOutProductList,
    optBaseRegions
};
var outputDir = rootCommand.Parse(args).GetValueForOption(optOutProductList) ?? DEFAULT_OUTPUT_DIR;
var baseRegions = (rootCommand.Parse(args).GetValueForOption(optBaseRegions) ?? DEFAULT_BASE_REGIONS).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);

/* Fix throttling issue */
if (baseRegions.Length > 10)
{
    // suffle the list and take the first 10
    var rnd = new Random();
    baseRegions = [.. baseRegions.OrderBy(r => rnd.Next()).Take(10)];
}
/* END */

var jsonOpts = new JsonSerializerOptions() { WriteIndented = true };
var allServicesMap = new Dictionary<string, ServiceFamilyRecord>();
foreach (var region in baseRegions)
{
    var servicesMap = await GetProductCategory(region);
    Thread.Sleep(10000); // slow down to avoid throttling
    var servicesList = servicesMap.Values.OrderBy(sf => sf.Name).ToArray();
    var outputData = new Dictionary<string, ServiceFamilyRecord[]> { [region] = servicesList };
    var outFileProductList = Path.Combine(outputDir, $"products-{region}.json");
    await File.WriteAllTextAsync(outFileProductList, JsonSerializer.Serialize(outputData, jsonOpts));

    // merge to the global map
    foreach (var serviceFamily in servicesList)
    {
        if (!allServicesMap.TryGetValue(serviceFamily.Name, out var allServiceFamily))
        {
            allServiceFamily = new ServiceFamilyRecord { Name = serviceFamily.Name };
            allServicesMap[serviceFamily.Name] = allServiceFamily;
        }

        foreach (var service in serviceFamily.Services.Values)
        {
            if (!allServiceFamily.Services.TryGetValue(service.Id, out var allService))
            {
                allService = new ServiceRecord { Id = service.Id, Name = service.Name };
                allServiceFamily.AddService(allService);
            }

            foreach (var product in service.Products.Values)
            {
                allService.AddProduct(product);
            }
        }
    }
}

var allServicesList = allServicesMap.Values.OrderBy(sf => sf.Name).ToArray();
var outFileAllProductList = Path.Combine(outputDir, $"products.json");
await File.WriteAllTextAsync(outFileAllProductList, JsonSerializer.Serialize(allServicesList, jsonOpts));

internal class ProductRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    // [JsonPropertyName("sku_id")]
    // public string SkuId { get; set; } = default!;

    [JsonPropertyName("sku_name")]
    public string SkuName { get; set; } = default!;

    // [JsonPropertyName("meter_id")]
    // public string MeterId { get; set; } = default!;

    [JsonPropertyName("meter_name")]
    public string MeterName { get; set; } = default!;
}

internal class ServiceRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("products")]
    public IDictionary<string, ProductRecord> Products { get; set; } = new Dictionary<string, ProductRecord>();

    public ServiceRecord AddProduct(ProductRecord product)
    {
        Products[$"{product.Id}/{product.SkuName}/{product.MeterName}"] = product;
        return this;
    }
}

internal class ServiceFamilyRecord
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = default!;

    [JsonPropertyName("services")]
    public IDictionary<string, ServiceRecord> Services { get; set; } = new Dictionary<string, ServiceRecord>();

    public ServiceFamilyRecord AddService(ServiceRecord service)
    {
        Services[service.Id] = service;
        return this;
    }
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
