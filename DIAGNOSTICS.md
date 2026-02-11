# Clap.Net Diagnostics

This document describes the compile-time diagnostics provided by Clap.Net to help catch common CLI configuration errors.

## Overview

Clap.Net provides compile-time validation to detect configuration issues early in the development process. When the source generator detects problems with your CLI definition, it reports diagnostics that appear as warnings or errors in your IDE and build output.

## Diagnostic Codes

### CLAP001: Invalid Custom Parser
**Severity:** Error
**Message:** `Custom parser type '{type}' must have a static Parse(string) method for argument '{arg}'`

Triggered when a custom `ValueParser` type doesn't have the required `static Parse(string)` method.

**Example:**
```csharp
public static class MyParser
{
    // Missing: public static MyType Parse(string value)
}

[Command]
public partial class App
{
    [Arg(ValueParser = typeof(MyParser))]  // CLAP001 error
    public MyType Value { get; init; }
}
```

### CLAP002: Duplicate Short Flag
**Severity:** Error
**Message:** `Duplicate short flag '-{flag}' found on arguments '{arg1}' and '{arg2}'`

Triggered when multiple arguments use the same short flag character.

**Example:**
```csharp
[Command]
public partial class App
{
    [Arg(Short = 'v')]
    public bool Verbose { get; init; }

    [Arg(Short = 'v')]  // CLAP002 error - duplicate!
    public bool Version { get; init; }
}
```

### CLAP003: Duplicate Long Option
**Severity:** Error
**Message:** `Duplicate long option '--{option}' found on arguments '{arg1}' and '{arg2}'`

Triggered when multiple arguments use the same long option name.

**Example:**
```csharp
[Command]
public partial class App
{
    [Arg(Long = "output")]
    public string? Output1 { get; init; }

    [Arg(Long = "output")]  // CLAP003 error - duplicate!
    public string? Output2 { get; init; }
}
```

### CLAP004: Reserved Help Flag
**Severity:** Warning
**Message:** `Argument '{arg}' uses reserved help flag '{flag}'. This will override the built-in help functionality`

Triggered when an argument uses `-h` or `--help`, which are reserved for the built-in help system.

**Example:**
```csharp
[Command]
public partial class App
{
    [Arg(Short = 'h')]  // CLAP004 warning
    public bool Host { get; init; }
}
```

**Note:** This is a warning, not an error. You can intentionally override help flags if needed, but be aware of the implications.

### CLAP005: Reserved Version Flag Conflict
**Severity:** Warning
**Message:** `Argument '{arg}' uses flag '{flag}' which conflicts with the version flag for this command. Version will take precedence`

Triggered when an argument uses `-v` or `--version` on a command that has a `Version` property set.

**Example:**
```csharp
[Command(Version = "1.0.0")]
public partial class App
{
    [Arg(Short = 'v')]  // CLAP005 warning
    public bool Verbose { get; init; }
}
```

**Note:** The version flag will take precedence over your custom argument. Consider using a different flag like `-V` or `--verbose`.

### CLAP006: Invalid Short Flag Character
**Severity:** Error
**Message:** `Invalid short flag character '{char}' on argument '{arg}'. Short flags must be alphanumeric`

Triggered when a short flag uses a non-alphanumeric character.

**Example:**
```csharp
[Command]
public partial class App
{
    [Arg(Short = '@')]  // CLAP006 error - invalid character!
    public bool Invalid { get; init; }
}
```

**Valid:** `a-z`, `A-Z`, `0-9`
**Invalid:** `@`, `#`, `$`, `-`, `_`, space, etc.

### CLAP007: Invalid Long Option Name
**Severity:** Error
**Message:** `Invalid long option name '{name}' on argument '{arg}'. Long options must be non-empty and cannot contain whitespace`

Triggered when a long option name is empty, consists only of whitespace, or contains whitespace characters.

**Example:**
```csharp
[Command]
public partial class App
{
    [Arg(Long = "my option")]  // CLAP007 error - contains space!
    public bool Invalid { get; init; }

    [Arg(Long = "  ")]  // CLAP007 error - only whitespace!
    public bool AlsoInvalid { get; init; }
}
```

**Valid:** `output`, `log-level`, `max_size`
**Invalid:** `my option`, `  `, `log level`

### CLAP009: Multiple Subcommand Properties
**Severity:** Error
**Message:** `Command '{command}' defines multiple subcommand properties: '{prop1}' and '{prop2}'. Only one subcommand property is allowed`

Triggered when a command class has more than one property marked with `[Command]`.

**Example:**
```csharp
[Command]
public partial class App
{
    [Command]
    public Commands1? Command1 { get; init; }

    [Command]  // CLAP009 error - only one allowed!
    public Commands2? Command2 { get; init; }
}
```

**Correct approach:**
```csharp
[Command]
public partial class App
{
    [Command]
    public MyCommands? Command { get; init; }
}

[SubCommand]
public partial class MyCommands
{
    [Command]
    public partial class Build : MyCommands;

    [Command]
    public partial class Test : MyCommands;
}
```

## Diagnostic Categories

All diagnostics use the category `"Clap.Net"` and are enabled by default.

### Error vs. Warning

- **Errors** (CLAP002, CLAP003, CLAP006, CLAP007, CLAP009): Prevent compilation. These represent configuration mistakes that would cause runtime failures or generate invalid code.

- **Warnings** (CLAP004, CLAP005): Allow compilation but alert you to potential issues. These represent situations where you might be overriding built-in functionality unintentionally.

## Testing Diagnostics

The `Clap.Net.Tests/DiagnosticTestCommands.cs` file contains example commands that intentionally trigger each diagnostic. These serve as both documentation and verification that the diagnostics work correctly.

To see diagnostics in action, uncomment the test commands in that file and build the project - you'll see the appropriate warnings and errors in your build output.

## Implementation

Diagnostics are implemented in:
- **`Clap.Net/Validation/CommandValidator.cs`**: Core validation logic
- **`Clap.Net/ClapGenerator.cs`**: Integration point that calls validation before code generation

The validation runs during source generation, so diagnostics appear immediately in your IDE as you type, just like compiler errors and warnings.
