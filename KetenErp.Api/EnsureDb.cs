using System;
using System.Linq;
using System.Threading.Tasks;
using KetenErp.Infrastructure.Data;
using KetenErp.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KetenErp.Api
{
    public static class EnsureDb
    {
        public static async Task EnsureAsync(IServiceProvider servicesProvider, IHostEnvironment env)
        {
            using var scope = servicesProvider.CreateScope();
            var services = scope.ServiceProvider;
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var db = services.GetRequiredService<KetenErpDbContext>();

            // Try to apply migrations if available, otherwise fall back to EnsureCreated.
            var migrations = db.Database.GetMigrations();
            if (migrations != null && migrations.Any())
            {
                db.Database.Migrate();
            }
            else
            {
                db.Database.EnsureCreated();
            }

            // If the database file existed previously but is missing Identity tables (partial DB),
            // EnsureCreated won't add missing tables. Do a quick probe and recreate DB if necessary
            // to ensure Identity tables are present for development environments.
            var needsRecreate = false;
            try
            {
                // Try reading from the Roles set; this will throw if the table doesn't exist
                await db.Roles.AnyAsync();
            }
            catch
            {
                needsRecreate = true;
            }

            if (needsRecreate)
            {
                Console.WriteLine("Recreating database because required Identity tables were missing.");
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
            }

            // Additional safety: verify that all expected tables for current model exist.
            try
            {
                using var connCheck = db.Database.GetDbConnection();
                connCheck.Open();
                var expectedTables = new[] { "AspNetUsers", "AspNetRoles", "Products", "SpareParts", "ServiceRecords", "ServiceOperations", "ChangedParts", "ServiceItems", "ServiceRecordPhotos", "SentQuotes", "CompletedServiceRecords", "ServiceTemplates", "Suggestions", "EmailAccounts" };
                var missing = new System.Collections.Generic.List<string>();
                foreach (var tname in expectedTables)
                {
                    using var cmd = connCheck.CreateCommand();
                    cmd.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tname}';";
                    var r = cmd.ExecuteScalar();
                    if (r == null)
                    {
                        missing.Add(tname);
                    }
                }

                if (missing.Count > 0)
                {
                    Console.WriteLine($"Detected missing tables: {string.Join(',', missing)}; recreating database to ensure schema matches EF model.");
                    db.Database.EnsureDeleted();
                    db.Database.EnsureCreated();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not perform schema completeness check: {ex.Message}");
            }

            // Ensure Products table has MinStock and SKU columns
            try
            {
                using var conn = db.Database.GetDbConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA table_info('Products');";
                using var rdr = cmd.ExecuteReader();
                var found = false;
                while (rdr.Read())
                {
                    var name = rdr[1]?.ToString();
                    if (string.Equals(name, "MinStock", StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                rdr.Close();
                if (!found)
                {
                    using var alter = conn.CreateCommand();
                    alter.CommandText = "ALTER TABLE Products ADD COLUMN MinStock INTEGER NOT NULL DEFAULT 0;";
                    alter.ExecuteNonQuery();
                    Console.WriteLine("Added MinStock column to Products table.");
                }

                // Ensure SKU column
                try
                {
                    using var cmd2 = conn.CreateCommand();
                    cmd2.CommandText = "PRAGMA table_info('Products');";
                    using var rdr2 = cmd2.ExecuteReader();
                    var hasSku = false;
                    while (rdr2.Read())
                    {
                        var name = rdr2[1]?.ToString();
                        if (string.Equals(name, "SKU", StringComparison.OrdinalIgnoreCase))
                        {
                            hasSku = true;
                            break;
                        }
                    }
                    rdr2.Close();
                    if (!hasSku)
                    {
                        using var alter2 = conn.CreateCommand();
                        alter2.CommandText = "ALTER TABLE Products ADD COLUMN SKU TEXT;";
                        alter2.ExecuteNonQuery();
                        Console.WriteLine("Added SKU column to Products table.");
                        try
                        {
                            using var backfill = conn.CreateCommand();
                            backfill.CommandText = "UPDATE Products SET SKU = substr(Description, instr(Description, ' | ')+3) WHERE (SKU IS NULL OR SKU = '') AND Description LIKE '% | %';";
                            var updated = backfill.ExecuteNonQuery();
                            if (updated > 0) Console.WriteLine($"Backfilled SKU for {updated} products from Description.");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Could not backfill SKU values: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Could not ensure SKU column exists: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure MinStock column exists: {ex.Message}");
            }

            // Ensure ServiceRecords table has Durum and Notlar columns
            try
            {
                using var conn2 = db.Database.GetDbConnection();
                conn2.Open();
                using var cmdSr = conn2.CreateCommand();
                cmdSr.CommandText = "PRAGMA table_info('ServiceRecords');";
                using var rdrSr = cmdSr.ExecuteReader();
                var foundDurum = false;
                while (rdrSr.Read())
                {
                    var name = rdrSr[1]?.ToString();
                    if (string.Equals(name, "Durum", StringComparison.OrdinalIgnoreCase))
                    {
                        foundDurum = true;
                        break;
                    }
                }
                rdrSr.Close();
                if (!foundDurum)
                {
                    using var alterSr = conn2.CreateCommand();
                    alterSr.CommandText = "ALTER TABLE ServiceRecords ADD COLUMN Durum TEXT NOT NULL DEFAULT 'Kayıt Açıldı';";
                    alterSr.ExecuteNonQuery();
                    Console.WriteLine("Added Durum column to ServiceRecords table.");
                    try
                    {
                        using var backfillSr = conn2.CreateCommand();
                        backfillSr.CommandText = "UPDATE ServiceRecords SET Durum = 'Kayıt Açıldı' WHERE Durum IS NULL OR Durum = '';";
                        var updated = backfillSr.ExecuteNonQuery();
                        if (updated > 0) Console.WriteLine($"Backfilled Durum for {updated} service records.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not backfill Durum values: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure Durum column exists: {ex.Message}");
            }

            try
            {
                using var conn3 = db.Database.GetDbConnection();
                conn3.Open();
                using var cmdNot = conn3.CreateCommand();
                cmdNot.CommandText = "PRAGMA table_info('ServiceRecords');";
                using var rdrNot = cmdNot.ExecuteReader();
                var foundNot = false;
                while (rdrNot.Read())
                {
                    var name = rdrNot[1]?.ToString();
                    if (string.Equals(name, "Notlar", StringComparison.OrdinalIgnoreCase))
                    {
                        foundNot = true;
                        break;
                    }
                }
                rdrNot.Close();
                if (!foundNot)
                {
                    using var alterNot = conn3.CreateCommand();
                    alterNot.CommandText = "ALTER TABLE ServiceRecords ADD COLUMN Notlar TEXT;";
                    alterNot.ExecuteNonQuery();
                    Console.WriteLine("Added Notlar column to ServiceRecords table.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure Notlar column exists: {ex.Message}");
            }

            // Ensure CompletedServiceRecords table exists
            try
            {
                using var conn = db.Database.GetDbConnection();
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"CREATE TABLE IF NOT EXISTS CompletedServiceRecords (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            OriginalServiceRecordId INTEGER,
            BelgeNo TEXT,
            ServisTakipNo TEXT,
            FirmaIsmi TEXT,
            UrunModeli TEXT,
            GelisTarihi TEXT,
            CompletedAt TEXT NOT NULL,
            SerializedRecordJson TEXT
        );";
                cmd.ExecuteNonQuery();
                Console.WriteLine("Ensured CompletedServiceRecords table exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure CompletedServiceRecords table exists: {ex.Message}");
            }

            // Ensure ServiceTemplates table exists
            try
            {
                using var connTmp = db.Database.GetDbConnection();
                connTmp.Open();
                using var cmdTmp = connTmp.CreateCommand();
                cmdTmp.CommandText = @"CREATE TABLE IF NOT EXISTS ServiceTemplates (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            ProductSKU TEXT,
            ChangedPartsJson TEXT,
            ServiceItemsJson TEXT,
            YapanKisi TEXT,
            CreatedAt TEXT NOT NULL
        );";
                cmdTmp.ExecuteNonQuery();
                Console.WriteLine("Ensured ServiceTemplates table exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure ServiceTemplates table exists: {ex.Message}");
            }

            // Ensure SentQuotes table has SenderName column (added later) - if missing, ALTER TABLE ADD COLUMN
            try
            {
                using var connSq = db.Database.GetDbConnection();
                connSq.Open();
                using var cmdSq = connSq.CreateCommand();
                cmdSq.CommandText = "PRAGMA table_info('SentQuotes');";
                using var rdrSq = cmdSq.ExecuteReader();
                var hasSenderName = false;
                while (rdrSq.Read())
                {
                    var name = rdrSq[1]?.ToString();
                    if (string.Equals(name, "SenderName", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSenderName = true;
                        break;
                    }
                }
                rdrSq.Close();
                if (!hasSenderName)
                {
                    using var alter = connSq.CreateCommand();
                    alter.CommandText = "ALTER TABLE SentQuotes ADD COLUMN SenderName TEXT;";
                    alter.ExecuteNonQuery();
                    Console.WriteLine("Added SenderName column to SentQuotes table.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure SenderName column on SentQuotes: {ex.Message}");
            }

            // Ensure Customers table exists
            try
            {
                using var connCust = db.Database.GetDbConnection();
                connCust.Open();
                using var cmdCust = connCust.CreateCommand();
                cmdCust.CommandText = @"CREATE TABLE IF NOT EXISTS Customers (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Email TEXT
        );";
                cmdCust.ExecuteNonQuery();
                Console.WriteLine("Ensured Customers table exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure Customers table exists: {ex.Message}");
            }

            // Ensure Suggestions table exists
            try
            {
                using var connSug = db.Database.GetDbConnection();
                connSug.Open();
                using var cmdSug = connSug.CreateCommand();
                cmdSug.CommandText = @"CREATE TABLE IF NOT EXISTS Suggestions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Key TEXT NOT NULL,
            Value TEXT NOT NULL,
            SortOrder INTEGER,
            CreatedAt TEXT NOT NULL,
            CreatedBy TEXT
        );";
                cmdSug.ExecuteNonQuery();
                Console.WriteLine("Ensured Suggestions table exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure Suggestions table exists: {ex.Message}");
            }

            // Ensure EmailAccounts table exists
            try
            {
                using var connEmail = db.Database.GetDbConnection();
                connEmail.Open();
                using var cmdEmail = connEmail.CreateCommand();
                cmdEmail.CommandText = @"CREATE TABLE IF NOT EXISTS EmailAccounts (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL,
            Host TEXT NOT NULL,
            Port INTEGER NOT NULL DEFAULT 587,
            UserName TEXT,
            EncryptedPassword TEXT,
            FromAddress TEXT NOT NULL,
            UseTls INTEGER NOT NULL DEFAULT 1,
            IsActive INTEGER NOT NULL DEFAULT 0,
            CreatedBy TEXT,
            CreatedAt TEXT NOT NULL
        );";
                cmdEmail.ExecuteNonQuery();
                Console.WriteLine("Ensured EmailAccounts table exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure EmailAccounts table exists: {ex.Message}");
            }

            // Ensure RefreshTokens table exists
            try
            {
                using var connRt = db.Database.GetDbConnection();
                connRt.Open();
                using var cmdRt = connRt.CreateCommand();
                cmdRt.CommandText = @"CREATE TABLE IF NOT EXISTS RefreshTokens (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Token TEXT NOT NULL,
            UserId TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            RevokedAt TEXT,
            ReplacedByToken TEXT
        );";
                cmdRt.ExecuteNonQuery();
                Console.WriteLine("Ensured RefreshTokens table exists.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not ensure RefreshTokens table exists: {ex.Message}");
            }

            string[] roles = new[] { "admin", "servis", "muhasebe", "user" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            async Task EnsureUser(string userName, string email, string pwd, string role, string? fullName = null)
            {
                var existing = await userManager.FindByNameAsync(userName);
                if (existing == null)
                {
                    var u = new ApplicationUser
                    {
                        UserName = userName,
                        Email = email,
                        FullName = fullName,
                        LockoutEnabled = false
                    };
                    var res = await userManager.CreateAsync(u, pwd);
                    if (res.Succeeded)
                    {
                        await userManager.AddToRoleAsync(u, role);
                        u.EmailConfirmed = true;
                        u.LockoutEnabled = false;
                        await userManager.UpdateAsync(u);
                    }
                }
            }

            await EnsureUser("admin", "admin@keten.local", "admin123", "admin", "System Administrator");
            await EnsureUser("servis", "servis@keten.local", "Servis123!", "servis", "Servis User");
            await EnsureUser("muhasebe", "muhasebe@keten.local", "Muhasebe123!", "muhasebe", "Muhasebe User");
            await EnsureUser("user", "user@keten.local", "User123!", "user", "Normal User");
        }
    }
}
