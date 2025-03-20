using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Clap.Net;

file record CommandCandidateModel(
    string Kind,
    INamedTypeSymbol CommandSymbol,
    List<(int, ISymbol)> PositionalMembers,
    List<(ISymbol, AttributeData)> OptionMembers,
    List<(ISymbol, AttributeData)> SwitchMembers
);

[Generator]
public class ClapGenerator : IIncrementalGenerator
{
    private static readonly SymbolDisplayFormat TypeNameFormat =
        new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
        );

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var commandCandidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var decl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(decl);

                    if (symbol is null)
                        return null;

                    var hasCommandAttribute = symbol
                        .GetAttributes()
                        .Any(attr => attr.AttributeClass?.Name is "CommandAttribute");

                    if (!hasCommandAttribute)
                        return null;

                    var positionalMembers = new List<(int, ISymbol)>();
                    var optionMembers = new List<(ISymbol, AttributeData)>();
                    var switchMembers = new List<(ISymbol, AttributeData)>();

                    var namedTypeSymbol = (INamedTypeSymbol)symbol;

                    foreach (var member in namedTypeSymbol.GetMembers())
                    {
                        if (member is IPropertySymbol or IFieldSymbol)
                        {
                            foreach (var attr in member.GetAttributes())
                            {
                                var attrName = attr.AttributeClass?.Name;

                                switch (attrName)
                                {
                                    case "OptionAttribute":
                                        optionMembers.Add((member, attr));
                                        break;

                                    case "SwitchAttribute":
                                        switchMembers.Add((member, attr));
                                        break;

                                    case "PositionalAttribute":
                                        var constant = attr.NamedArguments.First(a => a.Key == "Index").Value;
                                        var index = int.Parse(constant.Value?.ToString()!);
                                        positionalMembers.Add((index, member));
                                        break;
                                }
                            }
                        }
                    }

                    return new CommandCandidateModel(
                        decl.Keyword.Text, // class / struct / record
                        namedTypeSymbol,
                        positionalMembers,
                        optionMembers,
                        switchMembers
                    );
                }
            )
            .Where(symbol => symbol != null);


        // Step 3: Emit Parse method
        context.RegisterSourceOutput(commandCandidates, (spc, symbolModel) =>
        {
            var (kind, symbol, positionals, options, switches) = symbolModel!;

            var ns = symbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : symbol.ContainingNamespace.ToDisplayString();

            var typeName = symbol.Name;
            using var ms = new MemoryStream();
            var writer = new Utf8IndentedWriter(ms);

            if (ns is not null)
                writer.WriteLine($"namespace {ns};\n");
            
            writer.WriteLine(
                $$"""
                  #nullable enable

                  public partial {{kind}} {{typeName}}
                  {
                      public static {{typeName}} Parse(string[] args)
                      {
                  """);

            writer.IncreaseIndent(by: 2);
            foreach (var (_, member) in positionals)
            {
                writer.WriteLine($"{GetMemberTypeName(member)} {member.Name} = default!;");
            }

            foreach (var (member, _) in options)
            {
                writer.WriteLine($"{GetMemberTypeName(member)} {member.Name} = default!;");
            }

            foreach (var (member, _) in switches)
            {
                writer.WriteLine($"{GetMemberTypeName(member)} {member.Name} = default!;");
            }

            writer.WriteLine();
            writer.WriteLine("// Doing the fun bit now :)");

            writer.WriteLine("""
                             var index = 0;
                             var positionalIndex = 0;
                             while (index < args.Length)
                             {
                                 switch (args[index])
                                 {
                             """);

            writer.IncreaseIndent(by: 2);

            foreach (var (member, attr) in options)
            {
                var longName = attr.NamedArguments.FirstOrDefault(a => a.Key is "LongName").Value.Value;
                var shortName = attr.NamedArguments.FirstOrDefault(a => a.Key is "ShortName").Value.Value;

                if (longName is null && shortName is null)
                    continue;

                writer.WriteLine($"// Setting member '{typeName}.{member.Name}'");

                if (shortName is not null)
                    writer.WriteLine($"case \"-{shortName}\":");

                if (longName is not null)
                    writer.WriteLine($"case \"--{longName}\":");

                writer.IncreaseIndent();
                writer.WriteLine("index++;");
                WriteArgValueConversion(member, writer);
                writer.WriteLine("break;");
                writer.DecreaseIndent();
                writer.WriteLine();
            }

            foreach (var (member, attr) in switches)
            {
                var longName = attr.NamedArguments.FirstOrDefault(a => a.Key is "LongName").Value.Value;
                var shortName = attr.NamedArguments.FirstOrDefault(a => a.Key is "ShortName").Value.Value;

                if (longName is null && shortName is null)
                    continue;

                writer.WriteLine($"// Setting member '{typeName}.{member.Name}'");

                if (shortName is not null)
                    writer.WriteLine($"case \"-{shortName}\":");

                if (longName is not null)
                    writer.WriteLine($"case \"--{longName}\":");

                writer.IncreaseIndent();
                writer.WriteLine($"{member.Name} = true;");
                writer.WriteLine("break;");
                writer.DecreaseIndent();
                writer.WriteLine();
            }

            writer.WriteLine("""
                             case var arg when arg.StartsWith('-'):
                                 Console.WriteLine($"Unknown parameter {arg}");
                                 break;
                             """);
            writer.WriteLine();

            foreach (var (i, member) in positionals)
            {
                writer.WriteLine($"""
                                  // Setting member '{typeName}.{member.Name}'
                                  case var arg when positionalIndex is {i}:
                                  """);

                writer.IncreaseIndent();
                WriteArgValueConversion(member, writer);
                writer.WriteLine("positionalIndex++;");
                writer.WriteLine("break;");
                writer.DecreaseIndent();
            }

            writer.DecreaseIndent(by: 2);

            writer.WriteLine("""
                                 }
                             
                                 index++;
                             }
                             """);

            writer.WriteLine();

            writer.WriteLine($"return new {typeName}");
            writer.WriteLine('{');
            writer.IncreaseIndent();

            foreach (var (i, member) in positionals)
            {
                var condition = $"positionalIndex > {i}";
                if (member is IPropertySymbol { IsRequired: true })
                    condition = $"{condition} && {member.Name} != default";

                writer.WriteLine(
                    $"""
                     {member.Name} = {condition}
                         ? {member.Name}
                         : throw new Exception("Argument {member.Name} in position {i} is required"),
                     """);
            }

            foreach (var (member, _) in options)
            {
                writer.WriteLine($"{member.Name} = {member.Name},");
            }

            foreach (var (member, _) in switches)
            {
                writer.WriteLine($"{member.Name} = {member.Name},");
            }

            // Close member creation
            writer.DecreaseIndent();
            writer.WriteLine("};");

            // Close method 
            writer.DecreaseIndent();
            writer.WriteLine("}");

            // Close class
            writer.DecreaseIndent();
            writer.WriteLine("}");

            var output = Encoding.UTF8.GetString(ms.ToArray());
            // File.WriteAllText("/home/simon/git/Clap.Net/Clap.Net/TEST.cs", output);
            spc.AddSource($"{typeName}.ParseMethod.g.cs", SourceText.From(output, Encoding.UTF8));
        });
    }

    private static void WriteArgValueConversion(ISymbol member, Utf8IndentedWriter writer)
    {
        writer.WriteLine($"{member.Name} = {GetArgConversion(member)};");
    }

    private static string? GetArgConversion(ISymbol member)
    {
        return GetMemberTypeName(member) switch
        {
            "System.String" or "System.String?" => "args[index]",
            "System.Int32" or "System.Int32?" => "int.Parse(args[index])",
            var type => $"throw new Exception(\"Type '{type}' is not supported\")"
        };
    }

    private static string GetMemberTypeName(ISymbol member)
    {
        return member switch
        {
            IPropertySymbol prop => prop.Type.ToDisplayString(TypeNameFormat),
            IFieldSymbol field => field.Type.ToDisplayString(TypeNameFormat),
            _ => throw new Exception($"Dont know how to get type for  {member.ToDisplayString()}")
        };
    }
}