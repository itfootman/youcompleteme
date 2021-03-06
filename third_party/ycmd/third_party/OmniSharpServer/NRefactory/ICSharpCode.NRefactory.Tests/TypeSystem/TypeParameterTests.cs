﻿// Copyright (c) 2010-2013 AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem.Implementation;
using NUnit.Framework;

namespace ICSharpCode.NRefactory.TypeSystem
{
	[TestFixture]
	public class TypeParameterTests
	{
		[Test]
		public void TypeParameterDerivingFromOtherTypeParameterDoesNotInheritReferenceConstraint()
		{
			// class C<T, U> where T : class where U : T
			var c = new DefaultUnresolvedTypeDefinition(string.Empty, "C");
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 0, "T") { HasReferenceTypeConstraint = true });
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 1, "U") {
			                     	Constraints = { new TypeParameterReference(SymbolKind.TypeDefinition, 0) }
			                     });
			
			ITypeDefinition resolvedC = TypeSystemHelper.CreateCompilationAndResolve(c);
			// At runtime, we might have T=System.ValueType and U=int, so C# can't inherit the 'class' constraint
			// from one type parameter to another.
			Assert.AreEqual(true, resolvedC.TypeParameters[0].IsReferenceType);
			Assert.IsNull(resolvedC.TypeParameters[1].IsReferenceType);
		}
		
		[Test]
		public void ValueTypeParameterDerivingFromReferenceTypeParameter()
		{
			// class C<T, U> where T : class where U : struct, T
			var c = new DefaultUnresolvedTypeDefinition(string.Empty, "C");
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 0, "T") { HasReferenceTypeConstraint = true });
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 1, "U") {
			                     	HasValueTypeConstraint = true,
			                     	Constraints = { new TypeParameterReference(SymbolKind.TypeDefinition, 0) }
			                     });
			
			ITypeDefinition resolvedC = TypeSystemHelper.CreateCompilationAndResolve(c);
			// At runtime, we might have T=System.ValueType and U=int, so C# can't inherit the 'class' constraint
			// from one type parameter to another.
			Assert.AreEqual(true, resolvedC.TypeParameters[0].IsReferenceType);
			Assert.AreEqual(false, resolvedC.TypeParameters[1].IsReferenceType);
		}
		
		[Test]
		public void TypeParameterDerivingFromOtherTypeParameterInheritsEffectiveBaseClass()
		{
			// class C<T, U> where T : List<string> where U : T
			var c = new DefaultUnresolvedTypeDefinition(string.Empty, "C");
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 0, "T") {
			                     	Constraints = { typeof(List<string>).ToTypeReference() }
			                     });
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 1, "U") {
			                     	Constraints = { new TypeParameterReference(SymbolKind.TypeDefinition, 0) }
			                     });
			
			ITypeDefinition resolvedC = TypeSystemHelper.CreateCompilationAndResolve(c);
			Assert.AreEqual(true, resolvedC.TypeParameters[0].IsReferenceType);
			Assert.AreEqual(true, resolvedC.TypeParameters[1].IsReferenceType);
			Assert.AreEqual("System.Collections.Generic.List`1[[System.String]]", resolvedC.TypeParameters[0].EffectiveBaseClass.ReflectionName);
			Assert.AreEqual("System.Collections.Generic.List`1[[System.String]]", resolvedC.TypeParameters[1].EffectiveBaseClass.ReflectionName);
		}
		
		[Test]
		public void ImportOpenGenericType()
		{
			// class C<T, U> { void M<X>() {} }
			
			var c = new DefaultUnresolvedTypeDefinition(string.Empty, "C");
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 0, "T"));
			c.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.TypeDefinition, 1, "U"));
			var m = new DefaultUnresolvedMethod(c, "M");
			m.TypeParameters.Add(new DefaultUnresolvedTypeParameter(SymbolKind.Method, 0, "X"));
			c.Members.Add(m);
			
			var resolvedC1 = TypeSystemHelper.CreateCompilationAndResolve(c);
			var resolvedM1 = resolvedC1.Methods.Single(method => method.Name == "M");
			
			var resolvedC2 = TypeSystemHelper.CreateCompilationAndResolve(c);
			var resolvedM2 = resolvedC2.Methods.Single(method => method.Name == "M");
			
			// the types, methods and type parameters differ in the two compilations:
			Assert.AreNotEqual(resolvedC1, resolvedC2);
			Assert.AreNotEqual(resolvedM1, resolvedM2);
			Assert.AreNotEqual(resolvedC1.TypeParameters[1], resolvedC2.TypeParameters[1]);
			Assert.AreNotEqual(resolvedM1.TypeParameters[0], resolvedM2.TypeParameters[0]);
			
			// C<U, X>
			var pt1 = new ParameterizedType(resolvedC1, new[] { resolvedC1.TypeParameters[1], resolvedM1.TypeParameters[0] });
			var pt2 = (ParameterizedType)resolvedC2.Compilation.Import(pt1);
			
			// importing resulted in C<U, X> in the new compilation:
			Assert.AreEqual(resolvedC2, pt2.GetDefinition());
			Assert.AreEqual(resolvedC2.TypeParameters[1], pt2.TypeArguments[0]);
			Assert.AreEqual(resolvedM2.TypeParameters[0], pt2.TypeArguments[1]);
		}
	}
}
