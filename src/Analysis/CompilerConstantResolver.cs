using System.Runtime.InteropServices;
using Caique.Lexing;

namespace Caique.Analysis;

public class CompilerConstantResolver
{
    private readonly CompilationContext _compilationContext;
    private readonly StructureDataType _stringType;
    private readonly Dictionary<string, SemanticLiteralNode> _cache = [];

    public CompilerConstantResolver(CompilationContext compilationContext)
    {
        _compilationContext = compilationContext;

        var stringSymbol = compilationContext.StdScope.ResolveStructure(["std", "prelude", "String"])!.SyntaxDeclaration.Symbol!;
        _stringType = new StructureDataType(stringSymbol, []);
    }

    public SemanticLiteralNode? Resolve(string name, TextSpan span)
    {
        if (_cache.TryGetValue(name, out var existing))
            return existing;

        SemanticLiteralNode literal;
        if (name == "operating_system")
        {
            var operatingSystem = "unknown";
            if (OperatingSystem.IsLinux())
                operatingSystem = "linux";

            if (OperatingSystem.IsMacOS())
                operatingSystem = "macos";

            if (OperatingSystem.IsWindows())
                operatingSystem = "windows";

            if (OperatingSystem.IsFreeBSD())
                operatingSystem = "freebsd";

            var token = new Token(TokenKind.StringLiteral, operatingSystem, span);
            literal = new SemanticLiteralNode(token, _stringType);
        }
        else if (name == "family")
        {
            var family = "unknown";
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
                family = "unix";

            if (OperatingSystem.IsWindows())
                family = "windows";

            var token = new Token(TokenKind.StringLiteral, family, span);
            literal = new SemanticLiteralNode(token, _stringType);
        }
        else if (name == "arch")
        {
            var arch = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
            var token = new Token(TokenKind.StringLiteral, arch, span);
            literal = new SemanticLiteralNode(token, _stringType);
        }
        else
        {
            throw new NotImplementedException();
        }

        _cache[name] = literal;

        return literal;
    }
}
