using Shouldly;

namespace Clap.Net.Tests;

public class Tests
{
    [Fact]
    public void ShortFlag_IsTrue_WhenPresent()
    {
        var args = ShortFlagApp.TryParse(["-a"]);
        args.AsT0.All.ShouldBeTrue();
    }

    [Fact]
    public void ShortFlag_IsTrue_WhenPresentAndFollowedByWord()
    {
        var args = FlagFollowedByBoolArgsApp.TryParse(["-t", "simon"]);
        args.AsT0.Trim.ShouldBeTrue();
        args.AsT0.Word.Length.ShouldBe(5);
    }

    [Fact]
    public void ShortFlag_IsFalse_WhenNotPresent()
    {
        var args = ShortFlagApp.TryParse(ReadOnlySpan<IToken>.Empty);
        args.AsT0.All.ShouldBeFalse();
    }

    [Fact]
    public void LongFlag_IsTrue_WhenPresent()
    {
        var args = LongFlagApp.TryParse(["--all"]);
        args.AsT0.All.ShouldBeTrue();
    }

    [Fact]
    public void LongFlag_IsFalse_WhenNegated()
    {
        var args = LongFlagDefaultTrueApp.TryParse(["--no-all"]);
        args.AsT0.All.ShouldBeFalse();
    }

    [Fact]
    public void LongFlag_IsFalse_WhenNotPresent()
    {
        var args = LongFlagApp.TryParse(ReadOnlySpan<IToken>.Empty);
        args.AsT0.All.ShouldBeFalse();
    }

    [Fact]
    public void BasicRequiredPositionalArg_IsSet_WhenPresent()
    {
        var args = BasicRequiredPositionalApp.TryParse(["foo"]);
        args.AsT0.Subject.ShouldBe("foo");
    }

    [Fact]
    public void DoubleRequiredPositionalApp_IsSet_WhenPresent()
    {
        var args = DoubleRequiredPositionalApp.TryParse(["foo", "bar"]);
        args.AsT0.Subject.ShouldBe("foo");
        args.AsT0.Body.ShouldBe("bar");
    }

    [Fact]
    public void SubCommandApp_IsSet_WhenPresent()
    {
        var args = SubCommandApp.TryParse(["one"]);
        args.AsT0.Command.ShouldBeOfType<SubCommandCommand.One>();
    }

    [Fact]
    public void CombinedShortFlags_Allowed_InSingleToken()
    {
        var result = CombinedShortFlagsApp.TryParse(new[] { "-ab" });
        // Should be the T0 case
        result.IsT0.ShouldBeTrue();
        var args = result.AsT0;
        args.All.ShouldBeTrue();
        args.Bold.ShouldBeTrue();
    }

    [Fact]
    public void OptionWithValue_CanBeParsed_UsingShortAndLong()
    {
        var shortResult = OptionWithValueApp.TryParse(new[] { "-n", "Alice" });
        shortResult.IsT0.ShouldBeTrue();
        shortResult.AsT0.Name.ShouldBe("Alice");

        var longResult = OptionWithValueApp.TryParse(new[] { "--name", "Bob" });
        longResult.IsT0.ShouldBeTrue();
        longResult.AsT0.Name.ShouldBe("Bob");

        var eqResult = OptionWithValueApp.TryParse(new[] { "--name=Carol" });
        eqResult.IsT0.ShouldBeTrue();
        eqResult.AsT0.Name.ShouldBe("Carol");
    }

    [Fact]
    public void AppendOption_CollectsMultipleValues()
    {
        var result = AppendOptionApp.TryParse(
            new[]
            {
                "-t", "one",
                "--tag", "two",
                "-t", "three"
            });
        result.IsT0.ShouldBeTrue();
        result.AsT0.Tags.ShouldBeSubsetOf(["one", "two", "three"]);
    }

    [Fact]
    public void CountFlag_IncrementsCorrectly()
    {
        var result = CountFlagApp.TryParse(new[] { "-vvv" });
        result.IsT0.ShouldBeTrue();
        result.AsT0.Verbosity.ShouldBe(3);
    }

    [Fact]
    public void HelpFlag_IsTrue_WhenPresent()
    {
        var result = HelpApp.TryParse(new[] { "--help" });
        result.IsT1.ShouldBeTrue("Expected ShowHelp on help flag");
    }

    [Fact]
    public void MissingRequiredOption_ReturnsParseError()
    {
        var result = RequiredOptionApp.TryParse(ReadOnlySpan<IToken>.Empty);
        result.IsT3.ShouldBeTrue("Expected ParseError when required missing");
        result.AsT3.Message.ShouldContain("required", Case.Sensitive, "Error message should mention required");
    }

    [Fact]
    public void UnexpectedCompoundFlag_ReturnsParseError()
    {
        var result = CountFlagApp.TryParse(["-pp"]);
        result.IsT3.ShouldBeTrue("Expected ParseError when required missing");
        result.AsT3.Message.ShouldBe("Unexpected flag supplied in compound flags 'p'");
    }

