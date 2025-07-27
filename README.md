# Portfolio Rebalancer

A comprehensive F# application for portfolio rebalancing with support for index-based and current portfolio rebalancing strategies. Built with functional programming principles and modern .NET technologies.

## 🚀 Features

- **📊 Index Rebalancing**: Rebalance a portfolio based on index weights
- **🔄 Current Portfolio Rebalancing**: Rebalance existing portfolio with cash injection
- **💰 Commission Calculation**: Support for realistic trading commission calculations
- **🌐 Data Scraping**: Automated data collection from financial websites
- **🗄️ Database Storage**: PostgreSQL-based data persistence
- **⚡ Performance Monitoring**: Built-in performance tracking and caching
- **📝 Comprehensive Logging**: Detailed logging for debugging and monitoring
- **🛡️ Type Safety**: Full F# type safety with discriminated unions and Result types
- **🧪 Comprehensive Testing**: 35 unit tests covering validation, business logic, and utilities
- **⚙️ Configuration Management**: Centralized application settings
- **🔍 Input Validation**: Robust validation with custom error types

## 🏗️ Architecture

The application follows functional programming principles with clear separation of concerns:

### Core Modules

| Module | Purpose | Key Features |
|--------|---------|--------------|
| **Types.fs** | Domain types and data structures | Discriminated unions, Result types, domain models |
| **Configuration.fs** | Application settings and constants | Environment-aware settings, centralized configuration |
| **Validation.fs** | Input validation and error handling | Custom validation rules, Result-based error handling |
| **RebalancingCalculations.fs** | Pure calculation logic | Immutable calculations, functional algorithms |
| **Rebalancer.fs** | Orchestration and business logic | Result handling, error propagation |
| **Table.fs** | Display and presentation logic | Rich console output, formatted tables |
| **Db.fs** | Database operations | Async operations, connection management |
| **Scraper.fs** | Web scraping functionality | Selenium integration, data extraction |
| **Helpers.fs** | Utility functions | Common operations, formatting helpers |
| **Logging.fs** | Logging infrastructure | File-based logging, different log levels |
| **Performance.fs** | Performance monitoring and caching | In-memory caching, metrics collection |
| **Extensions.fs** | Extension methods and utilities | Type extensions, utility functions |
| **Messages.fs** | User-friendly messages | Centralized messaging, localization support |

### Key Design Principles

1. **🔄 Immutability**: All data transformations are immutable
2. **🧪 Pure Functions**: Business logic separated from side effects
3. **🛡️ Type Safety**: Compile-time guarantees with discriminated unions
4. **⚠️ Error Handling**: Result types for explicit error handling
5. **⚙️ Configuration**: Centralized configuration management
6. **⚡ Performance**: Built-in caching and performance monitoring
7. **📝 Logging**: Comprehensive logging for debugging and monitoring

## 🚀 Getting Started

### Prerequisites

- **.NET 9.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/9.0)
- **PostgreSQL database** (or Docker for containerized setup)
- **Chrome browser** (for web scraping functionality)

### Database Setup

#### Option 1: Docker (Recommended for Development)

Run PostgreSQL in a Docker container:

```bash
docker run --name portfolio-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=postgres1234 \
  -e POSTGRES_DB=portfolio \
  -p 5432:5432 \
  -d postgres
```

**Docker Management Commands:**
```bash
# Start the database
docker start portfolio-postgres

# Stop the database
docker stop portfolio-postgres

# Remove the container
docker rm portfolio-postgres

# View logs
docker logs portfolio-postgres

# Connect to database
docker exec -it portfolio-postgres psql -U postgres -d portfolio

# Check container status
docker ps -a | grep portfolio-postgres
```

#### Option 2: Local PostgreSQL Installation

1. Install PostgreSQL on your system
2. Create a database named `portfolio`
3. Update connection string in `Configuration.fs` if needed

### Installation & Setup

