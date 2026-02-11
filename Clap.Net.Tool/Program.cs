using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Clap.Net;
using Clap.Net.Tool;
using Clap.Net.Tool.Completions;

// Helper methods to safely extract attribute properties using proper reflection
static string? GetCommandName(Attribute? attr) =>
    attr?.GetType().GetProperty("Name")?.GetValue(attr) as string;

static string? GetCommandAbout(Attribute? attr) =>
    attr?.GetType().GetProperty("About")?.GetValue(attr) as string;

static string? GetCommandVersion(Attribute? attr) =>
    attr?.GetType().GetProperty("Version")?.GetValue(attr) as string;

static char? GetArgShort(Attribute? attr) =>
    attr?.GetType().GetProperty("Short")?.GetValue(attr) as char?;

static string? GetArgLong(Attribute? attr) =>
    attr?.GetType().GetProperty("Long")?.GetValue(attr) as string;

static string? GetArgHelp(Attribute? attr) =>
    attr?.GetType().GetProperty("Help")?.GetValue(attr) as string;

var result = ClapTool.Parse(args);
await GenerateOutputAsync(result);
return 0;

static async Task GenerateOutputAsync(ClapTool tool)
{
    try
    {
        if (!File.Exists(tool.AssemblyPath))
        {
            Console.Error.WriteLine($"Error: Assembly not found: {tool.AssemblyPath}");
            Environment.Exit(1);
        }

        // Validate and load assembly securely
        var fullPath = Path.GetFullPath(tool.AssemblyPath);
        var currentDir = Environment.CurrentDirectory;

        // Ensure assembly is in current directory or subdirectory
        if (!fullPath.StartsWith(currentDir, StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Error: Assembly must be in current directory or subdirectory for security.");
            Console.Error.WriteLine($"  Assembly path: {fullPath}");
            Console.Error.WriteLine($"  Current directory: {currentDir}");
            Environment.Exit(1);
        }

        Console.WriteLine($"Loading assembly: {fullPath}");
        var assembly = Assembly.LoadFile(fullPath);

        // Find types with [Cli] attribute
        var cliTypes = assembly.GetTypes()
            .Where(t => t.GetCustomAttribute(typeof(CliAttribute)) is not null)
            .ToList();

        if (!cliTypes.Any())
        {
            Console.WriteLine("No types with [Cli] attribute found in assembly.");
            return;
        }

        var format = tool.Format?.ToLowerInvariant() ?? "json";

        foreach (var cliType in cliTypes)
        {
            Console.WriteLine($"Generating {format} for: {cliType.Name}");

            var commandAttr = cliType.GetCustomAttribute(typeof(CommandAttribute));
            var commandName = tool.CommandName ?? GetCommandName(commandAttr) ?? cliType.Name;

            string output;
            string defaultFileName;

            if (format == "json")
            {
                output = GenerateOpenCliSchema(cliType, commandAttr, assembly);
                defaultFileName = $"{commandName}-cli.json";
            }
            else
            {
                // Generate completion script
                var context = ExtractCompletionContext(cliType, commandName, commandAttr, assembly);
                ICompletionGenerator generator = format switch
                {
                    "bash" => new BashCompletionGenerator(),
                    "zsh" => new ZshCompletionGenerator(),
                    "fish" => new FishCompletionGenerator(),
                    "powershell" => new PowerShellCompletionGenerator(),
                    _ => throw new ArgumentException($"Unknown format: {format}. Supported formats: json, bash, zsh, fish, powershell")
                };

                output = generator.Generate(context);
                defaultFileName = generator.ShellName == "zsh"
                    ? $"_{commandName}"
                    : $"{commandName}-completion{generator.FileExtension}";
            }

            var outputFile = tool.OutputPath
                ?? Path.Combine(
                    Path.GetDirectoryName(tool.AssemblyPath) ?? ".",
                    defaultFileName);

            await File.WriteAllTextAsync(outputFile, output);
            Console.WriteLine($"Output written to: {outputFile}");
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (ex.InnerException is not null)
            Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
        Environment.Exit(1);
    }
}

static string GenerateOpenCliSchema(Type cliType, Attribute? commandAttr, Assembly assembly)
{
    using var stream = new MemoryStream();
    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

    writer.WriteStartObject();
    writer.WriteString("opencli", "0.1");

    // Info section
    writer.WriteStartObject("info");
    writer.WriteString("title", GetCommandName(commandAttr) ?? cliType.Name);

    var about = GetCommandAbout(commandAttr);
    if (about is not null)
        writer.WriteString("description", about);

    writer.WriteString("version", GetCommandVersion(commandAttr) ?? assembly.GetName().Version?.ToString() ?? "1.0.0");
    writer.WriteEndObject();

    // Analyze properties for arguments and options
    var properties = cliType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    // Positional arguments (properties without [Arg] or with [Arg] but no Short/Long)
    var positionalArgs = properties
        .Where(p =>
        {
            if (IsSubCommand(p)) return false;

            var argAttr = p.GetCustomAttribute(typeof(ArgAttribute));
            if (argAttr is null)
                return true; // Properties without [Arg] are positional

            // Check if Short or Long properties are set
            var shortProp = argAttr.GetType().GetProperty("Short");
            var longProp = argAttr.GetType().GetProperty("Long");

            var shortValue = shortProp?.GetValue(argAttr) as char? ?? '\0';
            var longValue = longProp?.GetValue(argAttr) as string;

            // It's positional if both Short and Long are unset (Short = '\0', Long = null)
            return shortValue == '\0' && string.IsNullOrEmpty(longValue);
        })
        .ToList();

    if (positionalArgs.Any())
    {
        writer.WriteStartArray("arguments");
        foreach (var prop in positionalArgs)
        {
            writer.WriteStartObject();
            writer.WriteString("name", prop.Name);

            var argAttr = prop.GetCustomAttribute(typeof(ArgAttribute));
            var help = GetArgHelp(argAttr);
            if (help is not null)
                writer.WriteString("description", help);

            var isRequired = prop.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() is not null
                || (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is null);
            writer.WriteBoolean("required", isRequired);

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    // Named options (properties with [Arg(Short/Long)])
    var namedOptions = properties
        .Where(p => !IsSubCommand(p))
        .Select(p =>
        {
            var attr = p.GetCustomAttribute(typeof(ArgAttribute));
            if (attr is null) return null;

            var shortProp = attr.GetType().GetProperty("Short");
            var longProp = attr.GetType().GetProperty("Long");

            var shortValue = shortProp?.GetValue(attr) as char? ?? '\0';
            var longValue = longProp?.GetValue(attr) as string;

            // Skip if neither Short nor Long are set
            if (shortValue == '\0' && string.IsNullOrEmpty(longValue)) return null;

            return new
            {
                Property = p,
                Attr = attr,
                Short = shortValue,
                Long = longValue
            };
        })
        .Where(x => x is not null)
        .ToList()!;

    if (namedOptions.Any())
    {
        writer.WriteStartArray("options");
        foreach (var option in namedOptions)
        {
            writer.WriteStartObject();

            // Primary name (prefer long form)
            string primaryName = option.Long ?? (option.Short != '\0' ? option.Short.ToString() : option.Property.Name);
            writer.WriteString("name", primaryName);

            // Aliases
            if (option.Short != '\0' && !string.IsNullOrEmpty(option.Long))
            {
                writer.WriteStartArray("aliases");
                writer.WriteStartObject();
                writer.WriteString("name", option.Short.ToString());
                writer.WriteEndObject();
                writer.WriteEndArray();
            }

            // Get help text from attribute
            var helpProp = option.Attr.GetType().GetProperty("Help");
            var helpValue = helpProp?.GetValue(option.Attr) as string;
            if (helpValue is not null)
                writer.WriteString("description", helpValue);

            var isRequired = option.Property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() is not null
                || (option.Property.PropertyType.IsValueType && Nullable.GetUnderlyingType(option.Property.PropertyType) is null);
            writer.WriteBoolean("required", isRequired);

            // For options that take values (not boolean flags)
            if (option.Property.PropertyType != typeof(bool))
            {
                writer.WriteStartArray("arguments");
                writer.WriteStartObject();
                writer.WriteString("name", "value");
                writer.WriteBoolean("required", true);
                writer.WriteEndObject();
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    writer.WriteEndObject();
    writer.Flush();

    return Encoding.UTF8.GetString(stream.ToArray());
}

static bool IsSubCommand(PropertyInfo prop)
{
    return prop.GetCustomAttribute(typeof(CommandAttribute)) is not null
        || prop.PropertyType.GetCustomAttribute(typeof(SubCommandAttribute)) is not null;
}

static List<CompletionOption> ExtractOptions(PropertyInfo[] properties)
{
    var options = new List<CompletionOption>();
    foreach (var prop in properties)
    {
        if (IsSubCommand(prop)) continue;

        var argAttr = prop.GetCustomAttribute(typeof(ArgAttribute));
        if (argAttr is null) continue;

        var shortValue = GetArgShort(argAttr) ?? '\0';
        var longValue = GetArgLong(argAttr);

        // Skip if neither Short nor Long are set (positional)
        if (shortValue == '\0' && string.IsNullOrEmpty(longValue)) continue;

        var isFlag = prop.PropertyType == typeof(bool);
        var isRequired = prop.GetCustomAttribute<RequiredAttribute>() is not null
            || (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is null);

        options.Add(new CompletionOption(
            shortValue != '\0' ? shortValue : null,
            longValue,
            GetArgHelp(argAttr),
            isFlag,
            isRequired
        ));
    }
    return options;
}

static List<CompletionPositional> ExtractPositionals(PropertyInfo[] properties)
{
    var positionals = new List<CompletionPositional>();
    var positionalProps = properties
        .Where(p =>
        {
            if (IsSubCommand(p)) return false;

            var argAttr = p.GetCustomAttribute(typeof(ArgAttribute));
            if (argAttr is null) return true;

            var shortProp = argAttr.GetType().GetProperty("Short");
            var longProp = argAttr.GetType().GetProperty("Long");

            var shortValue = shortProp?.GetValue(argAttr) as char? ?? '\0';
            var longValue = longProp?.GetValue(argAttr) as string;

            return shortValue == '\0' && string.IsNullOrEmpty(longValue);
        })
        .ToList();

    int index = 0;
    foreach (var prop in positionalProps)
    {
        var argAttr = prop.GetCustomAttribute(typeof(ArgAttribute));
        var isRequired = prop.GetCustomAttribute<RequiredAttribute>() is not null
            || (prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is null);

        positionals.Add(new CompletionPositional(
            prop.Name,
            GetArgHelp(argAttr),
            index++,
            isRequired
        ));
    }
    return positionals;
}

static CompletionContext ExtractCompletionContext(Type cliType, string commandName, Attribute? commandAttr, Assembly assembly)
{
    var about = GetCommandAbout(commandAttr);

    var properties = cliType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    // Extract options and positionals using helper methods
    var options = ExtractOptions(properties);
    var positionals = ExtractPositionals(properties);

    // Extract subcommands
    var subCommands = new List<CompletionSubCommand>();
    foreach (var prop in properties)
    {
        if (!IsSubCommand(prop)) continue;

        var subCommandType = prop.PropertyType;

        // If the property type has [SubCommand] attribute, find nested command types
        if (subCommandType.GetCustomAttribute(typeof(SubCommandAttribute)) is not null)
        {
            var nestedTypes = subCommandType.GetNestedTypes(BindingFlags.Public);
            foreach (var nestedType in nestedTypes)
            {
                var nestedCommandAttr = nestedType.GetCustomAttribute(typeof(CommandAttribute));
                if (nestedCommandAttr is null) continue;

                var nestedName = GetCommandName(nestedCommandAttr) ?? nestedType.Name;
                var nestedAbout = GetCommandAbout(nestedCommandAttr);

                subCommands.Add(ExtractSubCommand(nestedType, nestedName, nestedAbout));
            }
        }
        else
        {
            // Single subcommand type
            var subCommandAttr = subCommandType.GetCustomAttribute(typeof(CommandAttribute));
            if (subCommandAttr is not null)
            {
                var subName = GetCommandName(subCommandAttr) ?? subCommandType.Name;
                var subAbout = GetCommandAbout(subCommandAttr);

                subCommands.Add(ExtractSubCommand(subCommandType, subName, subAbout));
            }
        }
    }

    return new CompletionContext(commandName, about, options, positionals, subCommands);
}

static CompletionSubCommand ExtractSubCommand(Type commandType, string name, string? about)
{
    var properties = commandType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

    // Extract options and positionals using helper methods
    var options = ExtractOptions(properties);
    var positionals = ExtractPositionals(properties);

    // Recursively extract nested subcommands
    var nestedSubCommands = new List<CompletionSubCommand>();
    foreach (var prop in properties)
    {
        if (!IsSubCommand(prop)) continue;

        var subCommandType = prop.PropertyType;

        if (subCommandType.GetCustomAttribute(typeof(SubCommandAttribute)) is not null)
        {
            var nestedTypes = subCommandType.GetNestedTypes(BindingFlags.Public);
            foreach (var nestedType in nestedTypes)
            {
                var nestedCommandAttr = nestedType.GetCustomAttribute(typeof(CommandAttribute));
                if (nestedCommandAttr is null) continue;

                var nestedName = GetCommandName(nestedCommandAttr) ?? nestedType.Name;
                var nestedAbout = GetCommandAbout(nestedCommandAttr);

                nestedSubCommands.Add(ExtractSubCommand(nestedType, nestedName, nestedAbout));
            }
        }
        else
        {
            var subCommandAttr = subCommandType.GetCustomAttribute(typeof(CommandAttribute));
            if (subCommandAttr is not null)
            {
                var subName = GetCommandName(subCommandAttr) ?? subCommandType.Name;
                var subAbout = GetCommandAbout(subCommandAttr);

                nestedSubCommands.Add(ExtractSubCommand(subCommandType, subName, subAbout));
            }
        }
    }

    return new CompletionSubCommand(name, about, options, positionals, nestedSubCommands);
}
