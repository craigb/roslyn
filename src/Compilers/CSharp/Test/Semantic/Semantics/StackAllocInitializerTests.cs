﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Test.Utilities;
using Microsoft.CodeAnalysis.Test.Utilities;
using Xunit;

namespace Microsoft.CodeAnalysis.CSharp.UnitTests
{
    [CompilerTrait(CompilerFeature.StackAllocInitializer)]
    public class StackAllocInitializerTests : CompilingTestBase
    {
        [Fact]
        public void NoBestType()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    struct A {}
    struct B {}

    public void Method()
    {
        var p1 = stackalloc[] { new A(), new B() };
        var p2 = stackalloc[] { };
        var p3 = stackalloc[] { Method() };
        var p4 = stackalloc[] { null };
        var p5 = stackalloc[] { (1, null) };
        var p6 = stackalloc[] { () => { } };
        var p7 = stackalloc[] { new {} , new { i = 0 } };
    }
}
namespace System {
    public struct ValueTuple<T1, T2> {
        public ValueTuple(T1 a, T2 b) { }
    }
}
", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (9,18): error CS0826: No best type found for implicitly-typed array
                //         var p1 = stackalloc[] { new A(), new B() };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "stackalloc[] { new A(), new B() }").WithLocation(9, 18),
                // (10,18): error CS0826: No best type found for implicitly-typed array
                //         var p2 = stackalloc[] { };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "stackalloc[] { }").WithLocation(10, 18),
                // (11,18): error CS0826: No best type found for implicitly-typed array
                //         var p3 = stackalloc[] { Method() };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "stackalloc[] { Method() }").WithLocation(11, 18),
                // (12,18): error CS0826: No best type found for implicitly-typed array
                //         var p4 = stackalloc[] { null };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "stackalloc[] { null }").WithLocation(12, 18),
                // (13,18): error CS0826: No best type found for implicitly-typed array
                //         var p5 = stackalloc[] { (1, null) };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "stackalloc[] { (1, null) }").WithLocation(13, 18),
                // (14,18): error CS0826: No best type found for implicitly-typed array
                //         var p6 = stackalloc[] { () => { } };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "stackalloc[] { () => { } }").WithLocation(14, 18),
                // (15,18): error CS0826: No best type found for implicitly-typed array
                //         var p7 = stackalloc[] { new {} , new { i = 0 } };
                Diagnostic(ErrorCode.ERR_ImplicitlyTypedArrayNoBestType, "stackalloc[] { new {} , new { i = 0 } }").WithLocation(15, 18)
                );
        }

        [Fact]
        public void BestTypeNumeric()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        var obj1 = stackalloc[] { 1, 1.2 };
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                );

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            var variables = tree.GetCompilationUnitRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>();
            Assert.Equal(1, variables.Count());

            var obj1 = variables.ElementAt(0);
            Assert.Equal("obj1", obj1.Identifier.Text);

            var obj1Value = model.GetSemanticInfoSummary(obj1.Initializer.Value);
            Assert.Equal(SpecialType.System_Double, ((PointerTypeSymbol)obj1Value.Type).PointedAtType.SpecialType);
            Assert.Equal(SpecialType.System_Double, ((PointerTypeSymbol)obj1Value.ConvertedType).PointedAtType.SpecialType);
            Assert.Equal(ConversionKind.Identity, obj1Value.ImplicitConversion.Kind);
            var declared = model.GetDeclaredSymbol(obj1.Initializer.Value);
            Assert.Null(declared);
        }

        [Fact]
        public void BadBestType()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        var obj1 = stackalloc[] { """" };
        var obj2 = stackalloc[] { new {} };
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (6,20): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('string')
                //         var obj1 = stackalloc[] { "" };
                Diagnostic(ErrorCode.ERR_ManagedAddr, @"stackalloc[] { """" }").WithArguments("string").WithLocation(6, 20),
                // (7,20): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('<empty anonymous type>')
                //         var obj2 = stackalloc[] { new {} };
                Diagnostic(ErrorCode.ERR_ManagedAddr, "stackalloc[] { new {} }").WithArguments("<empty anonymous type>").WithLocation(7, 20)
                );
        }

        [Fact]
        public void WrongLength()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        var obj1 = stackalloc int[10] { };
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (6,20): error CS0847: An array initializer of length '10' is expected
                //         var obj1 = stackalloc int[10] { };
                Diagnostic(ErrorCode.ERR_ArrayInitializerIncorrectLength, "stackalloc int[10] { }").WithArguments("10").WithLocation(6, 20)
                );
        }

        [Fact]
        public void NoInit()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        var obj1 = stackalloc int[];
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (6,34): error CS1586: Array creation must have array size or array initializer
                //         var obj1 = stackalloc int[];
                Diagnostic(ErrorCode.ERR_MissingArraySize, "[]").WithLocation(6, 34)
                );
        }

        [Fact]
        public void NestedInit()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        var obj1 = stackalloc int[1] { { 42 } };
        var obj2 = stackalloc int[ ] { { 42 } };
        var obj3 = stackalloc    [ ] { { 42 } };
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (6,40): error CS0623: Array initializers can only be used in a variable or field initializer. Try using a new expression instead.
                //         var obj1 = stackalloc int[1] { { 42 } };
                Diagnostic(ErrorCode.ERR_ArrayInitInBadPlace, "{ 42 }").WithLocation(6, 40),
                // (7,40): error CS0623: Array initializers can only be used in a variable or field initializer. Try using a new expression instead.
                //         var obj2 = stackalloc int[ ] { { 42 } };
                Diagnostic(ErrorCode.ERR_ArrayInitInBadPlace, "{ 42 }").WithLocation(7, 40),
                // (8,40): error CS0623: Array initializers can only be used in a variable or field initializer. Try using a new expression instead.
                //         var obj3 = stackalloc    [ ] { { 42 } };
                Diagnostic(ErrorCode.ERR_ArrayInitInBadPlace, "{ 42 }").WithLocation(8, 40)
                );
        }

        [Fact]
        public void AsStatement()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        stackalloc[] {1};
        stackalloc int[] {1};
        stackalloc int[1] {1};
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (6,9): error CS1525: Invalid expression term 'stackalloc'
                //         stackalloc[] {1};
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(6, 9),
                // (6,9): error CS0201: Only assignment, call, increment, decrement, and new object expressions can be used as a statement
                //         stackalloc[] {1};
                Diagnostic(ErrorCode.ERR_IllegalStatement, "stackalloc[] {1}").WithLocation(6, 9),
                // (7,9): error CS1525: Invalid expression term 'stackalloc'
                //         stackalloc int[] {1};
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 9),
                // (7,9): error CS0201: Only assignment, call, increment, decrement, and new object expressions can be used as a statement
                //         stackalloc int[] {1};
                Diagnostic(ErrorCode.ERR_IllegalStatement, "stackalloc int[] {1}").WithLocation(7, 9),
                // (8,9): error CS1525: Invalid expression term 'stackalloc'
                //         stackalloc int[1] {1};
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 9),
                // (8,9): error CS0201: Only assignment, call, increment, decrement, and new object expressions can be used as a statement
                //         stackalloc int[1] {1};
                Diagnostic(ErrorCode.ERR_IllegalStatement, "stackalloc int[1] {1}").WithLocation(8, 9)
                );
        }

        [Fact]
        public void BadRank()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        var obj1 = stackalloc int[][] { 1 };
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (6,31): error CS1575: A stackalloc expression requires [] after type
                //         var obj1 = stackalloc int[][] { 1 };
                Diagnostic(ErrorCode.ERR_BadStackAllocExpr, "int[][]").WithLocation(6, 31)
                );
        }

        [Fact]
        public void BadDimension()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public void Method1()
    {
        var obj1 = stackalloc int[,] { 1 };
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (6,31): error CS1575: A stackalloc expression requires [] after type
                //         var obj1 = stackalloc int[,] { 1 };
                Diagnostic(ErrorCode.ERR_BadStackAllocExpr, "int[,]").WithLocation(6, 31)
                );
        }

        [Fact]
        public void TestFlowPass1()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
