module PortfolioRebalancer.Scraper.Scraper

open System
open System.Threading
open System.Collections.Generic
open System.Collections.ObjectModel
open OpenQA.Selenium
open OpenQA.Selenium.Chrome
open OpenQA.Selenium.Support.UI
open PortfolioRebalancer.Core.Types
open PortfolioRebalancer.Core.Helpers
open PortfolioRebalancer.Data.Db
open Microsoft.Extensions.Logging
open PortfolioRebalancer.Core.Logger

let CreateChromeDriver () =
    let options = ChromeOptions()
    options.AddArgument("--headless")
    options.AddArgument("--no-sandbox")
    options.AddArgument("--disable-dev-shm-usage")
    options.AddArgument("--disable-gpu")
    options.AddArgument("--disable-extensions")
    options.AddArgument("--disable-plugins")
    options.AddArgument("--disable-images")
    options.AddArgument("--disable-javascript")
    options.AddArgument("--disable-web-security")
    options.AddArgument("--disable-features=VizDisplayCompositor")
    options.AddArgument("--window-size=1920,1080")
    
    let logger: ILogger = getModuleLoggerByName "Scraper"
    logger.LogInformation("Creating ChromeDriver with headless configuration...")
    let driver = new ChromeDriver(options)
    logger.LogInformation("ChromeDriver created successfully")
    driver

let WaitForElementAsync (driver: IWebDriver) (by: By) (timeoutSeconds: float) = async {
    let logger: ILogger = getModuleLoggerByName "Scraper"
    logger.LogDebug("Waiting for element: {Element} (timeout: {Timeout} seconds)", by, timeoutSeconds)
    let wait = WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds))
    try
        let element = wait.Until(fun d -> d.FindElement(by))
        logger.LogDebug("Element found successfully: {Element}", by)
        return Ok element
    with
    | ex -> 
        logger.LogError(ex, "Element not found after {Timeout} seconds: {Element}", timeoutSeconds, by)
        return Error (ElementNotFound ex.Message)
}

let ExecuteScriptAsync<'T> (driver: IWebDriver) (script: string) (args: obj array) = async {
    let logger: ILogger = getModuleLoggerByName "Scraper"
    logger.LogDebug("Executing JavaScript: {Script}", script)
    try
        let result = (driver :?> IJavaScriptExecutor).ExecuteScript(script, args)
        logger.LogDebug("JavaScript executed successfully")
        return Ok (unbox<'T> result)
    with
    | ex -> 
        logger.LogError(ex, "JavaScript execution failed: {Script}", script)
        return Error (DataExtractionError ex.Message)
}

let ExtractStockDataFromTable (rows: ReadOnlyCollection<IWebElement>) =
    let logger: ILogger = getModuleLoggerByName "Scraper"
    try
        let stockData = 
            rows 
            |> Seq.toList
            |> List.choose (fun row ->
                let cells = row.FindElements(By.TagName("td"))
                if cells.Count >= 4 then
                    let symbol = cells.[0].Text.Trim()
                    let weightText = cells.[1].Text.Trim()
                    let priceText = cells.[2].Text.Trim()
                    let name = cells.[3].Text.Trim()
                    
                    match weightText, priceText with
                    | ValidWeight weight, ValidDecimal price when not (String.IsNullOrEmpty(symbol)) ->
                        Some {
                            Symbol = symbol
                            Weight = weight
                            Price = price
                            Name = name
                        }
                    | _, _ -> None
                else None)
        
        Ok stockData
    with
    | ex -> Error (DataExtractionError ex.Message)

