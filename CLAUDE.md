# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Clap.Net is a C# source generator for command-line argument parsing, inspired by clap-rs (the Rust CLI parser). It provides strongly-typed, attribute-based CLI definition with automatic help generation and subcommand support.

**Key Features:**
- Source generator that emits `Parse()` methods for classes marked with `[Command]`
- Supports subcommands, named arguments (options), positional arguments, and flags
- Generates help text from XML documentation comments
- Outputs CLI JSON schema for `[Cli]` commands

## Build and Test Commands

```bash
# Build the solution
dotnet build

# Build in Release mode
dotnet build -c Release

# Run tests (uses xunit with Shouldly assertions)
dotnet test

# Run tests with verbose output
dotnet test --verbosity normal

# Run a specific test
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Pack NuGet package
dotnet pack -c Release

# Run the test console application
dotnet run --project Clap.TestConsole

# Run test console with arguments
dotnet run --project Clap.TestConsole -- [args]
```

## Shell Completion Generation

The Clap.Net.Tool can generate shell completion scripts for commands marked with `[Cli]` attribute:

```bash
# Generate Bash completion
dotnet run --project Clap.Net.Tool -- \
    --assembly MyApp.dll \
    --format bash \
    --output myapp-completion.bash

# Generate Zsh completion
dotnet run --project Clap.Net.Tool -- \
    --assembly MyApp.dll \
    --format zsh \
    --output _myapp

# Generate Fish completion
dotnet run --project Clap.Net.Tool -- \
    --assembly MyApp.dll \
    --format fish \
    --output myapp.fish

# Generate PowerShell completion
dotnet run --project Clap.Net.Tool -- \
    --assembly MyApp.dll \
    --format powershell \
    --output myapp-completion.ps1

# Override command name (useful for custom install names)
dotnet run --project Clap.Net.Tool -- \
    --assembly MyApp.dll \
    --format bash \
    --command-name my-custom-name
```

**Supported formats:**
- `json` - OpenCLI JSON schema (default)
- `bash` - Bash completion script using bash-completion framework
- `zsh` - Zsh completion script using zsh completion system
- `fish` - Fish shell completion script
- `powershell` - PowerShell ArgumentCompleter script

**Installation instructions** are included in the generated completion scripts as comments.

## Debugging Generated Code

To view MSBuild-generated files during development, add to `Clap.Net.csproj`:

```xml
<PropertyGroup Condition="'$(Configuration)' == 'Debug'">
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>bin/Generated</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files will appear in `bin/Generated` directory.

## Architecture

### Source Generation Pipeline

1. **Syntax Providers** (`Providers/` directory):
   - `CommandModelSyntaxProvider` - Identifies classes with `[Command]` attribute
   - `SubCommandSyntaxProvider` - Identifies classes with `[SubCommand]` attribute

2. **Model Parsing** (`CommandModelParser.cs`):
   - Parses Roslyn syntax into intermediate models
   - Extracts XML documentation for help text
   - Resolves property/field types, default values, and attributes
   - Handles special variable name escaping (e.g., `params` → `@___params`)

3. **Models** (`Models/` directory):
   - `CommandModel` - Represents a command with its arguments and metadata
   - `ArgumentModel` - Base type for all argument types
   - `NamedArgumentModel` - Options with short/long names (e.g., `-v`, `--verbose`)
   - `PositionalArgumentModel` - Position-based arguments
   - `SubCommandArgumentModel` - Nested command property

4. **Code Generation** (`Generators/` directory):
   - `CommandCodeGenerator` - Emits the `Parse()` method as a partial class extension
   - `SubCommandCodeGenerator` - Emits parsing for subcommand types
   - Uses `SyntaxBuilder` helpers to construct C# code with proper indentation

5. **Argument Lexing** (`ArgsLexer.cs`):
   - Tokenizes command-line arguments
   - Handles short flags, long options, values, and subcommands
   - Recent rename from `Lexer` to `ArgsLexer` to avoid naming collisions

### Key Components

- **ClapGenerator.cs**: Entry point implementing `IIncrementalGenerator`
  - Registers syntax providers
  - Combines command/subcommand models with compilation context
  - Emits source code via `RegisterSourceOutput`
  - Optionally emits CLI JSON schema for `[Cli]` commands

- **Attributes** (`Attributes/` directory):
  - `CommandAttribute` - Marks the root command class (Name, About, Version)
  - `ArgAttribute` - Defines an argument (Short, Long, Help, Env, Negation, Action)
  - `SubCommandAttribute` - Marks subcommand base classes
  - `CliAttribute` - Triggers JSON schema generation

- **Serialization** (`Serialisation/` directory):
  - `SyntaxBuilder` - Helpers for emitting C# code
  - `SimpleJsonBuilder` - Builds JSON schema for CLI commands
  - Various builder classes for blocks, switches, and type syntax

## Important Implementation Notes

- The main library targets `netstandard2.0` for broad compatibility
- Uses `EnforceExtendedAnalyzerRules` for source generator correctness
- Packages both as an analyzer (`analyzers/dotnet/cs/`) and library (`lib/netstandard2.0/`)
- Recent changes removed the need for `SubCommand = true` on nested command properties
- Static class references (like `Environment`) must be fully qualified to avoid collisions
- `NegatedFlag` had namespacing issues that were recently fixed

## Testing

Tests are in `Clap.Net.Tests/`:
- Uses xunit as the test framework
- Uses Shouldly for fluent assertions
- `Tests.cs` contains integration tests for parsing scenarios
- `ArgsLexerTests.cs` tests the argument tokenizer
- Tests reference Clap.Net as an analyzer: `OutputItemType="Analyzer"`

## Test Console

`Clap.TestConsole/` contains example commands in `Examples/` directory. Use this to manually test the generator and verify parsing behavior.

## Common Patterns

**Defining a command:**
```csharp
[Command(Name = "myapp", About = "Does stuff")]
public partial class MyApp
{
    [Arg(Short = 'v', Long = "verbose")]
    public bool Verbose { get; init; }

    // Positional argument (no Arg attribute)
    public required string InputFile { get; init; }
}

// Usage
var app = MyApp.Parse(args);
```

**Subcommands:**
```csharp
[Command]
public partial class MyApp
{
    [Command]  // Marks this as a subcommand property
    public MyCommands? Command { get; init; }
}

[SubCommand]  // Marks this as a subcommand base type
public partial class MyCommands
{
    [Command]
    public partial class Build : MyCommands;

    [Command]
    public partial class Test : MyCommands;
}
```

**Custom Parsers:**
```csharp
// Define a custom type
public record struct Point(int X, int Y);

// Define a custom parser with static Parse(string) method
public static class PointParser
{
    public static Point Parse(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 2)
            throw new FormatException("Point must be in format 'x,y'");

        return new Point(int.Parse(parts[0]), int.Parse(parts[1]));
    }
}

// Use the parser in a command
[Command]
public partial class MyApp
{
    [Arg(Short = 'p', Long = "point", ValueParser = typeof(PointParser))]
    public required Point Point { get; init; }
}

// Usage: dotnet run -- --point "10,20"
```

**Important:** Custom parsers MUST have a static `Parse(string)` method. If the method is missing or has the wrong signature, you'll get diagnostic error **CLAP001**: "Custom parser type 'X' must have a static Parse(string) method for argument 'Y'".

## Recent Changes

Per git history:
- Fixed `NegatedFlag` namespacing issue
- Fully qualified `Environment` static class to avoid collisions
- Removed requirement for `SubCommand = true` on nested command properties
- Made `PrintHelpMessage` public
- Renamed `Lexer` to `ArgsLexer` to avoid user code collisions