    [Fact]
    public void TrailingParams_AreCollected_WhenPresent()
    {
        var result = TrailingArgsApp.TryParse(["example", "example1", "example2"]);
        result.IsT0.ShouldBeTrue();
        result.AsT0.FirstWord.ShouldBe("example");
        result.AsT0.RestOfTheWords.Length.ShouldBe(2);
        result.AsT0.RestOfTheWords[0].ShouldBe("example1");
        result.AsT0.RestOfTheWords[1].ShouldBe("example2");
    }

    [Fact]
    public void TrailingParams_IsEmpty_WhenNotPresent()
    {
        var result = TrailingArgsApp.TryParse(["example"]);
        result.IsT0.ShouldBeTrue();
        result.AsT0.FirstWord.ShouldBe("example");
        result.AsT0.RestOfTheWords.ShouldNotBeNull();
        result.AsT0.RestOfTheWords.Length.ShouldBe(0);
    }

    [Fact]
    public void EnvironmentArg_IsPopulated_WhenPresent()
    {
        Environment.SetEnvironmentVariable(EnvironmentArgsApp.EnvName, "Simon");
        var result = EnvironmentArgsApp.TryParse(ReadOnlySpan<IToken>.Empty);
        result.IsT0.ShouldBeTrue();
        result.AsT0.Name.ShouldBe("Simon");
    }

    [Fact]
    public void EnvironmentArg_ShouldUseFlagValue_WhenFlagIsPresent()
    {
        Environment.SetEnvironmentVariable(EnvironmentArgsApp.EnvName, "Simon");
        var result = EnvironmentArgsApp.TryParse(["--name", "Dave"]);
        result.IsT0.ShouldBeTrue();
        result.AsT0.Name.ShouldBe("Dave");
    }

    [Fact]
    public void ParentApp_HasRequiredSubCommand_WhenPresent()
    {
        var result = ParentApp.TryParse(["-d", ParentCommand.ChildCommand.Name, "-p"]);
        result.IsT0.ShouldBeTrue();
        result.AsT0.Dodgems.ShouldBeTrue();
        result.AsT0.Command.ShouldNotBeNull();
        var command = result.AsT0.Command.ShouldBeOfType<ParentCommand.ChildCommand>();
        command.IsPresent.ShouldBeTrue();
    }

    [Fact]
    public void ParentWithoutChildApp_HasOptionalSubCommand_WhenPresent()
    {
        var result = ParentWithoutChildApp.TryParse(["-d", ParentCommand.ChildCommand.Name, "-p"]);
        result.IsT0.ShouldBeTrue();
        result.AsT0.Dodgems.ShouldBeTrue();
        result.AsT0.Command.ShouldNotBeNull();
        var command = result.AsT0.Command.ShouldBeOfType<ParentCommand.ChildCommand>();
        command.IsPresent.ShouldBeTrue();
    }

    [Fact]
    public void ParentWithoutChildApp_DoesntHaveChild_WhenNotProvided()
    {
        var result = ParentWithoutChildApp.TryParse(["-d"]);
        result.IsT0.ShouldBeTrue();
        result.AsT0.Dodgems.ShouldBeTrue();
        result.AsT0.Command.ShouldBeNull();
    }
}

[Command]
public partial class ShortFlagApp
{
    [Arg(Short = 'a')]
    public bool All { get; init; }
}

[Command]
public partial class LongFlagApp
{
    [Arg(Long = "all")]
    public bool All { get; init; }
}

[Command]
public partial class LongFlagDefaultTrueApp
{
    [Arg(Long = "all", Negation = true)]
    public bool All { get; init; } = true;
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
    [Command(Subcommand = true)]
    public required SubCommandCommand Command { get; init; }
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
    [Arg(Short = 'a')]
    public bool All { get; init; }

    [Arg(Short = 'b')]
    public bool Bold { get; init; }
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
    public IEnumerable<string> Tags { get; init; } = [];
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
    [Arg(Short = 'h', Long = "help", Negation = true, Action = ArgAction.Help)]
    public bool Help { get; init; }
}

[Command]
public partial class RequiredOptionApp
{
    [Arg(Short = 'r', Long = "req")]
    public required int Req { get; init; }
}

[Command]
public partial class FlagFollowedByBoolArgsApp
{
    [Arg(Short = 't')]
    public bool Trim { get; init; }

    [Arg]
    public required string Word { get; init; }
}

[Command]
public partial class TrailingArgsApp
{
    [Arg]
    public required string FirstWord { get; init; }

    [Arg]
    public string[] RestOfTheWords { get; init; } = [];
}

[Command]
public partial class EnvironmentArgsApp
{
    public const string EnvName = "APP_NAME";

    [Arg(Short = 'n', Long = "name", Env = EnvName)]
    public required string Name { get; init; }
}

[Command]
public partial class ParentApp
{
    [Arg(Short = 'd')]
    public bool Dodgems { get; init; }

    [Command(Subcommand = true)]
    public required ParentCommand Command { get; init; }
}

[Command]
public partial class ParentWithoutChildApp
{
    [Arg(Short = 'd')]
    public bool Dodgems { get; init; }

    [Command(Subcommand = true)]
    public ParentCommand? Command { get; init; }
}

[SubCommand]
public partial class ParentCommand
{
    [Command(Name = Name)]
    public partial class ChildCommand : ParentCommand
    {
        public const string Name = "child_command";

        [Arg(Short = 'p', Long = "present")]
        public required bool IsPresent { get; init; }
    }
}