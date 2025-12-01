namespace Pakkeshop.Configuration;

public class GoogleSheetsSettings
{
    public string SpreadsheetId { get; set; } = string.Empty;
    public string SheetName { get; set; } = string.Empty;
    public string CredentialsJson { get; set; } = string.Empty;
}
