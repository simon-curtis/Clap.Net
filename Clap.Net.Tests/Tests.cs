namespace Clap.Net.Tests;

public class Tests
{
    [Test]
    public async Task ShortFlag_IsTrue_WhenPresent()
    {
        var args = ShortFlagApp.TryParse(["-a"]);
        await Assert.That(args.AsT0.All).IsTrue();
    }

    [Test]
    public async Task ShortFlag_IsFalse_WhenNotPresent()
    {
        var args = ShortFlagApp.TryParse([]);
        await Assert.That(args.AsT0.All).IsFalse();
    }

    [Test]
    public async Task LongFlag_IsTrue_WhenPresent()
    {
        var args = LongFlagApp.TryParse(["--all"]);
        await Assert.That(args.AsT0.All).IsTrue();
    }

    [Test]
    public async Task LongFlag_IsFalse_WhenNotPresent()
    {
        var args = LongFlagApp.TryParse([]);
        await Assert.That(args.AsT0.All).IsFalse();
    }

    [Test]
    public async Task BasicRequiredPositionalArg_IsSet_WhenPresent()
    {
        var args = BasicRequiredPositionalApp.TryParse(["foo"]);
        await Assert.That(args.AsT0.Subject).IsEqualTo("foo");
    }

    [Test]
    public async Task DoubleRequiredPositionalApp_IsSet_WhenPresent()
    {
        var args = DoubleRequiredPositionalApp.TryParse(["foo", "bar"]);
        await Assert.That(args.AsT0.Subject).IsEqualTo("foo");
        await Assert.That(args.AsT0.Body).IsEqualTo("bar");
    }

    [Test]
    public async Task SubCommandApp_IsSet_WhenPresent()
    {
        var args = SubCommandApp.TryParse(["one"]);
        await Assert.That(args.AsT0.Command).IsTypeOf<SubCommandCommand.One>();
    }

    [Test]
    public async Task CombinedShortFlags_Allowed_InSingleToken()
    {
        var result = CombinedShortFlagsApp.TryParse(new[] { "-ab" });
        // Should be the T0 case
        await Assert.That(result.IsT0).IsTrue();
        var args = result.AsT0;
        await Assert.That(args.All).IsTrue();
        await Assert.That(args.Bold).IsTrue();
    }

    [Test]
    public async Task OptionWithValue_CanBeParsed_UsingShortAndLong()
    {
        var shortResult = OptionWithValueApp.TryParse(new[] { "-n", "Alice" });
        await Assert.That(shortResult.IsT0).IsTrue();
        await Assert.That(shortResult.AsT0.Name).IsEqualTo("Alice");

        var longResult = OptionWithValueApp.TryParse(new[] { "--name", "Bob" });
        await Assert.That(longResult.IsT0).IsTrue();
        await Assert.That(longResult.AsT0.Name).IsEqualTo("Bob");

        var eqResult = OptionWithValueApp.TryParse(new[] { "--name=Carol" });
        await Assert.That(eqResult.IsT0).IsTrue();
        await Assert.That(eqResult.AsT0.Name).IsEqualTo("Carol");
    }

    [Test]
    public async Task AppendOption_CollectsMultipleValues()
    {
        var result = AppendOptionApp.TryParse(new[]
        {
            "-t", "one",
            "--tag", "two",
            "-t", "three"
        });
        await Assert.That(result.IsT0).IsTrue();
        await Assert.That(result.AsT0.Tags).IsEquivalentTo(["one", "two", "three"]);
    }

    [Test]
    public async Task CountFlag_IncrementsCorrectly()
    {
        var result = CountFlagApp.TryParse(new[] { "-vvv" });
        await Assert.That(result.IsT0).IsTrue();
        await Assert.That(result.AsT0.Verbosity).IsEqualTo(3);
    }

    [Test]
    public async Task HelpFlag_ReturnsShowHelp()
    {
        var result = HelpApp.TryParse(new[] { "--help" });
        await Assert.That(result.IsT1).IsTrue().Because("Expected ShowHelp on help flag");
    }

    [Test]
    public async Task MissingRequiredOption_ReturnsParseError()
    {
        var result = RequiredOptionApp.TryParse([]);
        await Assert.That(result.IsT3).IsTrue().Because("Expected ParseError when required missing");
        await Assert.That(result.AsT3.Message).Contains("required")
            .Because("Error message should mention required");
    }
}

[Command]
public partial class ShortFlagApp
{
    [Arg(Short = 'a')] public bool All { get; init; }
}

[Command]
public partial class LongFlagApp
{
    [Arg(Long = "all")] public bool All { get; init; }
}

[Command]
public partial class BasicRequiredPositionalApp
{
    public required string Subject { get; init; }
}

[Command]
public partial class DoubleRequiredPositionalApp
{
    public required string Subject { get; init; }
    public required string Body { get; init; }
}

[Command]
public partial class SubCommandApp
{
    [Command(Traits.Subcommand)] public required SubCommandCommand Command { get; init; }
}

[SubCommand]
public partial class SubCommandCommand
{
    [Command]
    public partial class One : SubCommandCommand;

    [Command]
    public partial class Two : SubCommandCommand;
}

[Command]
public partial class CombinedShortFlagsApp
{
    [Arg(Short = 'a')] public bool All { get; init; }
    [Arg(Short = 'b')] public bool Bold { get; init; }
}

[Command]
public partial class OptionWithValueApp
{
    [Arg(Short = 'n', Long = "name", Help = "User name")]
    public required string Name { get; init; }
}

[Command]
public partial class AppendOptionApp
{
    [Arg(Short = 't', Long = "tag", Action = ArgAction.Append)]
    public IEnumerable<string> Tags { get; init; } = Enumerable.Empty<string>();
}

[Command]
public partial class CountFlagApp
{
    [Arg(Short = 'v', Action = ArgAction.Count)]
    public int Verbosity { get; init; }
}

[Command]
public partial class HelpApp
{
    [Arg(Short = 'h', Long = "help", Action = ArgAction.Help)]
    public bool Help { get; init; }
}

[Command]
public partial class RequiredOptionApp
{
    [Arg(Short = 'r', Long = "req")] public required int Req { get; init; }
}