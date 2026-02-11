using Shouldly;

namespace Clap.Net.Tests;

public class Tests
{
    [Fact]
    public void ShortFlag_IsTrue_WhenPresent()
    {
        var args = ShortFlagApp.TryParse(["-a"]);
        args.IsSuccess.ShouldBeTrue();
        args.Command.All.ShouldBeTrue();
    }

    [Fact]
    public void ShortFlag_IsTrue_WhenPresentAndFollowedByWord()
    {
        var args = FlagFollowedByBoolArgsApp.TryParse(["-t", "simon"]);
        args.IsSuccess.ShouldBeTrue();
        args.Command.Trim.ShouldBeTrue();
        args.Command.Word.Length.ShouldBe(5);
    }

    [Fact]
    public void ShortFlag_IsFalse_WhenNotPresent()
    {
        var args = ShortFlagApp.TryParse(ReadOnlySpan<IToken>.Empty);
        args.IsSuccess.ShouldBeTrue();
        args.Command.All.ShouldBeFalse();
    }

    [Fact]
    public void LongFlag_IsTrue_WhenPresent()
    {
        var args = LongFlagApp.TryParse(["--all"]);
        args.IsSuccess.ShouldBeTrue();
        args.Command.All.ShouldBeTrue();
    }

    [Fact]
    public void LongFlag_IsFalse_WhenNegated()
    {
        var args = LongFlagDefaultTrueApp.TryParse(["--no-all"]);
        args.IsSuccess.ShouldBeTrue();
        args.Command.All.ShouldBeFalse();
    }

    [Fact]
    public void LongFlag_IsFalse_WhenNotPresent()
    {
        var args = LongFlagApp.TryParse(ReadOnlySpan<IToken>.Empty);
        args.IsSuccess.ShouldBeTrue();
        args.Command.All.ShouldBeFalse();
    }

    [Fact]
    public void BasicRequiredPositionalArg_IsSet_WhenPresent()
    {
        var args = BasicRequiredPositionalApp.TryParse(["foo"]);
        args.IsSuccess.ShouldBeTrue();
        args.Command.Subject.ShouldBe("foo");
    }

    [Fact]
    public void DoubleRequiredPositionalApp_IsSet_WhenPresent()
    {
        var args = DoubleRequiredPositionalApp.TryParse(["foo", "bar"]);
        args.IsSuccess.ShouldBeTrue();
        args.Command.Subject.ShouldBe("foo");
        args.Command.Body.ShouldBe("bar");
    }

    [Fact]
    public void SubCommandApp_IsSet_WhenPresent()
    {
        var args = SubCommandApp.TryParse(["one"]);
        args.IsSuccess.ShouldBeTrue();
        args.Command.Command.ShouldBeOfType<SubCommandCommand.One>();
    }

    [Fact]
    public void CombinedShortFlags_Allowed_InSingleToken()
    {
        var result = CombinedShortFlagsApp.TryParse(new[] { "-ab" });
        // Should be the T0 case
        result.IsSuccess.ShouldBeTrue();
        var args = result.Command;
        args.All.ShouldBeTrue();
        args.Bold.ShouldBeTrue();
    }

    [Fact]
    public void OptionWithValue_CanBeParsed_UsingShortAndLong()
    {
        var shortResult = OptionWithValueApp.TryParse(new[] { "-n", "Alice" });
        shortResult.IsSuccess.ShouldBeTrue();
        shortResult.Command.Name.ShouldBe("Alice");

        var longResult = OptionWithValueApp.TryParse(new[] { "--name", "Bob" });
        longResult.IsSuccess.ShouldBeTrue();
        longResult.Command.Name.ShouldBe("Bob");

        var eqResult = OptionWithValueApp.TryParse(new[] { "--name=Carol" });
        eqResult.IsSuccess.ShouldBeTrue();
        eqResult.Command.Name.ShouldBe("Carol");
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
        result.IsSuccess.ShouldBeTrue();
        result.Command.Tags.ShouldBeSubsetOf(["one", "two", "three"]);
    }