1. **Clone the repository:**
   ```bash
   git clone <repository-url>
   cd PortfolioRebalancer
   ```

2. **Build the project:**
   ```bash
   dotnet build
   ```

3. **Run the application:**
   ```bash
   dotnet run
   ```

## 🧪 Testing

The project includes a comprehensive test suite with **35 tests** covering all major functionality, featuring beautiful enhanced output using Spectre.Console:

### Test Coverage

| Category | Tests | Description |
|----------|-------|-------------|
| **Validation** | 15 | Input validation, error handling, and data integrity |
| **Helper Functions** | 5 | Utility functions and calculations |
| **Rebalancing Calculations** | 8 | Core rebalancing logic and edge cases |
| **Configuration** | 3 | Application settings and constants |
| **Messages** | 2 | User-facing messages and formatting |

**Total: 35 tests** covering all critical business logic and edge cases.

### 🧪 Testing

The test suite provides comprehensive coverage of all critical functionality:

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity detailed

# Run specific test project
dotnet test PortfolioRebalancer.Tests/

# Run tests with normal verbosity
dotnet test --verbosity normal
```



### Test Categories

The test suite includes:
- **Unit Tests**: Individual function testing with comprehensive coverage
- **Validation Tests**: Input validation and error handling scenarios
- **Business Logic Tests**: Core rebalancing calculations and edge cases
- **Configuration Tests**: Application settings and constants validation
- **Extension Tests**: Type extensions and utility function testing

## 💻 Usage Examples

### Index Rebalancing

```fsharp
let config = {
    IncludeCommission = true
    Cash = 10000m
    Index = indexData
    Current = []
    Prices = priceData
}

let result = rebalanceIndex config
```

### Current Portfolio Rebalancing

```fsharp
let config = {
    IncludeCommission = true
    Cash = 5000m
    Index = indexData
    Current = currentPortfolio
    Prices = priceData
}

let result = rebalanceCurrentPortfolio config
```

### Application Menu

The application provides an interactive menu with the following options:

1. **Show Index and Current Portfolio** - Display current portfolio data
2. **Import Current Portfolio from JSON** - Import portfolio data from JSON file
3. **Rebalance Index with Cash** - Perform index-based rebalancing
4. **Rebalance Current Portfolio with Cash** - Rebalance existing portfolio
5. **Get Index and Stock Data** - Scrape and save market data
6. **Show Performance Metrics** - Display performance statistics
7. **Clear Cache** - Clear cached data and metrics
0. **Exit** - Exit the application

## ⚙️ Configuration

Key configuration options in `Configuration.fs`:

### Commission Settings
- **Fixed Rate**: Commission per share for low-priced stocks
- **Percentage Rate**: Percentage-based commission for high-priced stocks

### Database Settings
- **Connection Strings**: Environment-aware database connections
- **Timeouts**: Database operation timeouts
- **Retry Logic**: Automatic retry for failed operations

### Validation Rules
- **Cash Amount Limits**: Minimum and maximum cash amounts
- **Symbol Length**: Valid symbol length constraints
- **Price Ranges**: Valid price ranges for stocks

### Application Settings
- **Culture**: Localization settings (PKR currency)
- **Logging**: Log levels and file paths
- **Performance**: Cache settings and metrics retention

### Database Configuration

The application automatically detects the environment and uses appropriate database settings:

- **Development**: Uses local PostgreSQL connection
- **Production**: Uses `DATABASE_URL` environment variable
- **Docker**: Uses default PostgreSQL connection string

## ⚡ Performance Features

- **💾 Caching**: In-memory caching for frequently accessed data
- **📊 Performance Monitoring**: Built-in metrics collection
- **🔄 Async Operations**: Non-blocking database and web operations
- **🧹 Memory Management**: Proper resource disposal and cleanup
- **📈 Metrics Collection**: Operation timing and success tracking

## ⚠️ Error Handling

The application uses F# Result types for explicit error handling:

```fsharp
type ValidationError =
    | InvalidCashAmount of decimal
    | InvalidSymbol of string
    | MissingPriceData of string list
    | EmptyIndex
    | EmptyPortfolio
    | InsufficientData of string

