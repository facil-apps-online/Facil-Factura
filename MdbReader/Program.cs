using System;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Text.Json;

class Program
{
    static void Main()
    {
        string dbPath = @"C:\Desarrollos\FacilFactura\Validador_rips_ips\Support\Ref_rips.mdb";
        string connStr = $@"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={dbPath};";

        try
        {
            using (var conn = new OleDbConnection(connStr))
            {
                conn.Open();
                Console.WriteLine("Conexión exitosa a Ref_rips.mdb");

                // Dump CUPS to JSON
                var cupsList = new System.Collections.Generic.List<object>();
                using (var cmd = new OleDbCommand("SELECT * FROM CUPS", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var obj = new System.Collections.Generic.Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            obj[reader.GetName(i)] = reader[i] == DBNull.Value ? null : reader[i];
                        }
                        cupsList.Add(obj);
                    }
                }
                string outDir = @"C:\Desarrollos\FacilFactura\SeedData";
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "cups.json"), JsonSerializer.Serialize(cupsList, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"CUPS exportados: {cupsList.Count} registros.");

                // Dump CIE10 to JSON
                var cie10List = new System.Collections.Generic.List<object>();
                using (var cmd = new OleDbCommand("SELECT * FROM R_CIE10", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var obj = new System.Collections.Generic.Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            obj[reader.GetName(i)] = reader[i] == DBNull.Value ? null : reader[i];
                        }
                        cie10List.Add(obj);
                    }
                }
                File.WriteAllText(Path.Combine(outDir, "cie10.json"), JsonSerializer.Serialize(cie10List, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"CIE10 exportados: {cie10List.Count} registros.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
