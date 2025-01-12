using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Faithlife.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterpolatedStringAnalyzer : DiagnosticAnalyzer
{
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

		context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
		{
			compilationStartAnalysisContext.RegisterOperationBlockStartAction(context =>
			{
				context.RegisterOperationAction(AnalyzeOperation, OperationKind.InterpolatedString);
			});
		});
	}

	public const string DiagnosticIdDollar = "FL0007";
	public const string DiagnosticIdUnnecessary = "FL0014";

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_ruleDollar, s_ruleUnnecessary);

	private static void AnalyzeOperation(OperationAnalysisContext context)
	{
		var invocationOperation = (IInterpolatedStringOperation)context.Operation;
		var foundDollarSign = false;

		if (!invocationOperation.Children.Any(child => child is IInterpolationOperation))
			context.ReportDiagnostic(Diagnostic.Create(s_ruleUnnecessary, invocationOperation.Syntax.GetLocation()));

		foreach (var child in invocationOperation.Children)
		{
			if ((child as IInterpolatedStringTextOperation)?.Text.Syntax.ToFullString().EndsWith("$", StringComparison.Ordinal) ?? false)
			{
				foundDollarSign = true;
			}
			else
			{
				if (child is IInterpolatedStringContentOperation && foundDollarSign)
					context.ReportDiagnostic(Diagnostic.Create(s_ruleDollar, child.Syntax.GetLocation()));
				foundDollarSign = false;
			}
		}
	}

	private static readonly DiagnosticDescriptor s_ruleDollar = new(
		id: DiagnosticIdDollar,
		title: "Unintentional ${} in interpolated strings",
		messageFormat: "Avoid using ${} in interpolated strings.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticIdDollar}");

	private static readonly DiagnosticDescriptor s_ruleUnnecessary = new(
		id: DiagnosticIdUnnecessary,
		title: "Unnecessary interpolated string",
		messageFormat: "Avoid using an interpolated string where an equivalent literal string exists.",
		category: "Usage",
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		helpLinkUri: $"https://github.com/Faithlife/FaithlifeAnalyzers/wiki/{DiagnosticIdUnnecessary}");
}
