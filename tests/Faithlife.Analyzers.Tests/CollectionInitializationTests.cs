using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace Faithlife.Analyzers.Tests;

[TestFixture]
internal sealed class CollectionInitializationTests : CodeFixVerifier
{
	[Test]
	public void EmptyListCreation()
	{
		const string program = @"
using System.Collections.Generic;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var list = new List<int>();
		}
	}
}";

		var expected = new DiagnosticResult
		{
			Id = CollectionInitializationAnalyzer.DiagnosticId,
			Message = "Use collection expression instead of explicit collection creation",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", 10, 15)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix = @"
using System.Collections.Generic;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var list = [];
		}
	}
}";

		VerifyCSharpFix(program, fix, 0);
	}

	[Test]
	public void ListWithInitializer()
	{
		const string program = @"
using System.Collections.Generic;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var list = new List<int> { 1, 2, 3 };
		}
	}
}";

		var expected = new DiagnosticResult
		{
			Id = CollectionInitializationAnalyzer.DiagnosticId,
			Message = "Use collection expression instead of explicit collection creation",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", 10, 15)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix = @"
using System.Collections.Generic;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var list = [1, 2, 3];
		}
	}
}";

		VerifyCSharpFix(program, fix, 0);
	}

	[Test]
	public void ListFromArray()
	{
		const string program = @"
using System.Collections.Generic;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var array = new int[] { 1, 2, 3 };
			var list = new List<int>(array);
		}
	}
}";

		var expected = new DiagnosticResult
		{
			Id = CollectionInitializationAnalyzer.DiagnosticId,
			Message = "Use collection expression instead of explicit collection creation",
			Severity = DiagnosticSeverity.Info,
			Locations = [new DiagnosticResultLocation("Test0.cs", 11, 15)],
		};

		VerifyCSharpDiagnostic(program, expected);

		const string fix = @"
using System.Collections.Generic;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var array = new int[] { 1, 2, 3 };
			var list = [..array];
		}
	}
}";

		VerifyCSharpFix(program, fix, 0);
	}

	[Test]
	public void LinqChainShouldNotTrigger()
	{
		const string program = @"
using System.Collections.Generic;
using System.Linq;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var source = new[] { 1, 2, 3, 4, 5 };
			var list = new List<int>(source.Where(x => x > 2).Select(x => x * 2));
		}
	}
}";

		// Should not trigger diagnostic for LINQ chains
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void SimpleWhereClauseShouldNotTrigger()
	{
		const string program = @"
using System.Collections.Generic;
using System.Linq;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var source = new[] { 1, 2, 3, 4, 5 };
			var list = new List<int>(source.Where(x => x > 2));
		}
	}
}";

		// Should not trigger diagnostic for LINQ chains, even simple ones
		VerifyCSharpDiagnostic(program);
	}

	[Test]
	public void ComplexInitializerShouldNotTrigger()
	{
		const string program = @"
using System.Collections.Generic;

namespace TestApplication
{
	public class TestClass
	{
		public void TestMethod()
		{
			var list = new List<int> 
			{ 
				1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 
				11, 12, 13, 14, 15 
			};
		}
	}
}";

		// Should not trigger for complex initializers (> 10 elements)
		VerifyCSharpDiagnostic(program);
	}

	protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() => new CollectionInitializationAnalyzer();

	protected override CodeFixProvider GetCSharpCodeFixProvider() => new CollectionInitializationCodeFixProvider();
}