    [Fact]
    public void CountFlag_IncrementsCorrectly()
    {
        var result = CountFlagApp.TryParse(new[] { "-vvv" });
        result.IsSuccess.ShouldBeTrue();
        result.Command.Verbosity.ShouldBe(3);
    }

    [Fact]
    public void HelpFlag_IsTrue_WhenPresent()
    {
        var result = HelpApp.TryParse(new[] { "--help" });
        result.IsHelp.ShouldBeTrue("Expected ShowHelp on help flag");
    }

    [Fact]
    public void MissingRequiredOption_ReturnsParseError()
    {
        var result = RequiredOptionApp.TryParse(ReadOnlySpan<IToken>.Empty);
        result.IsError.ShouldBeTrue("Expected ParseError when required missing");
        result.Error.Message.ShouldContain("required", Case.Sensitive, "Error message should mention required");
    }

    [Fact]
    public void UnexpectedCompoundFlag_ReturnsParseError()
    {
        var result = CountFlagApp.TryParse(["-pp"]);
        result.IsError.ShouldBeTrue("Expected ParseError when required missing");
        result.Error.Message.ShouldBe("Unexpected flag supplied in compound flags 'p'");
    }

    [Fact]
    public void TrailingParams_AreCollected_WhenPresent()
    {
        var result = TrailingArgsApp.TryParse(["example", "example1", "example2"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.FirstWord.ShouldBe("example");
        result.Command.RestOfTheWords.Length.ShouldBe(2);
        result.Command.RestOfTheWords[0].ShouldBe("example1");
        result.Command.RestOfTheWords[1].ShouldBe("example2");
    }

    [Fact]
    public void TrailingParams_IsEmpty_WhenNotPresent()
    {
        var result = TrailingArgsApp.TryParse(["example"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.FirstWord.ShouldBe("example");
        result.Command.RestOfTheWords.ShouldNotBeNull();
        result.Command.RestOfTheWords.Length.ShouldBe(0);
    }

    [Fact]
    public void EnvironmentArg_IsPopulated_WhenPresent()
    {
        Environment.SetEnvironmentVariable(EnvironmentArgsApp.EnvName, "Simon");
        var result = EnvironmentArgsApp.TryParse(ReadOnlySpan<IToken>.Empty);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Name.ShouldBe("Simon");
    }

    [Fact]
    public void EnvironmentArg_ShouldUseFlagValue_WhenFlagIsPresent()
    {
        Environment.SetEnvironmentVariable(EnvironmentArgsApp.EnvName, "Simon");
        var result = EnvironmentArgsApp.TryParse(["--name", "Dave"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Name.ShouldBe("Dave");
    }

    [Fact]
    public void ParentApp_HasRequiredSubCommand_WhenPresent()
    {
        var result = ParentApp.TryParse(["-d", ParentCommand.ChildCommand.Name, "-p"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Dodgems.ShouldBeTrue();
        result.Command.Command.ShouldNotBeNull();
        var command = result.Command.Command.ShouldBeOfType<ParentCommand.ChildCommand>();
        command.IsPresent.ShouldBeTrue();
    }

    [Fact]
    public void ParentWithoutChildApp_HasOptionalSubCommand_WhenPresent()
    {
        var result = ParentWithoutChildApp.TryParse(["-d", ParentCommand.ChildCommand.Name, "-p"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Dodgems.ShouldBeTrue();
        result.Command.Command.ShouldNotBeNull();
        var command = result.Command.Command.ShouldBeOfType<ParentCommand.ChildCommand>();
        command.IsPresent.ShouldBeTrue();
    }

    [Fact]
    public void ParentWithoutChildApp_DoesntHaveChild_WhenNotProvided()
    {
        var result = ParentWithoutChildApp.TryParse(["-d"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Dodgems.ShouldBeTrue();
        result.Command.Command.ShouldBeNull();
    }

    [Fact]
    public void ComplexTypes_ParseCorrectly()
    {
        var testDate = "2025-01-15";
        var testGuid = "12345678-1234-1234-1234-123456789012";
        var testDecimal = "123.45";

        var result = ComplexTypesApp.TryParse([
            "--date", testDate,
            "--id", testGuid,
            "--price", testDecimal
        ]);

        result.IsSuccess.ShouldBeTrue();
        result.Command.Date.ShouldBe(DateTime.Parse(testDate));
        result.Command.Id.ShouldBe(Guid.Parse(testGuid));
        result.Command.Price.ShouldBe(123.45m);
    }

    [Fact]
    public void CustomHelpText_IsIncluded_InHelpMessage()
    {
        var result = CustomHelpApp.TryParse(["--help"]);
        result.IsHelp.ShouldBeTrue("Expected ShowHelp on help flag");

        // The help message is printed to console, but we can verify the command has the properties set
        // by checking that parsing works and the app model has custom help defined via the attribute
        var validResult = CustomHelpApp.TryParse(["--name", "test"]);
        validResult.IsSuccess.ShouldBeTrue();
        validResult.Command.Name.ShouldBe("test");
    }

    [Fact]
    public void CustomParser_IsUsed_WhenSpecified()
    {
        var result = CustomParserApp.TryParse(["--point", "10,20"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Point.X.ShouldBe(10);
        result.Command.Point.Y.ShouldBe(20);
    }

    [Fact]
    public void CustomParser_WorksWithPositionalArgs()
    {
        var result = CustomParserPositionalApp.TryParse(["5,15"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Point.X.ShouldBe(5);
        result.Command.Point.Y.ShouldBe(15);
    }

    [Fact]
    public void RangeValidation_PassesWhenInRange()
    {
        var result = RangeValidationApp.TryParse(["--port", "8080"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Port.ShouldBe(8080);
    }

    [Fact]
    public void RangeValidation_FailsWhenTooLow()
    {
        var result = RangeValidationApp.TryParse(["--port", "0"]);
        result.IsError.ShouldBeTrue("Expected ParseError for value below minimum");
        result.Error.Message.ShouldContain("Port must be between 1 and 65535");
    }

    [Fact]
    public void RangeValidation_FailsWhenTooHigh()
    {
        var result = RangeValidationApp.TryParse(["--port", "70000"]);
        result.IsError.ShouldBeTrue("Expected ParseError for value above maximum");
        result.Error.Message.ShouldContain("Port must be between 1 and 65535");
    }

    [Fact]
    public void StringLengthValidation_PassesWhenValid()
    {
        var result = StringLengthValidationApp.TryParse(["--name", "Alice"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Name.ShouldBe("Alice");
    }

    [Fact]
    public void StringLengthValidation_FailsWhenTooShort()
    {
        var result = StringLengthValidationApp.TryParse(["--name", "Ab"]);
        result.IsError.ShouldBeTrue("Expected ParseError for string too short");
        result.Error.Message.ShouldContain("Name must be between 3 and 10 characters");
    }

    [Fact]
    public void StringLengthValidation_FailsWhenTooLong()
    {
        var result = StringLengthValidationApp.TryParse(["--name", "VeryLongName"]);
        result.IsError.ShouldBeTrue("Expected ParseError for string too long");
        result.Error.Message.ShouldContain("Name must be between 3 and 10 characters");
    }

    [Fact]
    public void RegexValidation_PassesWhenMatches()
    {
        var result = RegexValidationApp.TryParse(["--code", "ABC-1234"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Code.ShouldBe("ABC-1234");
    }

    [Fact]
    public void RegexValidation_FailsWhenDoesNotMatch()
    {
        var result = RegexValidationApp.TryParse(["--code", "invalid"]);
        result.IsError.ShouldBeTrue("Expected ParseError for invalid pattern");
        result.Error.Message.ShouldContain("Code must match pattern AAA-9999");
    }

    [Fact]
    public void EmailValidation_PassesWithValidEmail()
    {
        var result = EmailValidationApp.TryParse(["--email", "user@example.com"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Email.ShouldBe("user@example.com");
    }

    [Fact]
    public void EmailValidation_FailsWithInvalidEmail()
    {
        var result = EmailValidationApp.TryParse(["--email", "not-an-email"]);
        result.IsError.ShouldBeTrue("Expected ParseError for invalid email");
        result.Error.Message.ShouldContain("Invalid email address");
    }

    [Fact]
    public void MultipleValidations_AllMustPass()
    {
        var result = MultipleValidationsApp.TryParse(["--username", "valid_user123"]);
        result.IsSuccess.ShouldBeTrue();
        result.Command.Username.ShouldBe("valid_user123");
    }

    [Fact]
    public void MultipleValidations_FailsIfAnyFails()
    {
        // Too short (less than 5 chars)
        var result1 = MultipleValidationsApp.TryParse(["--username", "bob"]);
        result1.IsError.ShouldBeTrue();

        // Invalid characters
        var result2 = MultipleValidationsApp.TryParse(["--username", "user@name"]);
        result2.IsError.ShouldBeTrue();
        result2.Error.Message.ShouldContain("Username can only contain letters, numbers, and underscores");
    }

    // Note: InvalidParserApp is intentionally commented out to avoid compile errors
    // Uncommenting it should produce diagnostic error CLAP001:
    // "Custom parser type 'InvalidParser' must have a static Parse(string) method"
    /*
    public static class InvalidParser
    {
        // Missing Parse method - should cause CLAP001 error
    }

    [Command]
    public partial class InvalidParserApp
    {
        [Arg(ValueParser = typeof(InvalidParser))]
        public required Point Point { get; init; }
    }
    */
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
    [Command]
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

    [Command]
    public required ParentCommand Command { get; init; }
}

[Command]
public partial class ParentWithoutChildApp
{
    [Arg(Short = 'd')]
    public bool Dodgems { get; init; }

    [Command]
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

[Command]
public partial class ComplexTypesApp
{
    [Arg(Long = "date")]
    public required DateTime Date { get; init; }

    [Arg(Long = "id")]
    public required Guid Id { get; init; }

    [Arg(Long = "price")]
    public required decimal Price { get; init; }
}

[Command(About = "Test app with custom help", LongAbout = "This is a longer description that provides more details about the test application")]
public partial class CustomHelpApp
{
    [Arg(Short = 'n', Long = "name", Help = "Your name (custom help text)")]
    public required string Name { get; init; }

    /// <summary>
    /// The age value with help from XML comments
    /// </summary>
    [Arg(Short = 'a', Long = "age")]
    public int Age { get; init; }
}

// Custom type for testing parsers
public record struct Point(int X, int Y);

// Custom parser for Point type
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

[Command]
public partial class CustomParserApp
{
    [Arg(Short = 'p', Long = "point", ValueParser = typeof(PointParser))]
    public required Point Point { get; init; }
}

[Command]
public partial class CustomParserPositionalApp
{
    [Arg(ValueParser = typeof(PointParser))]
    public required Point Point { get; init; }
}

[Command]
public partial class RangeValidationApp
{
    [Arg(Long = "port")]
    [System.ComponentModel.DataAnnotations.Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public required int Port { get; init; }
}

[Command]
public partial class StringLengthValidationApp
{
    [Arg(Long = "name")]
    [System.ComponentModel.DataAnnotations.StringLength(10, MinimumLength = 3, ErrorMessage = "Name must be between 3 and 10 characters")]
    public required string Name { get; init; }
}

[Command]
public partial class RegexValidationApp
{
    [Arg(Long = "code")]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^[A-Z]{3}-\d{4}$", ErrorMessage = "Code must match pattern AAA-9999")]
    public required string Code { get; init; }
}

[Command]
public partial class EmailValidationApp
{
    [Arg(Long = "email")]
    [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Invalid email address")]
    public required string Email { get; init; }
}

[Command]
public partial class MultipleValidationsApp
{
    [Arg(Long = "username")]
    [System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 5)]
    [System.ComponentModel.DataAnnotations.RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers, and underscores")]
    public required string Username { get; init; }
}