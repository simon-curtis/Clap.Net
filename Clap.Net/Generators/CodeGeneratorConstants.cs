using Microsoft.CodeAnalysis;

namespace Clap.Net.Generators;

internal static class CodeGeneratorConstants
{
    public static readonly SymbolDisplayFormat FullNameDisplayFormat =
        new(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters);
}
