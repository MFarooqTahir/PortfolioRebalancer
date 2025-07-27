# Portfolio Rebalancer

A functional F# application for portfolio rebalancing with support for index-based and current portfolio rebalancing strategies.

## Features

- **Index Rebalancing**: Rebalance a portfolio based on index weights
- **Current Portfolio Rebalancing**: Rebalance existing portfolio with cash injection
- **Commission Calculation**: Support for realistic trading commission calculations
- **Data Scraping**: Automated data collection from financial websites
- **Database Storage**: PostgreSQL-based data persistence
- **Performance Monitoring**: Built-in performance tracking and caching
- **Comprehensive Logging**: Detailed logging for debugging and monitoring
- **Type Safety**: Full F# type safety with discriminated unions and Result types

## Architecture

The application follows functional programming principles with clear separation of concerns:

### Core Modules

- **Types.fs**: Domain types and data structures
- **Configuration.fs**: Application settings and constants
- **Validation.fs**: Input validation and error handling
- **RebalancingCalculations.fs**: Pure calculation logic
- **Rebalancer.fs**: Orchestration and business logic
- **Table.fs**: Display and presentation logic
- **Db.fs**: Database operations
- **Scraper.fs**: Web scraping functionality
- **Helpers.fs**: Utility functions
- **Logging.fs**: Logging infrastructure
- **Performance.fs**: Performance monitoring and caching
- **Extensions.fs**: Extension methods and utilities

### Key Design Principles

1. **Immutability**: All data transformations are immutable
2. **Pure Functions**: Business logic separated from side effects
3. **Type Safety**: Compile-time guarantees with discriminated unions
4. **Error Handling**: Result types for explicit error handling
5. **Configuration**: Centralized configuration management
6. **Performance**: Built-in caching and performance monitoring

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- PostgreSQL database (or Docker for containerized setup)
- Chrome browser (for web scraping)

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

**Docker Commands:**
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
```

#### Option 2: Local PostgreSQL Installation

1. Install PostgreSQL on your system
2. Create a database named `portfolio`
3. Update connection string in `Configuration.fs`

### Installation

1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd PortfolioRebalancer
   ```

2. Build the project:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

## Testing

### Running Tests

The project includes comprehensive unit tests with detailed logging:

#### Basic Test Execution
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests with normal verbosity
dotnet test --verbosity normal
```

#### Detailed Test Logging
```bash
# Set environment variable for persistent detailed output
$env:DOTNET_CLI_VSTEST_VERBOSITY="detailed"
dotnet test
```

#### Test Output Example
```
[TEST] Starting test: Basic arithmetic test
   └─ Timestamp: 16:15:23.456
   ├─ Assertion: Testing addition: 5 + 3
   ├─ Assertion: Result: 8
   ├─ Assertion: Addition test passed
   ├─ Assertion: Testing multiplication: 5 * 3 = 15
   ├─ Assertion: Multiplication test passed
[PASS] Completed test: Basic arithmetic test
   └─ Duration: 00:00:00.0012345

[TEST] Starting test: List operations test
   └─ Timestamp: 16:15:23.458
   ├─ Assertion: Testing list operations with: [1; 2; 3; 4; 5]
   ├─ Assertion: Sum: 15
   ├─ Assertion: List sum test passed
   ├─ Assertion: Doubled list: [2; 4; 6; 8; 10]
   ├─ Assertion: List map test passed
   ├─ Assertion: Even numbers: [2; 4]
   ├─ Assertion: List filter test passed
[PASS] Completed test: List operations test
   └─ Duration: 00:00:00.0005678
```

#### Test Categories

The test suite includes:
- **Unit Tests**: Individual function testing
- **Theory Tests**: Parameterized testing with multiple data sets
- **Performance Tests**: Performance simulation and monitoring
- **Validation Tests**: Input validation and error handling
- **Integration Tests**: Database and external service testing

### Test Features

- **Detailed Logging**: Each test step is logged with timestamps
- **Performance Tracking**: Test execution time monitoring
- **Error Reporting**: Comprehensive error details and stack traces
- **Assertion Logging**: Each assertion is logged with context
- **Theory Testing**: Multiple test cases with different inputs

## Usage Examples

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

## Configuration

Key configuration options in `Configuration.fs`:

- **Commission rates**: Fixed and percentage-based commission calculations
- **Database settings**: Connection strings and timeouts
- **Validation rules**: Input validation constraints
- **Application settings**: Culture, currency, and formatting options

### Database Configuration

The application automatically detects the environment and uses appropriate database settings:

- **Development**: Uses local PostgreSQL connection
- **Production**: Uses `DATABASE_URL` environment variable
- **Docker**: Uses default PostgreSQL connection string

## Performance Features

- **Caching**: In-memory caching for frequently accessed data
- **Performance Monitoring**: Built-in metrics collection
- **Async Operations**: Non-blocking database and web operations
- **Memory Management**: Proper resource disposal and cleanup

## Error Handling

The application uses F# Result types for explicit error handling:

```fsharp
type ValidationError =
    | InvalidCashAmount of decimal
    | InvalidSymbol of string
    | MissingPriceData of string list
    | EmptyIndex
    | EmptyPortfolio
    | InsufficientData of string
```

## Logging

Comprehensive logging with different levels:
- **Debug**: Detailed operation information
- **Info**: General application flow
- **Warning**: Non-critical issues
- **Error**: Critical errors and exceptions

## Development Workflow

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
```

## Contributing

1. Follow F# coding conventions
2. Maintain type safety
3. Add tests for new functionality
4. Update documentation
5. Use functional programming patterns

### Testing Guidelines

- Write tests for all new functionality
- Use detailed logging in tests for better debugging
- Include both positive and negative test cases
- Test edge cases and error conditions
- Use theory tests for parameterized testing

## License

This project is licensed under the MIT License.

## Acknowledgments

- Built with F# and .NET 9.0
- Uses Spectre.Console for rich terminal output
- PostgreSQL for data persistence
- Selenium for web scraping
- xUnit for comprehensive testing 