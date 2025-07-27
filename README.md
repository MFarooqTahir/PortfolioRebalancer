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
- PostgreSQL database
- Chrome browser (for web scraping)

### Installation

1. Clone the repository
2. Set up PostgreSQL database
3. Update connection string in `Configuration.fs`
4. Build the project:
   ```bash
   dotnet build
   ```

### Running the Application

```bash
dotnet run
```

The application provides an interactive menu for:
- Viewing current portfolio and index data
- Importing portfolio data from JSON
- Performing rebalancing calculations
- Scraping market data

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

## Testing

Run the test suite:

```bash
dotnet test
```

The test project includes unit tests for:
- Validation functions
- Commission calculations
- Data parsing
- Business logic

## Configuration

Key configuration options in `Configuration.fs`:

- **Commission rates**: Fixed and percentage-based commission calculations
- **Database settings**: Connection strings and timeouts
- **Validation rules**: Input validation constraints
- **Application settings**: Culture, currency, and formatting options

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

## Contributing

1. Follow F# coding conventions
2. Maintain type safety
3. Add tests for new functionality
4. Update documentation
5. Use functional programming patterns

## License

This project is licensed under the MIT License.

## Acknowledgments

- Built with F# and .NET 9.0
- Uses Spectre.Console for rich terminal output
- PostgreSQL for data persistence
- Selenium for web scraping 