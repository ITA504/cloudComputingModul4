using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using DailyProduction.Models;

namespace IbasAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class DailyProductionController : ControllerBase
    {
        private readonly ILogger<DailyProductionController> _logger;
        private readonly List<DailyProductionDTO> _productionRepo;

        public DailyProductionController(ILogger<DailyProductionController> logger)
        {
            _logger = logger;
            var csvPath = Path.Combine(AppContext.BaseDirectory, "Data", "IBASProduction2022.csv");
            _productionRepo = LoadFromCsv(csvPath);
            _logger.LogInformation("Loaded {Count} rows from {Path}", _productionRepo.Count, csvPath);
        }

        [HttpGet]
        public IEnumerable<DailyProductionDTO> Get() => _productionRepo;

        private static List<DailyProductionDTO> LoadFromCsv(string path)
        {
            var list = new List<DailyProductionDTO>();

            if (!System.IO.File.Exists(path))
                throw new FileNotFoundException($"CSV file not found at {path}");

            using var sr = new StreamReader(path);
            string? line;
            bool isHeader = true;

            while ((line = sr.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Split på komma – fallback til whitespace hvis der ikke er kommaer
                string[] parts = line.Split(',');
                if (parts.Length < 5)
                    parts = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s+");

                // Spring headerlinjen over
                if (isHeader)
                {
                    isHeader = false;
                    if (parts.Length >= 2 && parts[0].Contains("PartitionKey", StringComparison.OrdinalIgnoreCase))
                        continue; // normal header
                    // hvis første linje ikke var en header, så lad den parse igennem
                }

                if (parts.Length < 5) continue;

                // Forventet rækkefølge:
                // 0: PartitionKey
                // 1: RowKey (yyyy-MM-ddTHH:mm:ss)  ← vi bruger som "Date"
                // 2: ProductionTime (ISO)          ← ignoreres
                // 3: itemsProduced (int)
                // 4: itemsProduced@type            ← ignoreres

                var pkStr   = parts[0].Trim().Trim('"');
                var rowKey  = parts[1].Trim().Trim('"');
                var itemsStr= parts[3].Trim().Trim('"');

                if (!int.TryParse(pkStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var pk))
                    continue;

                var model = pk switch
                {
                    1 => BikeModel.IBv1,
                    2 => BikeModel.evIB100,
                    3 => BikeModel.evIB200,
                    _ => BikeModel.undefined
                };

                if (!DateTime.TryParse(rowKey, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
                {
                    // sidste forsøg uden tidszone
                    if (!DateTime.TryParse(rowKey, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                        continue;
                }

                if (!int.TryParse(itemsStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var items))
                    continue;

                list.Add(new DailyProductionDTO
                {
                    Date = date,
                    Model = model,
                    ItemsProduced = items
                });
            }

            return list;
        }
    }
}