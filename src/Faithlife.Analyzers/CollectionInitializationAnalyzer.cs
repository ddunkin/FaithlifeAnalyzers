using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollectionInitializationAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.EnableConcurrentExecution();
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			compilationStartAnalysisContext.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.ObjectCreationExpression);
		});
	}

	public const string DiagnosticId = "FL0021";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

	private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
	{
		var creationExpression = (ObjectCreationExpressionSyntax) context.Node;

		// Check if this is a collection type creation
		if (context.SemanticModel.GetSymbolInfo(creationExpression).Symbol is not IMethodSymbol constructor)
			return;

		var containingType = constructor.ContainingType;
		if (containingType == null)
			return;

		// Check if this implements ICollection, IList, or is a known collection type
		if (!IsCollectionType(containingType, context.SemanticModel.Compilation))
			return;

		// Always suggest collection expression for empty list creation (no arguments)
		if (creationExpression.ArgumentList?.Arguments.Count == 0)
		{
			context.ReportDiagnostic(Diagnostic.Create(s_rule, creationExpression.GetLocation()));
			return;
		}

		// Check if we have an initializer or are calling ToArray/ToList on a simple expression
		if (creationExpression.Initializer != null)
		{
			// Already has an initializer, suggest collection expression if simple enough
			if (IsSimpleInitializer(creationExpression.Initializer))
			{
				context.ReportDiagnostic(Diagnostic.Create(s_rule, creationExpression.GetLocation()));
			}
		}
		else if (creationExpression.ArgumentList?.Arguments.Count == 1)
		{
			// Check if the argument is not a LINQ chain that would require ToList()
			var argument = creationExpression.ArgumentList.Arguments[0];
			if (!IsLinqChain(argument.Expression))
			{
				context.ReportDiagnostic(Diagnostic.Create(s_rule, creationExpression.GetLocation()));
			}
		}
	}

	private static bool IsCollectionType(INamedTypeSymbol type, Compilation compilation)
	{
		// Check for common collection types
		var listType = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");

		if (listType != null && SymbolEqualityComparer.Default.Equals(type.ConstructedFrom, listType))
			return true;

		return false;
	}

	private static bool IsSimpleInitializer(InitializerExpressionSyntax initializer)
	{
		// Only suggest for simple initializers with literal values or simple expressions
		return initializer.Expressions.Count <= 10 &&
			   initializer.Expressions.All(expr => IsSimpleExpression(expr));
	}

	private static bool IsSimpleExpression(ExpressionSyntax expression)
	{
		return expression.IsKind(SyntaxKind.NumericLiteralExpression) ||
			   expression.IsKind(SyntaxKind.StringLiteralExpression) ||
			   expression.IsKind(SyntaxKind.TrueLiteralExpression) ||
			   expression.IsKind(SyntaxKind.FalseLiteralExpression) ||
			   expression.IsKind(SyntaxKind.IdentifierName) ||
			   expression.IsKind(SyntaxKind.SimpleMemberAccessExpression);
	}

	private static bool IsLinqChain(ExpressionSyntax expression)
	{
		// Check if this looks like a LINQ chain (method calls that might include Where, Select, etc.)
		if (expression is InvocationExpressionSyntax invocation &&
			invocation.Expression is MemberAccessExpressionSyntax memberAccess)
		{
			var methodName = memberAccess.Name.Identifier.ValueText;
			return methodName is "Where" or "Select" or "SelectMany" or "OrderBy" or "OrderByDescending" or
				   "GroupBy" or "Join" or "Skip" or "Take" or "Distinct" or "Union" or "Intersect" or
				   "Except" or "Zip" or "DefaultIfEmpty";
		}

		return false;
	}

	private static readonly DiagnosticDescriptor s_rule = new(
		id: DiagnosticId,
		title: "Use collection expression",
		messageFormat: "Use collection expression instead of explicit collection creation",
		category: "Style",
		defaultSeverity: DiagnosticSeverity.Info,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticId}");
}