type ScrapingError =
    | NavigationError of string
    | ElementNotFound of string
    | DataExtractionError of string
    | TimeoutError of string
    | NetworkError of string
```

## 📝 Logging

Comprehensive logging with different levels:
- **Debug**: Detailed operation information
- **Info**: General application flow
- **Warning**: Non-critical issues
- **Error**: Critical errors and exceptions

Log files are stored in the application directory with timestamps and rotation.

## 🧪 Testing

The application includes a comprehensive test suite with **35 tests** covering all major functionality. For detailed information about the enhanced test output system, see the [Testing section](#-testing) above.

### Test Coverage

| Category | Tests | Coverage |
|----------|-------|----------|
| **Validation** | 15 | Input validation, error handling |
| **Helper Functions** | 5 | Commission, formatting, utilities |
| **Rebalancing Calculations** | 8 | Core business logic, algorithms |
| **Configuration** | 3 | Settings and constants validation |
| **Messages** | 2 | User interface messages |

### Quick Test Commands

```bash
# Run tests with enhanced output (recommended)
dotnet run --project TestRunner/

# Run standard tests
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test PortfolioRebalancer.Tests/
```

### Test Categories

**Validation Tests (15 tests)**
- `validateCashAmount` - Positive, zero, and negative amounts
- `validateIndexNotEmpty` - Empty and non-empty index validation
- `validatePrice`, `validateShares`, `validateWeight` - Data validation
- `validateSymbol` - Symbol validation with length constraints
- `ValidDecimal` pattern matching - Decimal parsing validation

**Helper Function Tests (5 tests)**
- `calculateCommission` - Fixed rate and percentage rate calculations
- `createWeightMap` - Weight map creation from index data
- `formatPKR`, `toPercentage`, `formatPKRShort` - Formatting functions

**Rebalancing Calculation Tests (8 tests)**
- `createPriceMap`, `createCurrentSharesMap` - Data structure creation
- `calculateTargetShares` - Target share calculation logic
- `rebalanceIndex` - Index rebalancing with correct orders
- `rebalanceCurrentPortfolio` - Portfolio rebalancing scenarios
- Error handling for empty data and missing prices

**Configuration Tests (3 tests)**
- Commission rates validation
- Database settings validation
- Validation rules validation

**Messages Tests (2 tests)**
- Error, success, and info message validation

## 🔧 Development Workflow

### Quick Start for Development

1. **Start Database:**
   ```bash
   docker run --name portfolio-postgres \
     -e POSTGRES_USER=postgres \
     -e POSTGRES_PASSWORD=postgres1234 \
     -e POSTGRES_DB=portfolio \
     -p 5432:5432 \
     -d postgres
   ```

2. **Build and Test:**
   ```bash
   dotnet build
   
   # Run tests
   dotnet test
   
   # Or run standard tests
   dotnet test --logger "console;verbosity=detailed"
   ```

3. **Run Application:**
   ```bash
   dotnet run
   ```

### Common Commands

```bash
# Clean and rebuild
dotnet clean
dotnet build

# Run tests
dotnet test

# Run tests with detailed logging
dotnet test --logger "console;verbosity=detailed"

# Run specific test project
dotnet test PortfolioRebalancer.Tests/

# Set detailed verbosity for all test runs
$env:DOTNET_CLI_VSTEST_VERBOSITY="detailed"

# Database management
docker start portfolio-postgres
docker stop portfolio-postgres
docker logs portfolio-postgres

