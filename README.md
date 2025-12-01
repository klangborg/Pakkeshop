# Pakkeshop - Automatisk Email til Google Sheets Integration

Pakkeshop er en Azure Function der automatisk:
1. Læser ulæste emails fra en IMAP inbox hver time
2. Udtrækker pakkeoplysninger via Azure OpenAI
3. Indsætter data i et Google Sheet
4. Sletter emailen efter behandling

## Funktionalitet

**Email Processing:**
- Timer trigger der kører hver time (CRON: `0 0 */1 * * *`)
- Henter kun ulæste emails via IMAP
- Sletter emails efter succesfuld behandling

**Data Extraction via Azure OpenAI:**
- Pakkenummer
- Distributør (dao, gls, postnord, bring)
- Pickup code (valgfrit)
- Sidste afhentningsdag (valgfrit)

**Google Sheets Integration:**
- Indsætter data som nye rækker
- Inkluderer timestamp for når data blev behandlet
- Retry logik (3 forsøg ved fejl)

## Lokal Udvikling

### Forudsætninger

- .NET 8.0 SDK
- Azure Functions Core Tools
- Visual Studio 2022 eller VS Code

### Konfiguration

Opdater `local.settings.json` med dine credentials:

```json
{
  "Values": {
    "Email:ImapServer": "mail.simply.com",
    "Email:ImapPort": "143",
    "Email:Username": "din-email@domain.dk",
    "Email:Password": "dit-password",
    "Email:UseSsl": "true",

    "OpenAI:Endpoint": "https://your-resource.openai.azure.com/",
    "OpenAI:ApiKey": "din-api-key",
    "OpenAI:DeploymentName": "gpt-4",
    "OpenAI:MaxTokens": "1000",

    "GoogleSheets:SpreadsheetId": "dit-spreadsheet-id",
    "GoogleSheets:SheetName": "Sheet1",
    "GoogleSheets:CredentialsJson": "{...service account json...}"
  }
}
```

### Kør Lokalt

```bash
dotnet restore
dotnet build
func start
```

## Azure Deployment

### 1. Opret Azure Resources

#### A. Opret Resource Group
```bash
az group create --name pakkeshop-rg --location westeurope
```

#### B. Opret Storage Account
```bash
az storage account create \
  --name pakkeshopstorage \
  --resource-group pakkeshop-rg \
  --location westeurope \
  --sku Standard_LRS
```

#### C. Opret Function App
```bash
az functionapp create \
  --name pakkeshop-function \
  --resource-group pakkeshop-rg \
  --storage-account pakkeshopstorage \
  --consumption-plan-location westeurope \
  --runtime dotnet-isolated \
  --runtime-version 8 \
  --functions-version 4 \
  --os-type Windows
```

### 2. Konfigurer Application Settings

Du skal konfigurere alle settings i Azure Portal eller via Azure CLI.

#### Via Azure Portal:

