using Shouldly;

namespace Clap.Net.Tests;

public class LexerTests
{
    [Fact]
    public void ShortFlag_IsReturned_WhenGivenAShortFlag()
    {
        var tokens = Lexer.Lex(["-a"]);
        tokens.Length.ShouldBe(1);
        tokens[0].ShouldBeOfType<ShortFlag>();
    }

    [Fact]
    public void ShortFlags_AreReturned_WhenGivenACoupleOfShortFlags()
    {
        var tokens = Lexer.Lex(["-a", "-b"]);
        tokens.Length.ShouldBe(2);
        tokens[0].ShouldBeOfType<ShortFlag>();
        tokens[1].ShouldBeOfType<ShortFlag>();
    }

    [Fact]
    public void LongFlag_IsReturned_WhenGivenALongFlag()
    {
        var tokens = Lexer.Lex(["--apple"]);
        tokens.Length.ShouldBe(1);
        tokens[0].ShouldBeOfType<LongFlag>();
    }

    [Fact]
    public void CompoundFlag_IsReturned_WhenGivenACompoundFlag()
    {
        Span<IToken> tokens = Lexer.Lex(["-abc"]);
        tokens.Length.ShouldBe(1);
        var compound = tokens[0].ShouldBeOfType<CompoundFlag>();
        compound.Chars.Length.ShouldBe(3);
        compound.Chars[0].ShouldBe('a');
        compound.Chars[1].ShouldBe('b');
        compound.Chars[2].ShouldBe('c');
    }

    [Fact]
    public void ValueLiteral_IsReturned_WhenGivenAStringLiteral()
    {
        var tokens = Lexer.Lex(["apple"]);
        tokens.Length.ShouldBe(1);
        var compound = tokens[0].ShouldBeOfType<ValueLiteral>();
        compound.Value.ShouldBe("apple");
    }

    [Fact]
    public void ValueLiteral_IsReturned_WhenGivenAIntLiteral()
    {
        var tokens = Lexer.Lex(["123"]);
        tokens.Length.ShouldBe(1);
        var compound = tokens[0].ShouldBeOfType<ValueLiteral>();
        compound.Value.ShouldBe("123");
    }
}