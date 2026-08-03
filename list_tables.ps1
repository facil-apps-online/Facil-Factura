$conn = New-Object -ComObject ADODB.Connection
$conn.Open("Provider=Microsoft.Jet.OLEDB.4.0;Data Source=C:\Desarrollos\FacilFactura\Validador_rips_ips\Support\Ref_rips.mdb")
$schema = $conn.OpenSchema(20)
while(-not $schema.EOF) {
    if ($schema.Fields.Item('TABLE_TYPE').Value -eq 'TABLE') {
        Write-Output $schema.Fields.Item('TABLE_NAME').Value
    }
    $schema.MoveNext()
}
$conn.Close()