using System;
unsafe class Test
{
    public static void Main()
    {
        int i, j, k;
        var obj1 = stackalloc int [1] { i = 1 };
        var obj2 = stackalloc int [ ] { j = 2 };
        var obj3 = stackalloc     [ ] { k = 3 };

        Console.Write(i);
        Console.Write(j);
        Console.Write(k);
    }
}", TestOptions.UnsafeReleaseExe);

            CompileAndVerify(comp, expectedOutput: "123");
        }

        [Fact]
        public void TestFlowPass2()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
unsafe class Test
{
    public static void Main()
    {
        int i, j, k;
        var obj1 = stackalloc int [1] { i };
        var obj2 = stackalloc int [ ] { j };
        var obj3 = stackalloc     [ ] { k };
    }
}", TestOptions.UnsafeReleaseExe);

            comp.VerifyDiagnostics(
                // (7,41): error CS0165: Use of unassigned local variable 'i'
                //         var obj1 = stackalloc int [1] { i };
                Diagnostic(ErrorCode.ERR_UseDefViolation, "i").WithArguments("i").WithLocation(7, 41),
                // (8,41): error CS0165: Use of unassigned local variable 'j'
                //         var obj2 = stackalloc int [ ] { j };
                Diagnostic(ErrorCode.ERR_UseDefViolation, "j").WithArguments("j").WithLocation(8, 41),
                // (9,41): error CS0165: Use of unassigned local variable 'k'
                //         var obj3 = stackalloc     [ ] { k };
                Diagnostic(ErrorCode.ERR_UseDefViolation, "k").WithArguments("k").WithLocation(9, 41)
                );
        }

        [Fact]
        public void ConversionFromPointerStackAlloc_UserDefined_Implicit()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
using System;
unsafe class Test
{
    public void Method1()
    {
        Test obj1 = stackalloc int[3] { 1, 2, 3 };
        var obj2 = stackalloc int[3] { 1, 2, 3 };
        Span<int> obj3 = stackalloc int[3] { 1, 2, 3 };
        int* obj4 = stackalloc int[3] { 1, 2, 3 };
        double* obj5 = stackalloc int[3] { 1, 2, 3 };
    }
    
    public void Method2()
    {
        Test obj1 = stackalloc int[] { 1, 2, 3 };
        var obj2 = stackalloc int[] { 1, 2, 3 };
        Span<int> obj3 = stackalloc int[] { 1, 2, 3 };
        int* obj4 = stackalloc int[] { 1, 2, 3 };
        double* obj5 = stackalloc int[] { 1, 2, 3 };
    }

    public void Method3()
    {
        Test obj1 = stackalloc[] { 1, 2, 3 };
        var obj2 = stackalloc[] { 1, 2, 3 };
        Span<int> obj3 = stackalloc[] { 1, 2, 3 };
        int* obj4 = stackalloc[] { 1, 2, 3 };
        double* obj5 = stackalloc[] { 1, 2, 3 };
    }

    public static implicit operator Test(int* value) 
    {
        return default(Test);
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (11,24): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double*' is not possible.
                //         double* obj5 = stackalloc int[3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int[3] { 1, 2, 3 }").WithArguments("int", "double*").WithLocation(11, 24),
                // (20,24): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double*' is not possible.
                //         double* obj5 = stackalloc int[] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int[] { 1, 2, 3 }").WithArguments("int", "double*").WithLocation(20, 24),
                // (29,24): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double*' is not possible.
                //         double* obj5 = stackalloc[] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc[] { 1, 2, 3 }").WithArguments("int", "double*").WithLocation(29, 24)
                );

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            var variables = tree.GetCompilationUnitRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>();
            Assert.Equal(15, variables.Count());

