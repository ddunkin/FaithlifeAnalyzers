using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Faithlife.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CollectionInitializationCodeFixProvider)), Shared]
public sealed class CollectionInitializationCodeFixProvider : CodeFixProvider
{
	public override ImmutableArray<string> FixableDiagnosticIds => [CollectionInitializationAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

		var diagnostic = context.Diagnostics.First();
		var diagnosticSpan = diagnostic.Location.SourceSpan;

		var objectCreation = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
		if (objectCreation is null)
			return;

		// Check if C# version supports collection expressions (C# 12+)
		var compilation = semanticModel.Compilation;
		var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
		if (parseOptions.LanguageVersion < LanguageVersion.CSharp12)
			return;

		context.RegisterCodeFix(
			CodeAction.Create(
				title: "Use collection expression",
				createChangedDocument: token => ConvertToCollectionExpressionAsync(context.Document, objectCreation, token),
				"use-collection-expression"),
			diagnostic);
	}

	private static async Task<Document> ConvertToCollectionExpressionAsync(Document document, ObjectCreationExpressionSyntax objectCreation, CancellationToken cancellationToken)
	{
		var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

		CollectionExpressionSyntax collectionExpression;

		if (objectCreation.Initializer != null)
		{
			// Convert initializer to collection expression
			var elements = objectCreation.Initializer.Expressions
				.Select(expr => (CollectionElementSyntax)ExpressionElement(expr))
				.ToArray();

			collectionExpression = CollectionExpression(
				SeparatedList<CollectionElementSyntax>(elements));
		}
		else if (objectCreation.ArgumentList?.Arguments.Count == 1)
		{
			// Single argument - convert to spread element if it's an enumerable
			var argument = objectCreation.ArgumentList.Arguments[0];
			collectionExpression = CollectionExpression(
				SingletonSeparatedList<CollectionElementSyntax>(
					SpreadElement(argument.Expression)));
		}
		else
		{
			// Empty collection
			collectionExpression = CollectionExpression();
		}

		var newRoot = root.ReplaceNode(objectCreation, collectionExpression.NormalizeWhitespace());
		return await Simplifier.ReduceAsync(document.WithSyntaxRoot(newRoot), cancellationToken: cancellationToken).ConfigureAwait(false);
	}
}
