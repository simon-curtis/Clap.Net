using System;

// ReSharper disable once CheckNamespace
namespace Clap.Net;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Property | AttributeTargets.Field, Inherited = false)]
public sealed class CommandAttribute() : Attribute
{
    /// <summary>
    /// package name (if on Parser container), variant name (if on Subcommand variant)
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Defaults to `Version` field from MSBuild
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Defaults to `Author` field from MSBuild
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Defaults to the `Description` field from MSBuild
    /// </summary>
    /// <remarks>
    /// When a doc comment is also present, you most likely want to add #[Command(LongAbout = null)] to clear the doc comment 
    /// so only about gets shown with both -h and --help.
    /// </remarks>
    public string? About { get; init; }

    /// <summary>
    /// Default to `Comment` field from MSBuild
    /// </summary>
    public string? LongAbout { get; init; }

    /// <summary>
    /// Override default field / variant name case conversion for Command.Name / Arg.Id<br/><br/>
    /// Available options:
    /// <list>
    ///  <item>- camelCase</item>
    ///  <item>- <b>kebab-case</b> (default)</item>
    ///  <item>- PascalCase</item>
    ///  <item>- SCREAMING_SNAKE_CASE</item>
    ///  <item>- snake_case</item>
    ///  <item>- lower</item>
    ///  <item>- UPPER</item>
    ///  <item>- verbatim</item>
    /// </list>
    /// </summary>
    public string? RenameAll { get; init; }

    /// <summary>
    /// Override default field name case conversion for env variables for Arg.Env<br/><br/>
    /// Available options:
    /// <list>
    ///  <item>- camelCase</item>
    ///  <item>- kebab-case</item>
    ///  <item>- PascalCase</item>
    ///  <item>- <b>SCREAMING_SNAKE_CASE</b> (default)</item>
    ///  <item>- snake_case</item>
    ///  <item>- lower</item>
    ///  <item>- UPPER</item>
    ///  <item>- verbatim</item>
    /// </list>
    /// </summary>
    public string? RenameAllEnv { get; init; }

    /// <summary>
    /// Delegates to the variant for more subcommands (must have Subcommand attribute)
    /// </summary>
    public bool Flatten { get; init; }

    /// <summary>
    /// Nest subcommands under the current set of subcommands (must implement Subcommand)
    /// </summary>
    public bool Subcommand { get; init; }
}

[Flags]
public enum Traits
{
    Subcommand
}