            for (int i = 0; i < 15; i += 5)
            {
                var obj1 = variables.ElementAt(i);
                Assert.Equal("obj1", obj1.Identifier.Text);

                var obj1Value = model.GetSemanticInfoSummary(obj1.Initializer.Value);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj1Value.Type).PointedAtType.SpecialType);
                Assert.Equal("Test", obj1Value.ConvertedType.Name);
                Assert.Equal(ConversionKind.ImplicitUserDefined, obj1Value.ImplicitConversion.Kind);

                var obj2 = variables.ElementAt(i + 1);
                Assert.Equal("obj2", obj2.Identifier.Text);

                var obj2Value = model.GetSemanticInfoSummary(obj2.Initializer.Value);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj2Value.Type).PointedAtType.SpecialType);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj2Value.ConvertedType).PointedAtType.SpecialType);
                Assert.Equal(ConversionKind.Identity, obj2Value.ImplicitConversion.Kind);

                var obj3 = variables.ElementAt(i + 2);
                Assert.Equal("obj3", obj3.Identifier.Text);

                var obj3Value = model.GetSemanticInfoSummary(obj3.Initializer.Value);
                Assert.Equal("Span", obj3Value.Type.Name);
                Assert.Equal("Span", obj3Value.ConvertedType.Name);
                Assert.Equal(ConversionKind.Identity, obj3Value.ImplicitConversion.Kind);

                var obj4 = variables.ElementAt(i + 3);
                Assert.Equal("obj4", obj4.Identifier.Text);

                var obj4Value = model.GetSemanticInfoSummary(obj4.Initializer.Value);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj4Value.Type).PointedAtType.SpecialType);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj4Value.ConvertedType).PointedAtType.SpecialType);
                Assert.Equal(ConversionKind.Identity, obj4Value.ImplicitConversion.Kind);

                var obj5 = variables.ElementAt(i + 4);
                Assert.Equal("obj5", obj5.Identifier.Text);

                var obj5Value = model.GetSemanticInfoSummary(obj5.Initializer.Value);
                Assert.Null(obj5Value.Type);
                Assert.Equal(SpecialType.System_Double, ((PointerTypeSymbol)obj5Value.ConvertedType).PointedAtType.SpecialType);
                Assert.Equal(ConversionKind.NoConversion, obj5Value.ImplicitConversion.Kind);
            }
        }

        [Fact]
        public void ConversionFromPointerStackAlloc_UserDefined_Explicit()
        {
            var comp = CreateCompilationWithMscorlibAndSpan(@"
using System;
unsafe class Test
{
    public void Method1()
    {
        Test obj1 = (Test)stackalloc int[3]  { 1, 2, 3 };
        var obj2 = stackalloc int[3] { 1, 2, 3 };
        Span<int> obj3 = stackalloc int[3] { 1, 2, 3 };
        int* obj4 = stackalloc int[3] { 1, 2, 3 };
        double* obj5 = stackalloc int[3] { 1, 2, 3 };
    }
    
    public void Method2()
    {
        Test obj1 = (Test)stackalloc int[]  { 1, 2, 3 };
        var obj2 = stackalloc int[] { 1, 2, 3 };
        Span<int> obj3 = stackalloc int[] { 1, 2, 3 };
        int* obj4 = stackalloc int[] { 1, 2, 3 };
        double* obj5 = stackalloc int[] { 1, 2, 3 };
    }

    public void Method3()
    {
        Test obj1 = (Test)stackalloc []  { 1, 2, 3 };
        var obj2 = stackalloc[] { 1, 2, 3 };
        Span<int> obj3 = stackalloc [] { 1, 2, 3 };
        int* obj4 = stackalloc[] { 1, 2, 3 };
        double* obj5 = stackalloc[] { 1, 2, 3 };
    }

    public static explicit operator Test(Span<int> value) 
    {
        return default(Test);
    }
}", TestOptions.UnsafeReleaseDll);

            comp.VerifyDiagnostics(
                // (11,24): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double*' is not possible.
                //         double* obj5 = stackalloc int[3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int[3] { 1, 2, 3 }").WithArguments("int", "double*").WithLocation(11, 24),
                // (20,24): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double*' is not possible.
                //         double* obj5 = stackalloc int[] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int[] { 1, 2, 3 }").WithArguments("int", "double*").WithLocation(20, 24),
                // (29,24): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double*' is not possible.
                //         double* obj5 = stackalloc[] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc[] { 1, 2, 3 }").WithArguments("int", "double*").WithLocation(29, 24)
                );

            var tree = comp.SyntaxTrees.Single();
            var model = comp.GetSemanticModel(tree);

            var variables = tree.GetCompilationUnitRoot().DescendantNodes().OfType<VariableDeclaratorSyntax>();
            Assert.Equal(15, variables.Count());

            for (int i = 0; i < 15; i += 5)
            {
                var obj1 = variables.ElementAt(i);
                Assert.Equal("obj1", obj1.Identifier.Text);
                Assert.Equal(SyntaxKind.CastExpression, obj1.Initializer.Value.Kind());

                var obj1Value = model.GetSemanticInfoSummary(((CastExpressionSyntax)obj1.Initializer.Value).Expression);
                Assert.Equal("Span", obj1Value.Type.Name);
                Assert.Equal("Span", obj1Value.ConvertedType.Name);
                Assert.Equal(ConversionKind.Identity, obj1Value.ImplicitConversion.Kind);

                var obj2 = variables.ElementAt(i + 1);
                Assert.Equal("obj2", obj2.Identifier.Text);

                var obj2Value = model.GetSemanticInfoSummary(obj2.Initializer.Value);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj2Value.Type).PointedAtType.SpecialType);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj2Value.ConvertedType).PointedAtType.SpecialType);
                Assert.Equal(ConversionKind.Identity, obj2Value.ImplicitConversion.Kind);

                var obj3 = variables.ElementAt(i + 2);
                Assert.Equal("obj3", obj3.Identifier.Text);

                var obj3Value = model.GetSemanticInfoSummary(obj3.Initializer.Value);
                Assert.Equal("Span", obj3Value.Type.Name);
                Assert.Equal("Span", obj3Value.ConvertedType.Name);
                Assert.Equal(ConversionKind.Identity, obj3Value.ImplicitConversion.Kind);

                var obj4 = variables.ElementAt(i + 3);
                Assert.Equal("obj4", obj4.Identifier.Text);

                var obj4Value = model.GetSemanticInfoSummary(obj4.Initializer.Value);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj4Value.Type).PointedAtType.SpecialType);
                Assert.Equal(SpecialType.System_Int32, ((PointerTypeSymbol)obj4Value.ConvertedType).PointedAtType.SpecialType);
                Assert.Equal(ConversionKind.Identity, obj4Value.ImplicitConversion.Kind);

                var obj5 = variables.ElementAt(i + 4);
                Assert.Equal("obj5", obj5.Identifier.Text);

                var obj5Value = model.GetSemanticInfoSummary(obj5.Initializer.Value);
                Assert.Null(obj5Value.Type);
                Assert.Equal(SpecialType.System_Double, ((PointerTypeSymbol)obj5Value.ConvertedType).PointedAtType.SpecialType);
                Assert.Equal(ConversionKind.NoConversion, obj5Value.ImplicitConversion.Kind);
            }
        }

        [Fact]
        public void ConversionError()
        {
            CreateCompilationWithMscorlibAndSpan(@"
class Test
{
    void Method1()
    {
        double x = stackalloc int[3] { 1, 2, 3 };        // implicit
        short y = (short)stackalloc int[3] { 1, 2, 3 };  // explicit
    }

    void Method2()
    {
        double x = stackalloc int[] { 1, 2, 3 };          // implicit
        short y = (short)stackalloc int[] { 1, 2, 3 };    // explicit
    }

    void Method3()
    {
        double x = stackalloc[] { 1, 2, 3 };              // implicit
        short y = (short)stackalloc[] { 1, 2, 3 };        // explicit
    }
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics(

                // (6,20): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double' is not possible.
                //         double x = stackalloc int[3] { 1, 2, 3 };        // implicit
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int[3] { 1, 2, 3 }").WithArguments("int", "double").WithLocation(6, 20),
                // (7,19): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'short' is not possible.
                //         short y = (short)stackalloc int[3] { 1, 2, 3 };  // explicit
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "(short)stackalloc int[3] { 1, 2, 3 }").WithArguments("int", "short").WithLocation(7, 19),
                // (12,20): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double' is not possible.
                //         double x = stackalloc int[] { 1, 2, 3 };          // implicit
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int[] { 1, 2, 3 }").WithArguments("int", "double").WithLocation(12, 20),
                // (13,19): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'short' is not possible.
                //         short y = (short)stackalloc int[] { 1, 2, 3 };    // explicit
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "(short)stackalloc int[] { 1, 2, 3 }").WithArguments("int", "short").WithLocation(13, 19),
                // (18,20): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'double' is not possible.
                //         double x = stackalloc[] { 1, 2, 3 };          // implicit
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc[] { 1, 2, 3 }").WithArguments("int", "double").WithLocation(18, 20),
                // (19,19): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'short' is not possible.
                //         short y = (short)stackalloc[] { 1, 2, 3 };    // explicit
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "(short)stackalloc[] { 1, 2, 3 }").WithArguments("int", "short").WithLocation(19, 19)
                );
        }

        [Fact]
        public void MissingSpanType()
        {
            CreateStandardCompilation(@"
class Test
{
    void M()
    {
        Span<int> a1 = stackalloc int [3] { 1, 2, 3 };
        Span<int> a2 = stackalloc int [ ] { 1, 2, 3 };
        Span<int> a3 = stackalloc     [ ] { 1, 2, 3 };
    }
}").VerifyDiagnostics(
                // (6,9): error CS0246: The type or namespace name 'Span<>' could not be found (are you missing a using directive or an assembly reference?)
                //         Span<int> a1 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Span<int>").WithArguments("Span<>").WithLocation(6, 9),
                // (6,24): error CS0518: Predefined type 'System.Span`1' is not defined or imported
                //         Span<int> a1 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "stackalloc int [3] { 1, 2, 3 }").WithArguments("System.Span`1").WithLocation(6, 24),
                // (7,9): error CS0246: The type or namespace name 'Span<>' could not be found (are you missing a using directive or an assembly reference?)
                //         Span<int> a2 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Span<int>").WithArguments("Span<>").WithLocation(7, 9),
                // (7,24): error CS0518: Predefined type 'System.Span`1' is not defined or imported
                //         Span<int> a2 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "stackalloc int [ ] { 1, 2, 3 }").WithArguments("System.Span`1").WithLocation(7, 24),
                // (8,9): error CS0246: The type or namespace name 'Span<>' could not be found (are you missing a using directive or an assembly reference?)
                //         Span<int> a3 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_SingleTypeNameNotFound, "Span<int>").WithArguments("Span<>").WithLocation(8, 9),
                // (8,24): error CS0518: Predefined type 'System.Span`1' is not defined or imported
                //         Span<int> a3 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_PredefinedTypeNotFound, "stackalloc     [ ] { 1, 2, 3 }").WithArguments("System.Span`1").WithLocation(8, 24)
                );
        }

        [Fact]
        public void MissingSpanConstructor()
        {
            CreateStandardCompilation(@"
namespace System
{
    ref struct Span<T>
    {
    }
    class Test
    {
        void M()
        {
            Span<int> a1 = stackalloc int [3] { 1, 2, 3 };
            Span<int> a2 = stackalloc int [ ] { 1, 2, 3 };
            Span<int> a3 = stackalloc     [ ] { 1, 2, 3 };
        }
    }
}").VerifyDiagnostics(
                // (11,28): error CS0656: Missing compiler required member 'System.Span`1..ctor'
                //             Span<int> a1 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "stackalloc int [3] { 1, 2, 3 }").WithArguments("System.Span`1", ".ctor").WithLocation(11, 28),
                // (12,28): error CS0656: Missing compiler required member 'System.Span`1..ctor'
                //             Span<int> a2 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "stackalloc int [ ] { 1, 2, 3 }").WithArguments("System.Span`1", ".ctor").WithLocation(12, 28),
                // (13,28): error CS0656: Missing compiler required member 'System.Span`1..ctor'
                //             Span<int> a3 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_MissingPredefinedMember, "stackalloc     [ ] { 1, 2, 3 }").WithArguments("System.Span`1", ".ctor").WithLocation(13, 28)
                );
        }

        [Fact]
        public void ConditionalExpressionOnSpan_BothStackallocSpans()
        {
            CreateCompilationWithMscorlibAndSpan(@"
class Test
{
    void M()
    {
        var x1 = true ? stackalloc int [3] { 1, 2, 3 } : stackalloc int [3] { 1, 2, 3 };
        var x2 = true ? stackalloc int [ ] { 1, 2, 3 } : stackalloc int [ ] { 1, 2, 3 };
        var x3 = true ? stackalloc     [ ] { 1, 2, 3 } : stackalloc     [ ] { 1, 2, 3 };
    }
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics();
        }

        [Fact]
        public void ConditionalExpressionOnSpan_Convertible()
        {
            CreateCompilationWithMscorlibAndSpan(@"
using System;
class Test
{
    void M()
    {
        var x1 = true ? stackalloc int [3] { 1, 2, 3 } : (Span<int>)stackalloc int [3] { 1, 2, 3 };
        var x2 = true ? stackalloc int [ ] { 1, 2, 3 } : (Span<int>)stackalloc int [ ] { 1, 2, 3 };
        var x3 = true ? stackalloc     [ ] { 1, 2, 3 } : (Span<int>)stackalloc     [ ] { 1, 2, 3 };
    }
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics();
        }

        [Fact]
        public void ConditionalExpressionOnSpan_NoCast()
        {
            CreateCompilationWithMscorlibAndSpan(@"
using System;
class Test
{
    void M()
    {
        var x1 = true ? stackalloc int [3] { 1, 2, 3, } : (Span<int>)stackalloc short [3] { (short)1, (short)2, (short)3 };
        var x2 = true ? stackalloc int [ ] { 1, 2, 3, } : (Span<int>)stackalloc short [ ] { (short)1, (short)2, (short)3 };
        var x3 = true ? stackalloc     [ ] { 1, 2, 3, } : (Span<int>)stackalloc       [ ] { (short)1, (short)2, (short)3 };
    } 
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics(
                // (7,59): error CS8346: Conversion of a stackalloc expression of type 'short' to type 'Span<int>' is not possible.
                //         var x1 = true ? stackalloc int [3] { 1, 2, 3, } : (Span<int>)stackalloc short [3] { (short)1, (short)2, (short)3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "(Span<int>)stackalloc short [3] { (short)1, (short)2, (short)3 }").WithArguments("short", "System.Span<int>").WithLocation(7, 59),
                // (8,59): error CS8346: Conversion of a stackalloc expression of type 'short' to type 'Span<int>' is not possible.
                //         var x2 = true ? stackalloc int [ ] { 1, 2, 3, } : (Span<int>)stackalloc short [ ] { (short)1, (short)2, (short)3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "(Span<int>)stackalloc short [ ] { (short)1, (short)2, (short)3 }").WithArguments("short", "System.Span<int>").WithLocation(8, 59),
                // (9,59): error CS8346: Conversion of a stackalloc expression of type 'short' to type 'Span<int>' is not possible.
                //         var x3 = true ? stackalloc     [ ] { 1, 2, 3, } : (Span<int>)stackalloc       [ ] { (short)1, (short)2, (short)3 };
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "(Span<int>)stackalloc       [ ] { (short)1, (short)2, (short)3 }").WithArguments("short", "System.Span<int>").WithLocation(9, 59)
                );
        }

        [Fact]
        public void ConditionalExpressionOnSpan_CompatibleTypes()
        {
            CreateCompilationWithMscorlibAndSpan(@"
using System;
class Test
{
    void M()
    {
        Span<int> a1 = stackalloc int [3] { 1, 2, 3 };
        Span<int> a2 = stackalloc int [ ] { 1, 2, 3 };
        Span<int> a3 = stackalloc     [ ] { 1, 2, 3 };

        var x1 = true ? stackalloc int [3] { 1, 2, 3 } : a1;
        var x2 = true ? stackalloc int [ ] { 1, 2, 3 } : a2;
        var x3 = true ? stackalloc     [ ] { 1, 2, 3 } : a3;
    }
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics();
        }

        [Fact]
        public void ConditionalExpressionOnSpan_IncompatibleTypes()
        {
            CreateCompilationWithMscorlibAndSpan(@"
using System;
class Test
{
    void M()
    {
        Span<short> a = stackalloc short [10];
        var x1 = true ? stackalloc int [3] { 1, 2, 3 } : a;
        var x2 = true ? stackalloc int [ ] { 1, 2, 3 } : a;
        var x3 = true ? stackalloc     [ ] { 1, 2, 3 } : a;
    }
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics(
                // (8,18): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'stackalloc int[3]' and 'Span<short>'
                //         var x1 = true ? stackalloc int [3] { 1, 2, 3 } : a;
                Diagnostic(ErrorCode.ERR_InvalidQM, "true ? stackalloc int [3] { 1, 2, 3 } : a").WithArguments("stackalloc int[3]", "System.Span<short>").WithLocation(8, 18),
                // (9,18): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'stackalloc int[]' and 'Span<short>'
                //         var x2 = true ? stackalloc int [ ] { 1, 2, 3 } : a;
                Diagnostic(ErrorCode.ERR_InvalidQM, "true ? stackalloc int [ ] { 1, 2, 3 } : a").WithArguments("stackalloc int[]", "System.Span<short>").WithLocation(9, 18),
                // (10,18): error CS0173: Type of conditional expression cannot be determined because there is no implicit conversion between 'stackalloc int[]' and 'Span<short>'
                //         var x3 = true ? stackalloc     [ ] { 1, 2, 3 } : a;
                Diagnostic(ErrorCode.ERR_InvalidQM, "true ? stackalloc     [ ] { 1, 2, 3 } : a").WithArguments("stackalloc int[]", "System.Span<short>").WithLocation(10, 18)
                );
        }

        [Fact]
        public void ConditionalExpressionOnSpan_Nested()
        {
            CreateCompilationWithMscorlibAndSpan(@"
class Test
{
    bool N() => true;

    void M()
    {
        var x = N()
            ? N()
                ? stackalloc int[1] { 42 }
                : stackalloc int[ ] { 42 }
            : N()
                ? stackalloc[] { 42 }
                : N()
                    ? stackalloc int[2]
                    : stackalloc int[3];
    }
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics();
        }

        [Fact]
        public void BooleanOperatorOnSpan_NoTargetTyping()
        {
            CreateCompilationWithMscorlibAndSpan(@"
class Test
{
    void M()
    {
        if (stackalloc int[3] { 1, 2, 3 } == stackalloc int[3] { 1, 2, 3 }) { }
        if (stackalloc int[ ] { 1, 2, 3 } == stackalloc int[ ] { 1, 2, 3 }) { }
        if (stackalloc    [ ] { 1, 2, 3 } == stackalloc    [ ] { 1, 2, 3 }) { }
    }
}", TestOptions.UnsafeReleaseDll).VerifyDiagnostics(
                // (6,13): error CS1525: Invalid expression term 'stackalloc'
                //         if (stackalloc int[3] { 1, 2, 3 } == stackalloc int[3] { 1, 2, 3 }) { }
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(6, 13),
                // (6,46): error CS1525: Invalid expression term 'stackalloc'
                //         if (stackalloc int[3] { 1, 2, 3 } == stackalloc int[3] { 1, 2, 3 }) { }
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(6, 46),
                // (7,13): error CS1525: Invalid expression term 'stackalloc'
                //         if (stackalloc int[ ] { 1, 2, 3 } == stackalloc int[ ] { 1, 2, 3 }) { }
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 13),
                // (7,46): error CS1525: Invalid expression term 'stackalloc'
                //         if (stackalloc int[ ] { 1, 2, 3 } == stackalloc int[ ] { 1, 2, 3 }) { }
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 46),
                // (8,13): error CS1525: Invalid expression term 'stackalloc'
                //         if (stackalloc    [ ] { 1, 2, 3 } == stackalloc    [ ] { 1, 2, 3 }) { }
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 13),
                // (8,46): error CS1525: Invalid expression term 'stackalloc'
                //         if (stackalloc    [ ] { 1, 2, 3 } == stackalloc    [ ] { 1, 2, 3 }) { }
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 46)
            );
        }

        [Fact]
        public void StackAllocInitializerSyntaxProducesErrorsOnEarlierVersions()
        {
            var parseOptions = new CSharpParseOptions().WithLanguageVersion(LanguageVersion.CSharp7);

            CreateCompilationWithMscorlibAndSpan(@"
using System;
class Test
{
    void M()
    {
        Span<int> x1 = stackalloc int [3] { 1, 2, 3 };
        Span<int> x2 = stackalloc int [ ] { 1, 2, 3 };
        Span<int> x3 = stackalloc     [ ] { 1, 2, 3 };
    }
}", options: TestOptions.UnsafeReleaseDll, parseOptions: parseOptions).VerifyDiagnostics(
                // (7,24): error CS8107: Feature 'stackalloc initilizer' is not available in C# 7.0. Please use language version 7.3 or greater.
                //         Span<int> x1 = stackalloc int[1] { 2 };
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7, "stackalloc").WithArguments("stackalloc initializer", "7.3").WithLocation(7, 24),
                // (8,24): error CS8107: Feature 'stackalloc initilizer' is not available in C# 7.0. Please use language version 7.3 or greater.
                //         Span<int> x2 = stackalloc int[] { 2 };
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7, "stackalloc").WithArguments("stackalloc initializer", "7.3").WithLocation(8, 24),
                // (9,24): error CS8107: Feature 'stackalloc initilizer' is not available in C# 7.0. Please use language version 7.3 or greater.
                //         Span<int> x3 = stackalloc [] { 2 };
                Diagnostic(ErrorCode.ERR_FeatureNotAvailableInVersion7, "stackalloc").WithArguments("stackalloc initializer", "7.3").WithLocation(9, 24)
                );
        }

        [Fact]
        public void StackAllocSyntaxProducesUnsafeErrorInSafeCode()
        {
            CreateStandardCompilation(@"
class Test
{
    void M()
    {
        var x1 = stackalloc int [3] { 1, 2, 3 };
        var x2 = stackalloc int [ ] { 1, 2, 3 };
        var x3 = stackalloc     [ ] { 1, 2, 3 };
    }
}", options: TestOptions.UnsafeReleaseDll).VerifyDiagnostics(
                // (6,18): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context
                //         var x1 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_UnsafeNeeded, "stackalloc int [3] { 1, 2, 3 }").WithLocation(6, 18),
                // (7,18): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context
                //         var x2 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_UnsafeNeeded, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(7, 18),
                // (8,18): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context
                //         var x3 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_UnsafeNeeded, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(8, 18)
                );
        }

        [Fact]
        public void StackAllocInUsing1()
        {
            var test = @"
public class Test
{
    unsafe public static void Main()
    {
        using (var v1 = stackalloc int [3] { 1, 2, 3 })
        using (var v2 = stackalloc int [ ] { 1, 2, 3 })
        using (var v3 = stackalloc     [ ] { 1, 2, 3 })
        {
        }
    }
}
";
            CreateCompilationWithMscorlibAndSpan(test, options: TestOptions.ReleaseDll.WithAllowUnsafe(true)).VerifyDiagnostics(
                // (6,16): error CS1674: 'int*': type used in a using statement must be implicitly convertible to 'System.IDisposable'
                //         using (var v1 = stackalloc int [3] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_NoConvToIDisp, "var v1 = stackalloc int [3] { 1, 2, 3 }").WithArguments("int*").WithLocation(6, 16),
                // (7,16): error CS1674: 'int*': type used in a using statement must be implicitly convertible to 'System.IDisposable'
                //         using (var v2 = stackalloc int [ ] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_NoConvToIDisp, "var v2 = stackalloc int [ ] { 1, 2, 3 }").WithArguments("int*").WithLocation(7, 16),
                // (8,16): error CS1674: 'int*': type used in a using statement must be implicitly convertible to 'System.IDisposable'
                //         using (var v3 = stackalloc     [ ] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_NoConvToIDisp, "var v3 = stackalloc     [ ] { 1, 2, 3 }").WithArguments("int*").WithLocation(8, 16)
                );
        }

        [Fact]
        public void StackAllocInUsing2()
        {
            var test = @"
public class Test
{
    unsafe public static void Main()
    {
        using (System.IDisposable v1 = stackalloc int [3] { 1, 2, 3 })
        using (System.IDisposable v2 = stackalloc int [ ] { 1, 2, 3 })
        using (System.IDisposable v3 = stackalloc     [ ] { 1, 2, 3 })
        {
        }
    }
}
";
            CreateCompilationWithMscorlibAndSpan(test, options: TestOptions.ReleaseDll.WithAllowUnsafe(true)).VerifyDiagnostics(
                // (6,40): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'IDisposable' is not possible.
                //         using (System.IDisposable v1 = stackalloc int [3] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int [3] { 1, 2, 3 }").WithArguments("int", "System.IDisposable").WithLocation(6, 40),
                // (7,40): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'IDisposable' is not possible.
                //         using (System.IDisposable v2 = stackalloc int [ ] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc int [ ] { 1, 2, 3 }").WithArguments("int", "System.IDisposable").WithLocation(7, 40),
                // (8,40): error CS8346: Conversion of a stackalloc expression of type 'int' to type 'IDisposable' is not possible.
                //         using (System.IDisposable v3 = stackalloc     [ ] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_StackAllocConversionNotPossible, "stackalloc     [ ] { 1, 2, 3 }").WithArguments("int", "System.IDisposable").WithLocation(8, 40)
                );
        }

        [Fact]
        public void StackAllocInFixed()
        {
            var test = @"
public class Test
{
    unsafe public static void Main()
    {
        fixed (int* v1 = stackalloc int [3] { 1, 2, 3 })
        fixed (int* v2 = stackalloc int [ ] { 1, 2, 3 })
        fixed (int* v3 = stackalloc     [ ] { 1, 2, 3 })
        {
        }
    }
}
";
            CreateCompilationWithMscorlibAndSpan(test, options: TestOptions.ReleaseDll.WithAllowUnsafe(true)).VerifyDiagnostics(
                // (6,26): error CS0213: You cannot use the fixed statement to take the address of an already fixed expression
                //         fixed (int* v1 = stackalloc int [3] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_FixedNotNeeded, "stackalloc int [3] { 1, 2, 3 }").WithLocation(6, 26),
                // (7,26): error CS0213: You cannot use the fixed statement to take the address of an already fixed expression
                //         fixed (int* v2 = stackalloc int [ ] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_FixedNotNeeded, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(7, 26),
                // (8,26): error CS0213: You cannot use the fixed statement to take the address of an already fixed expression
                //         fixed (int* v3 = stackalloc     [ ] { 1, 2, 3 })
                Diagnostic(ErrorCode.ERR_FixedNotNeeded, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(8, 26)
                );
        }

        [Fact]
        public void ConstStackAllocExpression()
        {
            var test = @"
unsafe public class Test
{
    void M()
    {
        const int* p1 = stackalloc int [3] { 1, 2, 3 };
        const int* p2 = stackalloc int [ ] { 1, 2, 3 };
        const int* p3 = stackalloc     [ ] { 1, 2, 3 };
    }
}
";
            CreateStandardCompilation(test, options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)).VerifyDiagnostics(
                // (6,15): error CS0283: The type 'int*' cannot be declared const
                //         const int* p1 = stackalloc int[1] { 1 };
                Diagnostic(ErrorCode.ERR_BadConstType, "int*").WithArguments("int*").WithLocation(6, 15),
                // (7,15): error CS0283: The type 'int*' cannot be declared const
                //         const int* p2 = stackalloc int[] { 1 };
                Diagnostic(ErrorCode.ERR_BadConstType, "int*").WithArguments("int*").WithLocation(7, 15),
                // (8,15): error CS0283: The type 'int*' cannot be declared const
                //         const int* p3 = stackalloc [] { 1 };
                Diagnostic(ErrorCode.ERR_BadConstType, "int*").WithArguments("int*").WithLocation(8, 15)
                );
        }

        [Fact]
        public void RefStackAllocAssignment_ValueToRef()
        {
            var test = @"
using System;
public class Test
{
    void M()
    {
        ref Span<int> p1 = stackalloc int [3] { 1, 2, 3 };
        ref Span<int> p2 = stackalloc int [ ] { 1, 2, 3 };
        ref Span<int> p3 = stackalloc     [ ] { 1, 2, 3 };
    }
}
";
            CreateCompilationWithMscorlibAndSpan(test, options: TestOptions.ReleaseDll.WithAllowUnsafe(true)).VerifyDiagnostics(
                // (7,23): error CS8172: Cannot initialize a by-reference variable with a value
                //         ref Span<int> p1 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InitializeByReferenceVariableWithValue, "p1 = stackalloc int [3] { 1, 2, 3 }").WithLocation(7, 23),
                // (7,28): error CS1510: A ref or out value must be an assignable variable
                //         ref Span<int> p1 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_RefLvalueExpected, "stackalloc int [3] { 1, 2, 3 }").WithLocation(7, 28),
                // (8,23): error CS8172: Cannot initialize a by-reference variable with a value
                //         ref Span<int> p2 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InitializeByReferenceVariableWithValue, "p2 = stackalloc int [ ] { 1, 2, 3 }").WithLocation(8, 23),
                // (8,28): error CS1510: A ref or out value must be an assignable variable
                //         ref Span<int> p2 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_RefLvalueExpected, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(8, 28),
                // (9,23): error CS8172: Cannot initialize a by-reference variable with a value
                //         ref Span<int> p3 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InitializeByReferenceVariableWithValue, "p3 = stackalloc     [ ] { 1, 2, 3 }").WithLocation(9, 23),
                // (9,28): error CS1510: A ref or out value must be an assignable variable
                //         ref Span<int> p3 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_RefLvalueExpected, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(9, 28)
                );
        }

        [Fact]
        public void RefStackAllocAssignment_RefToRef()
        {
            var test = @"
using System;
public class Test
{
    void M()
    {
        ref Span<int> p1 = ref stackalloc int [3] { 1, 2, 3 };
        ref Span<int> p2 = ref stackalloc int [ ] { 1, 2, 3 };
        ref Span<int> p3 = ref stackalloc     [ ] { 1, 2, 3 };
    }
}
";
            CreateCompilationWithMscorlibAndSpan(test, options: TestOptions.ReleaseDll.WithAllowUnsafe(true)).VerifyDiagnostics(
                // (7,32): error CS1525: Invalid expression term 'stackalloc'
                //         ref Span<int> p1 = ref stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 32),
                // (8,32): error CS1525: Invalid expression term 'stackalloc'
                //         ref Span<int> p2 = ref stackalloc int [] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 32),
                // (9,32): error CS1525: Invalid expression term 'stackalloc'
                //         ref Span<int> p3 = ref stackalloc [] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(9, 32)
                );
        }

        [Fact]
        public void InvalidPositionForStackAllocSpan()
        {
            var test = @"
using System;
public class Test
{
    void M()
    {
        N(stackalloc int [3] { 1, 2, 3 });
        N(stackalloc int [ ] { 1, 2, 3 });
        N(stackalloc     [ ] { 1, 2, 3 });
    }
    void N(Span<int> span)
    {
    }
}
";
            CreateCompilationWithMscorlibAndSpan(test, options: TestOptions.ReleaseDll.WithAllowUnsafe(true)).VerifyDiagnostics(
                // (7,11): error CS1525: Invalid expression term 'stackalloc'
                //         N(stackalloc int [3] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 11),
                // (8,11): error CS1525: Invalid expression term 'stackalloc'
                //         N(stackalloc int [] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 11),
                // (9,11): error CS1525: Invalid expression term 'stackalloc'
                //         N(stackalloc [] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(9, 11)
                );
        }

        [Fact]
        public void CannotDotIntoStackAllocExpression()
        {
            var test = @"
public class Test
{
    void M()
    {
        int length1 = (stackalloc int [3] { 1, 2, 3 }).Length;
        int length2 = (stackalloc int [ ] { 1, 2, 3 }).Length;
        int length3 = (stackalloc     [ ] { 1, 2, 3 }).Length;

        int length4 = stackalloc int [3] { 1, 2, 3 }.Length;
        int length5 = stackalloc int [ ] { 1, 2, 3 }.Length;
        int length6 = stackalloc     [ ] { 1, 2, 3 }.Length;
    }
}
";
            CreateCompilationWithMscorlibAndSpan(test, TestOptions.ReleaseDll).VerifyDiagnostics(
                // (6,24): error CS1525: Invalid expression term 'stackalloc'
                //         int length1 = (stackalloc int [3] { 1, 2, 3 }).Length;
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(6, 24),
                // (7,24): error CS1525: Invalid expression term 'stackalloc'
                //         int length2 = (stackalloc int [ ] { 1, 2, 3 }).Length;
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 24),
                // (8,24): error CS1525: Invalid expression term 'stackalloc'
                //         int length3 = (stackalloc     [ ] { 1, 2, 3 }).Length;
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 24),
                // (10,23): error CS1525: Invalid expression term 'stackalloc'
                //         int length4 = stackalloc int [3] { 1, 2, 3 }.Length;
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(10, 23),
                // (11,23): error CS1525: Invalid expression term 'stackalloc'
                //         int length5 = stackalloc int [ ] { 1, 2, 3 }.Length;
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(11, 23),
                // (12,23): error CS1525: Invalid expression term 'stackalloc'
                //         int length6 = stackalloc     [ ] { 1, 2, 3 }.Length;
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(12, 23)
                );
        }

        [Fact]
        public void OverloadResolution_Fail()
        {
            var test = @"
using System;
unsafe public class Test
{
    static void Main()
    {
        Invoke(stackalloc int [3] { 1, 2, 3 });
        Invoke(stackalloc int [ ] { 1, 2, 3 });
        Invoke(stackalloc     [ ] { 1, 2, 3 });
    }

    static void Invoke(Span<short> shortSpan) => Console.WriteLine(""shortSpan"");
    static void Invoke(Span<bool> boolSpan) => Console.WriteLine(""boolSpan"");
    static void Invoke(int* intPointer) => Console.WriteLine(""intPointer"");
    static void Invoke(void* voidPointer) => Console.WriteLine(""voidPointer"");
}
";
            CreateCompilationWithMscorlibAndSpan(test, TestOptions.UnsafeReleaseExe).VerifyDiagnostics(
                // (7,16): error CS1525: Invalid expression term 'stackalloc'
                //         Invoke(stackalloc int[3] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 16),
                // (8,16): error CS1525: Invalid expression term 'stackalloc'
                //         Invoke(stackalloc int[] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 16),
                // (9,16): error CS1525: Invalid expression term 'stackalloc'
                //         Invoke(stackalloc[] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(9, 16)
            );
        }

        [Fact]
        public void StackAllocWithDynamic()
        {
            CreateStandardCompilation(@"
class Program
{
    static void Main()
    {
        dynamic d = 1;
        var d1 = stackalloc dynamic [3] { d };
        var d2 = stackalloc dynamic [ ] { d };
        var d3 = stackalloc         [ ] { d };
    }
}").VerifyDiagnostics(
                // (7,29): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('dynamic')
                //         var d1 = stackalloc dynamic [3] { d };
                Diagnostic(ErrorCode.ERR_ManagedAddr, "dynamic").WithArguments("dynamic").WithLocation(7, 29),
                // (7,18): error CS0847: An array initializer of length '3' is expected
                //         var d1 = stackalloc dynamic [3] { d };
                Diagnostic(ErrorCode.ERR_ArrayInitializerIncorrectLength, "stackalloc dynamic [3] { d }").WithArguments("3").WithLocation(7, 18),
                // (8,29): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('dynamic')
                //         var d2 = stackalloc dynamic [ ] { d };
                Diagnostic(ErrorCode.ERR_ManagedAddr, "dynamic").WithArguments("dynamic").WithLocation(8, 29),
                // (9,18): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('dynamic')
                //         var d3 = stackalloc         [ ] { d };
                Diagnostic(ErrorCode.ERR_ManagedAddr, "stackalloc         [ ] { d }").WithArguments("dynamic").WithLocation(9, 18),
                // (9,18): error CS0214: Pointers and fixed size buffers may only be used in an unsafe context
                //         var d3 = stackalloc         [ ] { d };
                Diagnostic(ErrorCode.ERR_UnsafeNeeded, "stackalloc         [ ] { d }").WithLocation(9, 18)
                );
        }

        [Fact]
        public void StackAllocWithDynamicSpan()
        {
            CreateCompilationWithMscorlibAndSpan(@"
using System;
class Program
{
    static void Main()
    {
        dynamic d = 1;
        Span<dynamic> d1 = stackalloc dynamic [3] { d };
        Span<dynamic> d2 = stackalloc dynamic [ ] { d };
        Span<dynamic> d3 = stackalloc         [ ] { d };
    }
}").VerifyDiagnostics(
                // (8,39): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('dynamic')
                //         Span<dynamic> d1 = stackalloc dynamic [3] { d };
                Diagnostic(ErrorCode.ERR_ManagedAddr, "dynamic").WithArguments("dynamic").WithLocation(8, 39),
                // (8,28): error CS0847: An array initializer of length '3' is expected
                //         Span<dynamic> d1 = stackalloc dynamic [3] { d };
                Diagnostic(ErrorCode.ERR_ArrayInitializerIncorrectLength, "stackalloc dynamic [3] { d }").WithArguments("3").WithLocation(8, 28),
                // (9,39): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('dynamic')
                //         Span<dynamic> d2 = stackalloc dynamic [ ] { d };
                Diagnostic(ErrorCode.ERR_ManagedAddr, "dynamic").WithArguments("dynamic").WithLocation(9, 39),
                // (10,28): error CS0208: Cannot take the address of, get the size of, or declare a pointer to a managed type ('dynamic')
                //         Span<dynamic> d3 = stackalloc         [ ] { d };
                Diagnostic(ErrorCode.ERR_ManagedAddr, "stackalloc         [ ] { d }").WithArguments("dynamic").WithLocation(10, 28)
                );
        }

        [Fact]
        public void StackAllocAsArgument()
        {
            CreateStandardCompilation(@"
class Program
{
    static void N(object p) { }

    static void Main()
    {
        N(stackalloc int [3] { 1, 2, 3 });
        N(stackalloc int [ ] { 1, 2, 3 });
        N(stackalloc     [ ] { 1, 2, 3 });
    }
}").VerifyDiagnostics(
                // (8,11): error CS1525: Invalid expression term 'stackalloc'
                //         N(stackalloc int[3] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 11),
                // (9,11): error CS1525: Invalid expression term 'stackalloc'
                //         N(stackalloc int[] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(9, 11),
                // (10,11): error CS1525: Invalid expression term 'stackalloc'
                //         N(stackalloc[] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(10, 11)
                );
        }

        [Fact]
        public void StackAllocInParenthesis()
        {
            CreateStandardCompilation(@"
class Program
{
    static void Main()
    {
        var x1 = (stackalloc int [3] { 1, 2, 3 });
        var x2 = (stackalloc int [ ] { 1, 2, 3 });
        var x3 = (stackalloc     [ ] { 1, 2, 3 });
    }
}").VerifyDiagnostics(
                // (6,19): error CS1525: Invalid expression term 'stackalloc'
                //         var x1 = (stackalloc int[3] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(6, 19),
                // (7,19): error CS1525: Invalid expression term 'stackalloc'
                //         var x2 = (stackalloc int[] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 19),
                // (8,19): error CS1525: Invalid expression term 'stackalloc'
                //         var x3 = (stackalloc[] { 1, 2, 3 });
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 19)
                );
        }

        [Fact]
        public void StackAllocInNullConditionalOperator()
        {
            CreateStandardCompilation(@"
class Program
{
    static void Main()
    {
        var x1 = stackalloc int [3] { 1, 2, 3 } ?? stackalloc int [3] { 1, 2, 3 };
        var x2 = stackalloc int [ ] { 1, 2, 3 } ?? stackalloc int [ ] { 1, 2, 3 };
        var x3 = stackalloc     [ ] { 1, 2, 3 } ?? stackalloc     [ ] { 1, 2, 3 };
    }
}").VerifyDiagnostics(
                // (6,18): error CS1525: Invalid expression term 'stackalloc'
                //         var x1 = stackalloc int [3] { 1, 2, 3 } ?? stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(6, 18),
                // (6,52): error CS1525: Invalid expression term 'stackalloc'
                //         var x1 = stackalloc int [3] { 1, 2, 3 } ?? stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(6, 52),
                // (7,18): error CS1525: Invalid expression term 'stackalloc'
                //         var x2 = stackalloc int []  { 1, 2, 3 } ?? stackalloc int []  { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 18),
                // (7,52): error CS1525: Invalid expression term 'stackalloc'
                //         var x2 = stackalloc int []  { 1, 2, 3 } ?? stackalloc int []  { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(7, 52),
                // (8,18): error CS1525: Invalid expression term 'stackalloc'
                //         var x3 = stackalloc     []  { 1, 2, 3 } ?? stackalloc     []  { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 18),
                // (8,52): error CS1525: Invalid expression term 'stackalloc'
                //         var x3 = stackalloc     []  { 1, 2, 3 } ?? stackalloc     []  { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_InvalidExprTerm, "stackalloc").WithArguments("stackalloc").WithLocation(8, 52)
                );
        }

        [Fact]
        public void StackAllocInCastAndConditionalOperator()
        {
            CreateCompilationWithMscorlibAndSpan(@"
using System;
class Test
{
    public void Method()
    {
        Test value1 = true ? new Test() : (Test)stackalloc int [3] { 1, 2, 3 };
        Test value2 = true ? new Test() : (Test)stackalloc int [ ] { 1, 2, 3 };
        Test value3 = true ? new Test() : (Test)stackalloc     [ ] { 1, 2, 3 };
    }
    
    public static explicit operator Test(Span<int> value) 
    {
        return new Test();
    }
}", TestOptions.ReleaseDll).VerifyDiagnostics();
        }

        [Fact]
        public void ERR_StackallocInCatchFinally_Catch()
        {
            var text = @"
unsafe class C
{
    int x = M(() =>
    {
        try
        {
            // fine
            int* p1 = stackalloc int [3] { 1, 2, 3 };
            int* p2 = stackalloc int [ ] { 1, 2, 3 };
            int* p3 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* q1 = stackalloc int [3] { 1, 2, 3 };
                    int* q2 = stackalloc int [ ] { 1, 2, 3 };
                    int* q3 = stackalloc     [ ] { 1, 2, 3 };
                }
                catch
                {
                    int* err11 = stackalloc int [3] { 1, 2, 3 };
                    int* err12 = stackalloc int [ ] { 1, 2, 3 };
                    int* err13 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
        catch
        {
            int* err21 = stackalloc int [3] { 1, 2, 3 };
            int* err22 = stackalloc int [ ] { 1, 2, 3 };
            int* err23 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* p1 = stackalloc int [3] { 1, 2, 3 };
                    int* p2 = stackalloc int [ ] { 1, 2, 3 };
                    int* p3 = stackalloc     [ ] { 1, 2, 3 };
                }
                catch
                {
                    int* err31 = stackalloc int [3] { 1, 2, 3 };
                    int* err32 = stackalloc int [ ] { 1, 2, 3 };
                    int* err33 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
    });

    static int M(System.Action action)
    {
        try
        {
            // fine
            int* p1 = stackalloc int [3] { 1, 2, 3 };
            int* p2 = stackalloc int [ ] { 1, 2, 3 };
            int* p3 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* q1 = stackalloc int [3] { 1, 2, 3 };
                    int* q2 = stackalloc int [ ] { 1, 2, 3 };
                    int* q3 = stackalloc     [ ] { 1, 2, 3 };
                }
                catch
                {
                    int* err41 = stackalloc int [3] { 1, 2, 3 };
                    int* err42 = stackalloc int [ ] { 1, 2, 3 };
                    int* err43 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
        catch
        {
            int* err51 = stackalloc int [3] { 1, 2, 3 };
            int* err52 = stackalloc int [ ] { 1, 2, 3 };
            int* err53 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* p1 = stackalloc int [3] { 1, 2, 3 };
                    int* p2 = stackalloc int [ ] { 1, 2, 3 };
                    int* p3 = stackalloc     [ ] { 1, 2, 3 };
                }
                catch
                {
                    int* err61 = stackalloc int [3] { 1, 2, 3 };
                    int* err62 = stackalloc int [ ] { 1, 2, 3 };
                    int* err63 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
        return 0;
    }
}
";
            CreateStandardCompilation(text, options: TestOptions.UnsafeReleaseDll).VerifyDiagnostics(
                // (23,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err11 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(23, 34),
                // (24,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err12 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(24, 34),
                // (25,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err13 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(25, 34),
                // (31,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err21 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(31, 26),
                // (32,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err22 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(32, 26),
                // (33,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err23 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(33, 26),
                // (45,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err31 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(45, 34),
                // (46,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err32 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(46, 34),
                // (47,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err33 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(47, 34),
                // (72,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err41 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(72, 34),
                // (73,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err42 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(73, 34),
                // (74,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err43 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(74, 34),
                // (80,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err51 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(80, 26),
                // (81,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err52 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(81, 26),
                // (82,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err53 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(82, 26),
                // (94,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err61 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(94, 34),
                // (95,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err62 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(95, 34),
                // (96,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err63 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(96, 34)
                );
        }

        [Fact]
        public void ERR_StackallocInCatchFinally_Finally()
        {
            var text = @"
unsafe class C
{
    int x = M(() =>
    {
        try
        {
            // fine
            int* p1 = stackalloc int [3] { 1, 2, 3 };
            int* p2 = stackalloc int [ ] { 1, 2, 3 };
            int* p3 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* q1 = stackalloc int [3] { 1, 2, 3 };
                    int* q2 = stackalloc int [ ] { 1, 2, 3 };
                    int* q3 = stackalloc     [ ] { 1, 2, 3 };
                }
                finally
                {
                    int* err11 = stackalloc int [3] { 1, 2, 3 };
                    int* err12 = stackalloc int [ ] { 1, 2, 3 };
                    int* err13 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
        finally
        {
            int* err21 = stackalloc int [3] { 1, 2, 3 };
            int* err22 = stackalloc int [ ] { 1, 2, 3 };
            int* err23 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* p1 = stackalloc int [3] { 1, 2, 3 };
                    int* p2 = stackalloc int [ ] { 1, 2, 3 };
                    int* p3 = stackalloc     [ ] { 1, 2, 3 };
                }
                finally
                {
                    int* err31 = stackalloc int [3] { 1, 2, 3 };
                    int* err32 = stackalloc int [ ] { 1, 2, 3 };
                    int* err33 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
    });

    static int M(System.Action action)
    {
        try
        {
            // fine
            int* p1 = stackalloc int [3] { 1, 2, 3 };
            int* p2 = stackalloc int [ ] { 1, 2, 3 };
            int* p3 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* q1 = stackalloc int [3] { 1, 2, 3 };
                    int* q2 = stackalloc int [ ] { 1, 2, 3 };
                    int* q3 = stackalloc     [ ] { 1, 2, 3 };
                }
                finally
                {
                    int* err41 = stackalloc int [3] { 1, 2, 3 };
                    int* err42 = stackalloc int [ ] { 1, 2, 3 };
                    int* err43 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
        finally
        {
            int* err51 = stackalloc int [3] { 1, 2, 3 };
            int* err52 = stackalloc int [ ] { 1, 2, 3 };
            int* err53 = stackalloc     [ ] { 1, 2, 3 };
            System.Action a = () =>
            {
                try
                {
                    // fine
                    int* p1 = stackalloc int [3] { 1, 2, 3 };
                    int* p2 = stackalloc int [ ] { 1, 2, 3 };
                    int* p3 = stackalloc     [ ] { 1, 2, 3 };
                }
                finally
                {
                    int* err61 = stackalloc int [3] { 1, 2, 3 };
                    int* err62 = stackalloc int [ ] { 1, 2, 3 };
                    int* err63 = stackalloc     [ ] { 1, 2, 3 };
                }
            };
        }
        return 0;
    }
}
";
            CreateStandardCompilation(text, options: TestOptions.UnsafeReleaseDll).VerifyDiagnostics(
                // (23,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err11 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(23, 34),
                // (24,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err12 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(24, 34),
                // (25,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err13 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(25, 34),
                // (31,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err21 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(31, 26),
                // (32,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err22 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(32, 26),
                // (33,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err23 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(33, 26),
                // (45,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err31 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(45, 34),
                // (46,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err32 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(46, 34),
                // (47,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err33 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(47, 34),
                // (72,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err41 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(72, 34),
                // (73,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err42 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(73, 34),
                // (74,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err43 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(74, 34),
                // (80,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err51 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(80, 26),
                // (81,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err52 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(81, 26),
                // (82,26): error CS0255: stackalloc may not be used in a catch or finally block
                //             int* err53 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(82, 26),
                // (94,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err61 = stackalloc int [3] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [3] { 1, 2, 3 }").WithLocation(94, 34),
                // (95,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err62 = stackalloc int [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc int [ ] { 1, 2, 3 }").WithLocation(95, 34),
                // (96,34): error CS0255: stackalloc may not be used in a catch or finally block
                //                     int* err63 = stackalloc     [ ] { 1, 2, 3 };
                Diagnostic(ErrorCode.ERR_StackallocInCatchFinally, "stackalloc     [ ] { 1, 2, 3 }").WithLocation(96, 34)
                );
        }
    }
}