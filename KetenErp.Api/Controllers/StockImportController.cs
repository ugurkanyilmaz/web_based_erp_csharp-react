using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using KetenErp.Core.Repositories;
using KetenErp.Core.Entities;
using System.Text.RegularExpressions;

namespace KetenErp.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockImportController : ControllerBase
    {
        private readonly IProductRepository _productRepo;
        private readonly ISparePartRepository _sparePartRepo;

        public StockImportController(IProductRepository productRepo, ISparePartRepository sparePartRepo)
        {
            _productRepo = productRepo;
            _sparePartRepo = sparePartRepo;
        }

        // Parse stock strings coming from Excel. Handles formats like "11,00", "1.100,00", "1,100.00" etc.
        private int ParseStock(string? stockText)
        {
            if (string.IsNullOrWhiteSpace(stockText))
                return 0;

            stockText = stockText.Trim();
            decimal value;
            var styles = System.Globalization.NumberStyles.Number;

            // 1) Try invariant (dot as decimal)
            if (decimal.TryParse(stockText, styles, System.Globalization.CultureInfo.InvariantCulture, out value))
                return (int)Math.Round(value);

            // 2) Try Turkish (comma as decimal)
            try
            {
                var tr = new System.Globalization.CultureInfo("tr-TR");
                if (decimal.TryParse(stockText, styles, tr, out value))
                    return (int)Math.Round(value);
            }
            catch { }

            // 3) Clean non-digit except separators then retry
            var cleaned = System.Text.RegularExpressions.Regex.Replace(stockText, "[^\\d\\.,\\-]", "");
            if (decimal.TryParse(cleaned, styles, System.Globalization.CultureInfo.InvariantCulture, out value))
                return (int)Math.Round(value);
            try
            {
                var tr = new System.Globalization.CultureInfo("tr-TR");
                if (decimal.TryParse(cleaned, styles, tr, out value))
                    return (int)Math.Round(value);
            }
            catch { }

            // 4) Fallback - strip non-digits and parse as integer
            var digitsOnly = System.Text.RegularExpressions.Regex.Replace(stockText, "[^\\d\\-]", "");
            if (int.TryParse(digitsOnly, out int intVal))
                return intVal;

            return 0;
        }

        [HttpPost("products")]
        public async Task<IActionResult> ImportProducts(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Dosya yüklenmedi." });

            var fileName = file.FileName.ToLowerInvariant();
            if (!fileName.EndsWith(".xlsx") && !fileName.EndsWith(".xls"))
                return BadRequest(new { message = "Sadece .xlsx veya .xls dosyaları desteklenmektedir." });

            try
            {
                // Set EPPlus license context
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                var imported = 0;
                var updated = 0;
                var errors = new List<string>();

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    using (var package = new ExcelPackage(stream))
                    {
                        if (package.Workbook.Worksheets.Count == 0)
                            return BadRequest(new { message = "Excel dosyasında sayfa bulunamadı." });

                        // Try to find a worksheet with data (check all worksheets)
                        ExcelWorksheet? worksheet = null;
                        foreach (var ws in package.Workbook.Worksheets)
                        {
                            if (ws.Dimension != null && ws.Dimension.Rows > 0)
                            {
                                worksheet = ws;
                                break;
                            }
                        }

                        if (worksheet == null)
                            return BadRequest(new { message = "Excel dosyasında veri bulunamadı." });

                        // Find header row (search first 5 rows for "Kart Kodu" or "SKU")
                        int headerRow = 0;
                        int skuCol = 0, descCol = 0, stockCol = 0;

                        for (int row = 1; row <= Math.Min(5, worksheet.Dimension.Rows); row++)
                        {
                            for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                            {
                                var cellValue = worksheet.Cells[row, col].Text?.Trim() ?? "";
                                if (cellValue.Contains("Kart Kodu", StringComparison.OrdinalIgnoreCase) || 
                                    cellValue.Equals("SKU", StringComparison.OrdinalIgnoreCase))
                                {
                                    headerRow = row;
                                    skuCol = col;
                                }
                                else if (cellValue.Contains("Açıklama", StringComparison.OrdinalIgnoreCase) ||
                                         cellValue.Contains("Başlık", StringComparison.OrdinalIgnoreCase))
                                {
                                    descCol = col;
                                }
                                else if (cellValue.Contains("Fiili Stok", StringComparison.OrdinalIgnoreCase) ||
                                         cellValue.Contains("Stok", StringComparison.OrdinalIgnoreCase))
                                {
                                    stockCol = col;
                                }
                            }
                            if (headerRow > 0 && skuCol > 0) break;
                        }

                        if (headerRow == 0 || skuCol == 0)
                        {
                            errors.Add("Excel dosyasında 'Kart Kodu' sütunu bulunamadı.");
                            return BadRequest(new { message = "Geçersiz Excel formatı", errors });
                        }

                        // Process data rows
                        for (int row = headerRow + 1; row <= worksheet.Dimension.Rows; row++)
                        {
                            try
                            {
                                var sku = worksheet.Cells[row, skuCol].Text?.Trim();
                                if (string.IsNullOrWhiteSpace(sku)) continue;

                                var description = descCol > 0 ? worksheet.Cells[row, descCol].Text?.Trim() : "";
                                var stockText = stockCol > 0 ? worksheet.Cells[row, stockCol].Text?.Trim() : "0";
                                var stock = ParseStock(stockText);

                                // Check if product exists
                                var existingProducts = await _productRepo.GetAllAsync();
                                var existing = existingProducts.FirstOrDefault(p => 
                                    (p.SKU ?? "").Equals(sku, StringComparison.OrdinalIgnoreCase));

                                if (existing != null)
                                {
                                    // Update existing
                                    existing.Stock = stock;
                                    if (!string.IsNullOrWhiteSpace(description))
                                        existing.Name = description;
                                    await _productRepo.UpdateAsync(existing);
                                    updated++;
                                }
                                else
                                {
                                    // Create new
                                    var newProduct = new Product
                                    {
                                        SKU = sku,
                                        Name = description ?? sku,
                                        Description = description ?? "",
                                        Stock = stock,
                                        MinStock = 0,
                                        Price = 0m
                                    };
                                    await _productRepo.AddAsync(newProduct);
                                    imported++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"Satır {row}: {ex.Message}");
                            }
                        }
                    }
                }

                return Ok(new 
                { 
                    message = "İçe aktarma tamamlandı",
                    imported,
                    updated,
                    errors = errors.Count > 0 ? errors : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "İçe aktarma hatası", error = ex.Message });
            }
        }

        [HttpPost("spareparts")]
        public async Task<IActionResult> ImportSpareParts(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Dosya yüklenmedi." });

            var fileName = file.FileName.ToLowerInvariant();
            if (!fileName.EndsWith(".xlsx") && !fileName.EndsWith(".xls"))
                return BadRequest(new { message = "Sadece .xlsx veya .xls dosyaları desteklenmektedir." });

            try
            {
                ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

                var imported = 0;
                var updated = 0;
                var errors = new List<string>();

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    stream.Position = 0;

                    using (var package = new ExcelPackage(stream))
                    {
                        if (package.Workbook.Worksheets.Count == 0)
                            return BadRequest(new { message = "Excel dosyasında sayfa bulunamadı." });

                        // Try to find a worksheet with data (check all worksheets)
                        ExcelWorksheet? worksheet = null;
                        foreach (var ws in package.Workbook.Worksheets)
                        {
                            if (ws.Dimension != null && ws.Dimension.Rows > 0)
                            {
                                worksheet = ws;
                                break;
                            }
                        }

                        if (worksheet == null)
                            return BadRequest(new { message = "Excel dosyasında veri bulunamadı." });

                        // Find header row
                        int headerRow = 0;
                        int skuCol = 0, descCol = 0, stockCol = 0;

                        for (int row = 1; row <= Math.Min(5, worksheet.Dimension.Rows); row++)
                        {
                            for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                            {
                                var cellValue = worksheet.Cells[row, col].Text?.Trim() ?? "";
                                if (cellValue.Contains("Kart Kodu", StringComparison.OrdinalIgnoreCase) || 
                                    cellValue.Equals("SKU", StringComparison.OrdinalIgnoreCase))
                                {
                                    headerRow = row;
                                    skuCol = col;
                                }
                                else if (cellValue.Contains("Açıklama", StringComparison.OrdinalIgnoreCase) ||
                                         cellValue.Contains("Başlık", StringComparison.OrdinalIgnoreCase))
                                {
                                    descCol = col;
                                }
                                else if (cellValue.Contains("Fiili Stok", StringComparison.OrdinalIgnoreCase) ||
                                         cellValue.Contains("Stok", StringComparison.OrdinalIgnoreCase))
                                {
                                    stockCol = col;
                                }
                            }
                            if (headerRow > 0 && skuCol > 0) break;
                        }

                        if (headerRow == 0 || skuCol == 0)
                        {
                            errors.Add("Excel dosyasında 'Kart Kodu' sütunu bulunamadı.");
                            return BadRequest(new { message = "Geçersiz Excel formatı", errors });
                        }

                        // Get all products for matching
                        var allProducts = await _productRepo.GetAllAsync();

                        // Process data rows
                        for (int row = headerRow + 1; row <= worksheet.Dimension.Rows; row++)
                        {
                            try
                            {
                                var kartKodu = worksheet.Cells[row, skuCol].Text?.Trim();
                                if (string.IsNullOrWhiteSpace(kartKodu)) continue;

                                var description = descCol > 0 ? worksheet.Cells[row, descCol].Text?.Trim() : "";
                                var stockText = stockCol > 0 ? worksheet.Cells[row, stockCol].Text?.Trim() : "0";
                                var stock = ParseStock(stockText);

                                // Parse SKU format: "A10-M15C21P14" where P14 is part number
                                // Pattern: look for last occurrence of "P" followed by digits
                                var match = Regex.Match(kartKodu, @"^(.+?)P(\d+)$", RegexOptions.IgnoreCase);
                                
                                string productSku = "";
                                string partNumber = "";
                                
                                if (match.Success)
                                {
                                    productSku = match.Groups[1].Value.TrimEnd('-');
                                    partNumber = "P" + match.Groups[2].Value;
                                }
                                else
                                {
                                    // No P pattern found - treat whole string as independent part
                                    partNumber = kartKodu;
                                }

                                // Find matching product by SKU prefix (ignore dashes/hyphens)
                                Product? matchedProduct = null;
                                if (!string.IsNullOrWhiteSpace(productSku))
                                {
                                    var normalizedProductSku = productSku.Replace("-", "").Replace(" ", "");
                                    matchedProduct = allProducts.FirstOrDefault(p => 
                                        (p.SKU ?? "").Replace("-", "").Replace(" ", "").Equals(normalizedProductSku, StringComparison.OrdinalIgnoreCase));
                                }

                                // Check if spare part exists
                                var existingParts = await _sparePartRepo.GetAllAsync();
                                var existing = existingParts.FirstOrDefault(sp => 
                                    (sp.PartNumber ?? "").Equals(partNumber, StringComparison.OrdinalIgnoreCase) &&
                                    (matchedProduct == null || sp.ProductId == matchedProduct.Id));

                                if (existing != null)
                                {
                                    // Update existing
                                    existing.Stock = stock;
                                    if (!string.IsNullOrWhiteSpace(description))
                                        existing.Title = description;
                                    await _sparePartRepo.UpdateAsync(existing);
                                    updated++;
                                }
                                else
                                {
                                    // Create new spare part
                                    var newPart = new SparePart
                                    {
                                        SKU = matchedProduct?.SKU ?? null,
                                        PartNumber = partNumber,
                                        Title = description ?? partNumber,
                                        ProductId = matchedProduct?.Id,
                                        Stock = stock,
                                        MinStock = 0
                                    };
                                    await _sparePartRepo.AddAsync(newPart);
                                    imported++;
                                }
                            }
                            catch (Exception ex)
                            {
                                errors.Add($"Satır {row}: {ex.Message}");
                            }
                        }
                    }
                }

                return Ok(new 
                { 
                    message = "Yedek parça içe aktarımı tamamlandı",
                    imported,
                    updated,
                    errors = errors.Count > 0 ? errors : null
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "İçe aktarma hatası", error = ex.Message });
            }
        }
    }
}
