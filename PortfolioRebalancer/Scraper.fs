module Scraper
    open System
    open System.IO
    open System.Text.Json
    open System.Threading.Tasks
    open System.Collections.ObjectModel
    open OpenQA.Selenium
    open OpenQA.Selenium.Chrome
    open OpenQA.Selenium.Support.UI
    open Types
    open Helpers
    open CachedFileData

    let private CreateChromeDriver() =

        let options = ChromeOptions()
        options.AddArgument("--headless")
        options.AddArgument("--no-sandbox")
        options.AddArgument("--disable-dev-shm-usage")
        options.AddArgument("--log-level=3")
        options.AddArgument("--disable-logging")
        options.AddArgument("--disable-gpu-sandbox")
        options.AddArgument("--disable-software-rasterizer")
        new ChromeDriver(options)

    let private WaitForElementAsync (driver: IWebDriver) (selector: By) (timeoutSeconds: float) = async {
        let wait = WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds))
        try
            let element = wait.Until(fun d -> 
                let elements = d.FindElements(selector)
                if elements.Count > 0 && elements.[0].Displayed then elements.[0] else null)
            return Ok element
        with
        | ex -> return Error (ElementNotFound $"Element not found: {selector}, Error: {ex.Message}")
    }

    let private ExecuteScriptAsync (driver: IWebDriver) (script: string) (args: obj[]) = async {
        try
            let jsExecutor = driver :?> IJavaScriptExecutor
            let result = jsExecutor.ExecuteScript(script, args)
            return Ok result
        with
        | ex -> return Error (DataExtractionError $"Script execution failed: {ex.Message}")
    }

    let private ExtractStockDataFromTable (rows: ReadOnlyCollection<IWebElement>) =
        try
            let stockData = 
                rows
                |> Seq.choose (fun row ->
                    let cells = row.FindElements(By.TagName("td"))
                
                    let symbol = 
                        if cells.Count > 0 then
                            try
                                let dataOrder = cells.[0].GetAttribute("data-order")
                                if not (String.IsNullOrEmpty(dataOrder)) then 
                                    dataOrder.Trim()
                                else
                                    let strongElement = cells.[0].FindElement(By.CssSelector("a.tbl__symbol strong"))
                                    strongElement.Text.Trim()
                            with
                            | _ -> 
                                let fullText = cells.[0].Text.Trim()
                                fullText.Replace("XD", "").Replace("CD", "").Replace("RD", "").Trim()
                        else 
                            ""
                    let name= if cells.Count > 1 then cells.[1].Text.Trim() else ""
                    let priceText= if cells.Count > 3 then cells.[3].Text.Trim() else "0.00"
                    let weightText= if cells.Count > 6 then cells.[6].Text.Trim() else ""

                    match weightText, priceText with
                        | ValidWeight weight, ValidDecimal price -> 
                            Some {
                                Symbol = symbol
                                Weight = weight
                                Price = price
                                Name = name
                            }
                        | _ -> None
                    )
                |> List.ofSeq
            Ok stockData
        with
        | ex -> Error (DataExtractionError $"Failed to extract stock data: {ex.Message}")

    let ScrapeIndexDataAsync (driver: IWebDriver) (url: string) (code: string) = async {
    
        try
            driver.Navigate().GoToUrl(url)
            printfn "Navigated to: %s" url

            let! kseLink = WaitForElementAsync driver (By.CssSelector($"a[data-code='{code}']")) 15.0
        
            match kseLink with
            | Error err -> return Error err
            | Ok element ->
                let! clickResult = ExecuteScriptAsync driver "arguments[0].click();" [| element |]
            
                match clickResult with
                | Error err -> return Error err
                | Ok _ ->
                    printfn "Clicked %s link" code
                    do! Async.Sleep 3000
                
                    let! selectElement = WaitForElementAsync driver (By.Name("DataTables_Table_1_length")) 15.0
                
                    match selectElement with
                    | Error err -> return Error err
                    | Ok element ->
                        let select = SelectElement(element)
                        select.SelectByValue("100")
                        printfn "Changed table length to 100"
                        do! Async.Sleep 3000

                    
                        let! tableElement = WaitForElementAsync driver (By.Id("DataTables_Table_1")) 15.0
                    
                        match tableElement with
                        | Error err -> return Error err
                        | Ok table ->
                            let rows = table.FindElements(By.CssSelector("tbody tr"))
                            printfn "Found %d rows in table" rows.Count
                        
                            let stockDataResult = ExtractStockDataFromTable rows
                        
                            match stockDataResult with
                            | Error err -> return Error err
                            | Ok stockData ->
                                printfn "Extracted %d stock records" (List.length stockData)
                                return Ok stockData
        with
        | ex -> 
            return Error (NavigationError $"Scraping error: {ex.Message}")
    }

    let GetStockFromPSXAsync (driver: IWebDriver) (symbol: string) = async {
        try
            let url = sprintf "https://dps.psx.com.pk/company/%s" symbol
            driver.Navigate().GoToUrl(url)
            printfn "Fetching price for %s from %s" symbol url
    
            do! Task.Delay(2000) |> Async.AwaitTask
    
            let! priceElement = WaitForElementAsync driver (By.CssSelector("div.quote__close")) 10.0
            let! nameElementFull = WaitForElementAsync driver (By.CssSelector("div.quote__name")) 10.0

            match priceElement, nameElementFull with
            | Ok pElement, Ok nElementFull ->
                let! nameResult = ExecuteScriptAsync driver "return arguments[0].childNodes[0].textContent.trim();" [|nElementFull|]
            
                match nameResult with
                | Ok nameObj ->
                    let priceText = pElement.Text.Replace("Rs.", "").Replace(",", "").Trim()
                    let name = nameObj :?> string

                    match Decimal.TryParse(priceText), name with
                    | (true, price), nameText when not (String.IsNullOrEmpty(nameText)) -> 
                        printfn "Found price for %s: Rs.%s" symbol (formatPKR price)
                        return Ok (price, nameText)
                    | _, _ -> 
                        printfn "Could not parse price for %s: %s or name error %s" symbol priceText name
                        return Error (DataExtractionError $"Invalid price format for {symbol}: {priceText}")
                | Error err ->
                    printfn "Could not extract name for %s" symbol
                    return Error err
            | Error err, _ -> 
                printfn "Could not find price element for %s" symbol
                return Error err
            | _, Error err -> 
                printfn "Could not find name element for %s" symbol
                return Error err
        with
        | ex -> 
            printfn "Error getting price for %s: %s" symbol ex.Message
            return Error (DataExtractionError $"Failed to get price for {symbol}: {ex.Message}")
    }

    let private GetMissingStockPricesAsync (driver: IWebDriver) (missingSymbols: string list) = async {
        let mutable results = []
    
        for symbol in missingSymbols do
            let! priceResult = GetStockFromPSXAsync driver symbol
            match priceResult with
            | Ok (price, name)->
                let stockRow = {
                    Symbol = symbol
                    Name = name
                    Price = price
                }
                results <- stockRow :: results
            | Error _ ->
                printfn "Skipping %s - could not get price" symbol
    
        return results
    }

    let ConstructIndexCache url indexCacheFile stockCacheFile currentPortfolioFile getLatestStockPrices = async {
        let indexType = KSE30

        let code = 
            match indexType with
                | KSE30 -> "KSE30"
                | KSE100 -> "KSE100"
        printf "Getting and constructing cache for %s" code

        use driver = CreateChromeDriver()

        let! indexData = ScrapeIndexDataAsync driver url code
        match indexData with
            | Error err -> return Error err
            | Ok kseData ->

                if getLatestStockPrices then
                    let! currentPortfolio = GetCurrentPortfolioRecordsData currentPortfolioFile

                    let stocksWithData = 
                        kseData 
                            |> List.filter (fun x -> x.Price <> 0m)
                            |> List.map _.Symbol
                            |> Set.ofList
                            

                    let portfolioSymbols = 
                        currentPortfolio
                            |> List.filter (fun x -> not (stocksWithData.Contains(x.Symbol)))   
                            |> List.map _.Symbol
                            |> List.distinct

                    let stocksWithoutData = 
                        kseData 
                            |> List.filter (fun x -> x.Price = 0m)
                            |> List.map _.Symbol
                            |> List.distinct


                    let! kseSymbols = 
                        (portfolioSymbols @ stocksWithoutData)
                            |> List.distinct
                            |> GetMissingStockPricesAsync driver
                    
                    let allData =
                        kseData 
                            |> List.filter (fun x -> x.Price <> 0m)
                            |> List.map (fun x -> 
                                {
                                    Symbol = x.Symbol
                                    Price = x.Price
                                    Name = x.Name
                                })
                            |> List.append kseSymbols
                            |> List.distinctBy _.Symbol

                    let! oldData = GetIndividualStockData stockCacheFile

                    let mergedData = 
                        allData @ oldData
                            |> List.distinctBy _.Symbol

                    do! SetIndividualStockData stockCacheFile mergedData

                let indexData = 
                    kseData 
                        |> List.map (fun x -> 
                            {
                                 Symbol = x.Symbol
                                 Weight = x.Weight
                            });

                do! SetIndexRecords indexCacheFile indexData

                printfn "Cache constructed"
                return Ok ()
    }