let ScrapeIndexDataAsync (driver: IWebDriver) (url: string) (code: string) = async {
    let logger: ILogger = getModuleLoggerByName "Scraper"
    try
        logger.LogInformation("Starting to scrape index data for {Code} from: {Url}", code, url)
        driver.Navigate().GoToUrl url
        logger.LogInformation("Navigated to: {Url}", url)
        logger.LogDebug("Current page title: {Title}", driver.Title)
        logger.LogDebug("Current page URL: {Url}", driver.Url)

        logger.LogDebug("Looking for KSE link with data-code='{Code}'", code)
        let! kseLink = WaitForElementAsync driver (By.CssSelector $"a[data-code='{code}']") 15.0
        
        match kseLink with
        | Error err -> 
            logger.LogError("Failed to find KSE link for {Code}", code)
            return Error err
        | Ok element ->
            logger.LogDebug("Found KSE link for {Code}, attempting to click", code)
            let! clickResult = ExecuteScriptAsync<obj> driver "arguments[0].click();" [| element |]
            
            match clickResult with
            | Error err -> 
                logger.LogError("Failed to click KSE link for {Code}", code)
                return Error err
            | Ok _ ->
                logger.LogInformation("Clicked {Code} link successfully", code)
                logger.LogDebug("Waiting 3 seconds for page to load...")
                do! Async.Sleep 3000
                logger.LogDebug("After click - Current page title: {Title}", driver.Title)
                logger.LogDebug("After click - Current page URL: {Url}", driver.Url)
                
                logger.LogDebug("Looking for table length selector...")
                let! selectElement = WaitForElementAsync driver (By.Name "DataTables_Table_1_length") 15.0
                
                match selectElement with
                | Error err -> 
                    logger.LogError("Failed to find table length selector")
                    return Error err
                | Ok element ->
                    logger.LogDebug("Found table length selector, changing to 100 rows...")
                    let select = SelectElement(element)
                    select.SelectByValue("100")
                    logger.LogInformation("Changed table length to 100")
                    logger.LogDebug("Waiting 3 seconds for table to reload...")
                    do! Async.Sleep 3000

                    
                    logger.LogDebug("Looking for data table...")
                    let! tableElement = WaitForElementAsync driver (By.Id "DataTables_Table_1") 15.0
                    
                    match tableElement with
                    | Error err -> 
                        logger.LogError("Failed to find data table")
                        return Error err
                    | Ok table ->
                        logger.LogDebug("Found data table, extracting rows...")
                        let rows = table.FindElements(By.CssSelector("tbody tr"))
                        logger.LogInformation("Found {Count} rows in table", rows.Count)
                        let htmlLength = table.GetAttribute("outerHTML").Length
                        let maxPreviewLength = 500
                        let previewLength = min maxPreviewLength htmlLength
                        logger.LogDebug("Table HTML structure: {Html}", table.GetAttribute("outerHTML").Substring(0, previewLength))
                        
                        logger.LogDebug("Extracting stock data from table rows...")
                        let stockDataResult = ExtractStockDataFromTable rows
                        
                        match stockDataResult with
                        | Error err -> 
                            logger.LogError("Failed to extract stock data: {Error}", err.ToString())
                            return Error err
                        | Ok stockData ->
                            logger.LogInformation("Successfully extracted {Count} stock records", List.length stockData)
                            let stockCount = List.length stockData
                            let maxCount = 3
                            let takeCount = min maxCount stockCount
                            let firstFew = stockData |> List.take takeCount
                            logger.LogDebug("First few records: {Records}", firstFew)
                            return Ok stockData
    with
    | ex -> 
        logger.LogError(ex, "Scraping error occurred")
        return Error (NavigationError $"Scraping error: {ex.Message}")
}

let GetStockFromPSXAsync (driver: IWebDriver) (symbol: string) = async {
    let logger: ILogger = getModuleLoggerByName "Scraper"
    try
        let url = sprintf "https://dps.psx.com.pk/company/%s" symbol
        driver.Navigate().GoToUrl url
        logger.LogDebug("Fetching price for {Symbol} from {Url}", symbol, url)
    
        do! Async.Sleep 2000
    
        let! priceElement = WaitForElementAsync driver (By.CssSelector "div.quote__close") 10.0
        let! nameElementFull = WaitForElementAsync driver (By.CssSelector "div.quote__name") 10.0

        match priceElement, nameElementFull with
        | Ok pElement, Ok nElementFull ->
            let! nameResult = ExecuteScriptAsync<string> driver "return arguments[0].childNodes[0].textContent.trim();" [|nElementFull|]
            
            match nameResult with
            | Ok name ->
                let priceText = pElement.Text.Replace("Rs.", "").Replace(",", "").Trim()

                match priceText, name with
                | ValidDecimal price, nameText when not (String.IsNullOrEmpty(nameText)) -> 
                    logger.LogDebug("Found price for {Symbol}: {Price}", symbol, formatPKR price)
                    return Ok (price, nameText)
                | _, _ -> 
                    logger.LogWarning("Could not parse price for {Symbol}: {Price} or name error {Name}", symbol, priceText, name)
                    return Error (DataExtractionError $"Invalid price format for {symbol}: {priceText}")
            | Error err ->
                logger.LogWarning("Could not extract name for {Symbol}", symbol)
                return Error err
        | Error err, _ -> 
            logger.LogWarning("Could not find price element for {Symbol}", symbol)
            return Error err
        | _, Error err -> 
            logger.LogWarning("Could not find name element for {Symbol}", symbol)
            return Error err
    with
    | ex -> 
        logger.LogError(ex, "Error getting price for {Symbol}", symbol)
        return Error (DataExtractionError $"Failed to get price for {symbol}: {ex.Message}")
}

// Restored version with database operations
let ConstructIndexCache (forceRefresh: bool) (indexType: IndexType) = async {
    let logger: ILogger = getModuleLoggerByName "Scraper"
    try
        logger.LogInformation("Starting ConstructIndexCache for {IndexType} (forceRefresh: {ForceRefresh})", indexType, forceRefresh)
        use driver = CreateChromeDriver ()
        
        let url = "https://www.psx.com.pk/psx/resources-and-tools/indices/kse-mis"
        let code = match indexType with | KSE30 -> "KSE30" | KSE100 -> "KSE100"
        logger.LogInformation("Target URL: {Url}, Index Code: {Code}", url, code)
        
        let! result = ScrapeIndexDataAsync driver url code
        
        match result with
        | Ok stockData ->
            // Convert to IndexTableRecord list
            let indexRecords = 
                stockData 
                |> List.map (fun data -> 
                    { Symbol = data.Symbol; Weight = data.Weight })
            
            // Convert to StockRecord list
            let stockRecords = 
                stockData 
                |> List.map (fun data -> 
                    { Symbol = data.Symbol; Name = data.Name; Price = data.Price })
            
            // Save to database
            do! setIndexRecordsAsync indexRecords
            do! setStockRecordsAsync stockRecords
            
            logger.LogInformation "Cache constructed"
            return Ok ()
        | Error err -> return Error err
    with
    | ex -> 
        logger.LogError(ex, "Navigation error in ConstructIndexCache")
        return Error (NavigationError ex.Message)
}