1. Gå til din Function App i [Azure Portal](https://portal.azure.com)
2. Vælg **Configuration** under **Settings**
3. Klik **New application setting** for hver af følgende:

**Email Settings:**
- `Email:ImapServer` = `mail.simply.com` (eller din IMAP server)
- `Email:ImapPort` = `143` (eller `993` for SSL)
- `Email:Username` = `din-email@domain.dk`
- `Email:Password` = `dit-email-password`
- `Email:UseSsl` = `true`

**Azure OpenAI Settings:**
- `OpenAI:Endpoint` = `https://your-resource.openai.azure.com/`
- `OpenAI:ApiKey` = `din-azure-openai-api-key`
- `OpenAI:DeploymentName` = `gpt-4` (eller dit deployment navn)
- `OpenAI:MaxTokens` = `1000`

**Google Sheets Settings:**
- `GoogleSheets:SpreadsheetId` = `dit-spreadsheet-id`
- `GoogleSheets:SheetName` = `Sheet1` (eller dit sheet navn)
- `GoogleSheets:CredentialsJson` = `{...din service account JSON...}`

4. Klik **Save** og derefter **Continue**

#### Via Azure CLI:

```bash
# Email Settings
az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "Email:ImapServer=mail.simply.com"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "Email:ImapPort=143"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "Email:Username=din-email@domain.dk"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "Email:Password=dit-password"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "Email:UseSsl=true"

# OpenAI Settings
az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "OpenAI:Endpoint=https://your-resource.openai.azure.com/"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "OpenAI:ApiKey=din-api-key"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "OpenAI:DeploymentName=gpt-4"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "OpenAI:MaxTokens=1000"

# Google Sheets Settings
az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "GoogleSheets:SpreadsheetId=dit-spreadsheet-id"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "GoogleSheets:SheetName=Sheet1"

az functionapp config appsettings set --name pakkeshop-function --resource-group pakkeshop-rg \
  --settings "GoogleSheets:CredentialsJson={...json...}"
```

### 3. Deploy Function

#### Via Visual Studio:
1. Højreklik på projektet i Solution Explorer
2. Vælg **Publish**
3. Vælg **Azure** og klik **Next**
4. Vælg **Azure Function App (Windows)** og klik **Next**
5. Vælg din Function App og klik **Finish**
6. Klik **Publish**

#### Via Azure CLI:
```bash
# Fra projektets rod-mappe
func azure functionapp publish pakkeshop-function
```

#### Via GitHub Actions (CI/CD):
Se [Azure Functions deployment dokumentation](https://learn.microsoft.com/en-us/azure/azure-functions/functions-how-to-github-actions)

### 4. Verificer Deployment

1. Gå til din Function App i Azure Portal
2. Vælg **Functions** under **Functions**
3. Du skulle se **EmailProcessor** funktionen
4. Klik på funktionen og vælg **Monitor** for at se execution logs
5. Vent til næste time hvor funktionen kører automatisk, eller trigger den manuelt

## Google Sheets Setup

### 1. Opret Service Account

1. Gå til [Google Cloud Console](https://console.cloud.google.com/)
2. Opret nyt projekt (fx "Pakkeshop")
3. Gå til **APIs & Services** → **Library**
4. Søg efter og aktiver **Google Sheets API**
5. Gå til **APIs & Services** → **Credentials**
6. Klik **Create Credentials** → **Service Account**
7. Udfyld navn (fx "pakkeshop-service") og klik **Create**
8. Skip permissions og klik **Done**
9. Klik på den nye service account
10. Gå til **Keys** tab
11. Klik **Add Key** → **Create new key** → **JSON**
12. Download JSON filen

### 2. Giv Service Account Adgang til Sheet

1. Åbn dit Google Sheet
2. Klik **Share**
3. Indsæt service account email (fra JSON: `client_email` feltet)
   - Eksempel: `pakkeshop@pakkeshop-479906.iam.gserviceaccount.com`
4. Giv **Editor** rettigheder
5. Fjern flueben ved "Notify people"
6. Klik **Share**

### 3. Find Spreadsheet ID

Spreadsheet ID findes i URL'en:
```
https://docs.google.com/spreadsheets/d/[SPREADSHEET_ID]/edit
```

### 4. Forbered Credentials JSON

Du skal escape JSON'en til en enkelt linje. Brug et værktøj som:
- [FreeFormatter JSON Escape](https://www.freeformatter.com/json-escape.html)

Eller i PowerShell:
```powershell
$json = Get-Content "path\to\credentials.json" -Raw
$escaped = $json -replace '"', '\"' -replace '\n', '\n' -replace '\r', ''
Write-Output $escaped
```

## Azure OpenAI Setup

### 1. Opret Azure OpenAI Resource

1. Gå til [Azure Portal](https://portal.azure.com)
2. Søg efter **Azure OpenAI**
3. Klik **Create**
4. Udfyld:
   - Resource group: vælg eller opret ny
   - Region: fx West Europe
   - Name: fx `pakkeshop-openai`
   - Pricing tier: Standard S0
5. Klik **Review + create** og derefter **Create**

### 2. Deploy Model

1. Gå til din Azure OpenAI resource
2. Klik **Model deployments** (eller gå til Azure OpenAI Studio)
3. Klik **Create new deployment**
4. Vælg model: **gpt-4** eller **gpt-35-turbo**
5. Giv deployment et navn (fx `gpt-4`)
6. Klik **Create**

### 3. Hent Credentials

1. I Azure Portal, gå til din Azure OpenAI resource
2. Vælg **Keys and Endpoint** under **Resource Management**
3. Kopier:
   - **Endpoint** (fx `https://pakkeshop-openai.openai.azure.com/`)
   - **Key 1** eller **Key 2**
4. Brug disse i Application Settings som `OpenAI:Endpoint` og `OpenAI:ApiKey`

## Monitoring

### Application Insights

Function App har automatisk Application Insights integration.

**Se logs:**
1. Gå til Function App i Azure Portal
2. Vælg **Application Insights** under **Settings**
3. Klik **View Application Insights data**
4. Vælg **Logs** og kør queries, fx:

```kusto
traces
| where message contains "Email processor"
| order by timestamp desc
| take 50
```

### Alerts

Opret alerts for fejl:
1. Gå til Application Insights
2. Vælg **Alerts** under **Monitoring**
3. Klik **Create** → **Alert rule**
4. Konfigurer conditions (fx failures > 5 in 5 minutes)
5. Tilføj action group for notifications

## Fejlsøgning

### Almindelige Problemer

**IMAP Connection Fejl:**
- Verificer IMAP server, port og credentials
- Check om UseSsl er korrekt sat (true for port 993, false for 143)
- Test IMAP adgang manuelt med en email client

**OpenAI API Fejl:**
- Verificer endpoint URL (skal slutte med /)
- Check API key er korrekt
- Verificer deployment name matcher din faktiske deployment
- Check quota/rate limits i Azure Portal

**Google Sheets Fejl:**
- Verificer service account email har Editor adgang til sheetet
- Check credentials JSON er korrekt escaped
- Verificer spreadsheet ID er korrekt
- Check sheet navn matcher

**Function Kører Ikke:**
- Verificer Application Settings er sat korrekt i Azure
- Check function trigger i Azure Portal under **Functions** → **EmailProcessor** → **Integration**
- Se logs i Application Insights

## Arkitektur

```
┌─────────────────────────────────────────┐
│         Azure Function Timer            │
│         (kører hver time)               │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│        ImapEmailService                 │
│  (henter ulæste emails via IMAP)        │
└────────────────┬────────────────────────┘
                 │
                 ▼
         ┌───────────────┐
         │  For hver     │
         │  email:       │
         └───────┬───────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│        OpenAIService                    │
│  (udtræk pakkedata via Azure OpenAI)    │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│     GoogleSheetsService                 │
│  (indsæt række i Google Sheet)          │
└────────────────┬────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│        ImapEmailService                 │
│         (slet email)                    │
└─────────────────────────────────────────┘
```

## Sikkerhed

- Alle credentials gemmes som Application Settings (encrypted at rest)
- Brug aldrig hardcoded credentials i koden
- Google Service Account har minimal permissions (kun write til specifikt sheet)
- IMAP connection bruger SSL/TLS
- Private keys gemmes sikkert i Azure

## Support

For problemer eller spørgsmål, kontakt udviklingsteamet.