# Check application status
dotnet run --help
```

### Project Structure

```
PortfolioRebalancer/
├── PortfolioRebalancer/           # Main application
│   ├── Program.fs                 # Application entry point
│   ├── Types.fs                   # Domain types
│   ├── Configuration.fs           # App configuration
│   ├── Validation.fs              # Input validation
│   ├── RebalancingCalculations.fs # Pure calculation logic
│   ├── Rebalancer.fs              # Business logic
│   ├── Table.fs                   # Display logic
│   ├── Db.fs                      # Database operations
│   ├── Scraper.fs                 # Web scraping
│   ├── Helpers.fs                 # Utility functions
│   ├── Logging.fs                 # Logging infrastructure
│   ├── Performance.fs             # Performance monitoring
│   ├── Extensions.fs              # Extension methods
│   └── Messages.fs                # User messages
├── PortfolioRebalancer.Tests/     # Test project
│   ├── Tests.fs                   # Comprehensive test suite (35 tests)
│   └── PortfolioRebalancer.Tests.fsproj # Test project configuration
├── PortfolioRebalancer.sln        # Solution file
└── Portfolio Data/                # Sample data
    ├── CurrentPortfolio.json      # Sample portfolio
    └── periodic-details.pdf       # Documentation
```

## 🤝 Contributing

1. **Follow F# coding conventions**
2. **Maintain type safety**
3. **Add tests for new functionality**
4. **Update documentation**
5. **Use functional programming patterns**

### F# Coding Rules & Best Practices

This project follows strict F# coding conventions and functional programming principles:

#### **Functional Programming Principles**
- **Immutability**: All data transformations are immutable
- **Pure Functions**: Business logic separated from side effects
- **Composition**: Use function composition and pipeline operators (`|>`)
- **Pattern Matching**: Extensive use of pattern matching for control flow
- **Result Types**: Explicit error handling with `Result<'T, 'E>` types

#### **Code Organization**
- **Module Structure**: Logical grouping of related functions
- **Type Definitions**: Clear, descriptive type names with discriminated unions
- **Function Naming**: Descriptive names that explain intent
- **Separation of Concerns**: Pure calculations separated from I/O operations

#### **Error Handling**
- **Result Types**: Use `Result<'T, 'E>` for functions that can fail
- **Custom Error Types**: Domain-specific error types with discriminated unions
- **Validation**: Comprehensive input validation with detailed error messages
- **Graceful Degradation**: Handle errors gracefully with user-friendly messages

#### **Performance & Optimization**
- **Lazy Evaluation**: Use lazy sequences where appropriate
- **Memoization**: Cache expensive calculations
- **Async Operations**: Use async/await for I/O operations
- **Memory Efficiency**: Avoid unnecessary allocations

### Testing Guidelines

- Write tests for all new functionality
- Focus on testing actual business logic, not just basic operations
- Include both positive and negative test cases
- Test edge cases and error conditions
- Test Result types and error handling paths
- Ensure all tests pass before submitting changes
- Use descriptive test names that explain the scenario being tested
- Test pure functions extensively
- Mock external dependencies appropriately

### Code Style

- Use F# naming conventions (camelCase for functions, PascalCase for types)
- Prefer immutable data structures
- Use discriminated unions for type-safe modeling
- Implement proper error handling with Result types
- Add comprehensive logging for debugging
- Use active patterns for complex pattern matching
- Prefer composition over inheritance
- Use type annotations where helpful for clarity

## 📄 License

This project is licensed under the MIT License.

## 🙏 Acknowledgments

- **Built with F# and .NET 9.0** - Modern functional programming
- **Spectre.Console** - Rich terminal output and formatting
- **PostgreSQL** - Robust data persistence
- **Selenium** - Web scraping and automation
- **xUnit** - Comprehensive testing framework
- **Dapper** - Lightweight ORM for database access


## 📞 Support

For issues, questions, or contributions:
1. Check the existing documentation
2. Review the test suite for usage examples
3. Examine the configuration settings
4. Check the logging output for debugging information

---

**Happy Portfolio Rebalancing! 🚀📈** 