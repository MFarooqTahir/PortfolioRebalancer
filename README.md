# Portfolio Rebalancer

A comprehensive F# application for portfolio rebalancing with support for index-based and current portfolio rebalancing strategies.

## ⚡ Quick Start

1. **Start Database:**
   ```bash
   docker run --name portfolio-postgres -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres1234 -e POSTGRES_DB=portfolio -p 5432:5432 -d postgres
   ```

2. **Build & Run:**
   ```bash
   dotnet build
   dotnet run --project PortfolioRebalancer/
   ```

3. **Get Started:**
   - Choose `Option 5` to fetch market data
   - Choose `Option 1` to view current data
   - Choose `Option 3` or `Option 4` to rebalance

## 🚀 Features

- **📊 Index Rebalancing**: Rebalance based on index weights
- **🔄 Current Portfolio Rebalancing**: Rebalance existing portfolio with cash
- **💰 Commission Calculation**: Realistic trading commission calculations
- **🌐 Data Scraping**: Automated data collection from financial websites
- **🗄️ PostgreSQL Storage**: Robust data persistence
- **⚡ Performance Monitoring**: Built-in caching and metrics
- **🛡️ Type Safety**: Full F# type safety with discriminated unions
- **🧪 Comprehensive Testing**: 35 unit tests

## ⚙️ Configuration

The application uses JSON-based configuration with environment support:

### Configuration Files

- **`appsettings.json`** - Base configuration (version controlled)
- **`appsettings.Development.json`** - Development overrides (version controlled)
- **`appsettings.Production.json`** - Production settings (ignored by git)

### Configuration Structure

```json
{
  "Commission": {
    "MinimumPrice": 12.0,
    "FixedRate": 0.03,
    "PercentageRate": 0.0025
  },
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=portfolio;Username=postgres;Password=postgres1234",
    "DefaultTimeoutSeconds": 30
  },
  "App": {
    "DefaultCulture": "hi-IN",
    "CurrencySymbol": "Rs."
  },
  "Validation": {
    "MinimumCashAmount": 0.0,
    "MaximumCashAmount": 1000000000.0,
    "MinimumShares": 0,
    "MaximumShares": 1000000000,
    "MinimumPrice": 0.01,
    "MaximumPrice": 1000000.0,
    "MinimumWeight": 0.0,
    "MaximumWeight": 1.0,
    "MaxSymbolLength": 10,
    "MinSymbolLength": 1
  }
}
```

### Environment Support

- **Development**: Uses `appsettings.Development.json` overrides
- **Production**: Uses `appsettings.Production.json` + environment variables
- **Environment Variables**: Override sensitive data (e.g., `DATABASE_URL`)

### Security

- Production configuration files are excluded from git
- Sensitive data should be provided via environment variables
- Development configs contain safe defaults

## 🏗️ Architecture

Built with functional programming principles:

- **Types.fs** - Domain types and data structures
- **Configuration.fs** - Environment-aware settings
- **Validation.fs** - Input validation with Result types
- **RebalancingCalculations.fs** - Pure calculation logic
- **Rebalancer.fs** - Business logic orchestration
- **Db.fs** - Database operations
- **Scraper.fs** - Web scraping functionality

## 📥 Portfolio Import

Import your current portfolio from JSON:

```json
[
    {
        "Symbol": "BAHL",
        "Shares": 650,
        "Price": 157.85
    }
]
```

**For detailed import instructions, see [IMPORT_GUIDE.md](IMPORT_GUIDE.md)**

## 🧪 Testing

```bash
dotnet test
```

35 comprehensive tests covering validation, business logic, and utilities.

## 🔧 Development

### Prerequisites
- .NET 9.0 SDK
- PostgreSQL (or Docker)
- Chrome browser (for web scraping)

### Common Commands

```bash
# Build
dotnet build

# Run
dotnet run --project PortfolioRebalancer/

# Test
dotnet test

# Database management
docker start portfolio-postgres
docker stop portfolio-postgres
```

### Project Structure

```
PortfolioRebalancer/
├── PortfolioRebalancer/           # Main application
├── PortfolioRebalancer.Tests/     # Test project
├── appsettings.json              # Base configuration
├── appsettings.Development.json  # Development overrides
└── appsettings.Production.json   # Production settings (gitignored)
```

## 📄 License

MIT License

---

**Happy Portfolio Rebalancing! 🚀📈** 