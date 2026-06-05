using System.IO.Compression;
using System.Text.Json;

var package = @"C:\Users\savas.boluk\Downloads\iis-export-test\IISExport_20260522_100232.iispackage";
var temp = Path.Combine(Path.GetTempPath(), "iisdebug_inspect");

if (Directory.Exists(temp)) Directory.Delete(temp, true);
ZipFile.ExtractToDirectory(package, temp);

Console.WriteLine("=== manifest.json ===");
var manifestPath = Path.Combine(temp, "manifest.json");
var manifestContent = File.ReadAllText(manifestPath);
Console.WriteLine(manifestContent.Substring(0, Math.Min(500, manifestContent.Length)));
Console.WriteLine("...");

// Check offset 18
Console.WriteLine($"\nChars around offset 18: '{manifestContent.Substring(15, 12)}'");
Console.WriteLine($"Byte at offset 18: '{(int)manifestContent[18]}' = '{manifestContent[18]}'");

// Try deserializing manifest 
try {
    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    var manifest = JsonSerializer.Deserialize<JsonElement>(manifestContent, opts);
    Console.WriteLine("Manifest deserialized OK");
} catch (Exception ex) {
    Console.WriteLine($"Manifest deserialization FAILED: {ex.Message}");
}

// Check site.json files
Console.WriteLine("\n=== site.json files ===");
var sitesDir = Path.Combine(temp, "sites");
foreach (var dir in Directory.GetDirectories(sitesDir))
{
    var siteJsonPath = Path.Combine(dir, "site.json");
    if (!File.Exists(siteJsonPath)) continue;
    var content = File.ReadAllText(siteJsonPath);
    Console.WriteLine($"\n--- {Path.GetFileName(dir)} ---");
    Console.WriteLine(content.Substring(0, Math.Min(300, content.Length)));
    Console.WriteLine($"Offset 18 char: '{(int)content[18]}' = '{content[18]}'");
    Console.WriteLine($"Around offset 18: '{content.Substring(Math.Max(0,15), Math.Min(12, content.Length-15))}'");
    
    try {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var obj = JsonSerializer.Deserialize<JsonElement>(content, opts);
        Console.WriteLine("site.json deserialized OK");
    } catch (Exception ex) {
        Console.WriteLine($"site.json deserialization FAILED: {ex.Message}");
    }
}

// Check apppool.json files
Console.WriteLine("\n=== apppool.json files ===");
var poolsDir = Path.Combine(temp, "apppools");
if (Directory.Exists(poolsDir)) {
    foreach (var dir in Directory.GetDirectories(poolsDir))
    {
        var poolPath = Path.Combine(dir, "apppool.json");
        if (!File.Exists(poolPath)) continue;
        var content = File.ReadAllText(poolPath);
        Console.WriteLine($"\n--- {Path.GetFileName(dir)} ---");
        Console.WriteLine(content.Substring(0, Math.Min(300, content.Length)));
        Console.WriteLine($"Offset 18 char: '{(int)content[18]}' = '{content[18]}'");
        Console.WriteLine($"Around offset 18: '{content.Substring(Math.Max(0,15), Math.Min(12, content.Length-15))}'");
        
        try {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var obj = JsonSerializer.Deserialize<JsonElement>(content, opts);
            Console.WriteLine("apppool.json deserialized OK");
        } catch (Exception ex) {
            Console.WriteLine($"apppool.json deserialization FAILED: {ex.Message}");
        }
    }
}
