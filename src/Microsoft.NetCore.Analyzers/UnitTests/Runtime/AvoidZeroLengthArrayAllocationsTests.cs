﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Reflection;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.CSharp.Analyzers.Runtime;
using Microsoft.NetCore.VisualBasic.Analyzers.Runtime;
using Test.Utilities;
using Xunit;

namespace Microsoft.NetCore.Analyzers.Runtime.UnitTests
{
    public class AvoidZeroLengthArrayAllocationsAnalyzerTests : CodeFixTestBase
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() { return new CSharpAvoidZeroLengthArrayAllocationsAnalyzer(); }
        protected override CodeFixProvider GetCSharpCodeFixProvider() { return new AvoidZeroLengthArrayAllocationsFixer(); }
        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer() { return new BasicAvoidZeroLengthArrayAllocationsAnalyzer(); }
        protected override CodeFixProvider GetBasicCodeFixProvider() { return new AvoidZeroLengthArrayAllocationsFixer(); }

        /// <summary>
        /// This type isn't defined in all locations where this test runs.  Need to alter the
        /// test code slightly to account for this.
        /// </summary>
        private static bool IsArrayEmptyDefined()
        {
            Assembly assembly = typeof(object).Assembly;
            Type type = assembly.GetType("System.Array");
            return type.GetMethod("Empty", BindingFlags.Public | BindingFlags.Static) != null;
        }

        [Fact]
        public void EmptyArrayCSharp()
        {
            const string arrayEmptySourceRaw =
                @"namespace System { public class Array { public static T[] Empty<T>() { return null; } } }";

            const string badSource = @"
using System.Collections.Generic;

class C
{
    unsafe void M1()
    {
        int[] arr1 = new int[0];                       // yes
        byte[] arr2 = { };                             // yes
        C[] arr3 = new C[] { };                        // yes
        string[] arr4 = new string[] { null };         // no
        double[] arr5 = new double[1];                 // no
        int[] arr6 = new[] { 1 };                      // no
        int[][] arr7 = new int[0][];                   // yes
        int[][][][] arr8 = new int[0][][][];           // yes
        int[,] arr9 = new int[0,0];                    // no
        int[][,] arr10 = new int[0][,];                // yes
        int[][,] arr11 = new int[1][,];                // no
        int[,][] arr12 = new int[0,0][];               // no
        int*[] arr13 = new int*[0];                    // no
        List<int> list1 = new List<int>() { };         // no
    }
}";

            const string fixedSource = @"
using System.Collections.Generic;

class C
{
    unsafe void M1()
    {
        int[] arr1 = System.Array.Empty<int>();                       // yes
        byte[] arr2 = System.Array.Empty<byte>();                             // yes
        C[] arr3 = System.Array.Empty<C>();                        // yes
        string[] arr4 = new string[] { null };         // no
        double[] arr5 = new double[1];                 // no
        int[] arr6 = new[] { 1 };                      // no
        int[][] arr7 = System.Array.Empty<int[]>();                   // yes
        int[][][][] arr8 = System.Array.Empty<int[][][]>();           // yes
        int[,] arr9 = new int[0,0];                    // no
        int[][,] arr10 = System.Array.Empty<int[,]>();                // yes
        int[][,] arr11 = new int[1][,];                // no
        int[,][] arr12 = new int[0,0][];               // no
        int*[] arr13 = new int*[0];                    // no
        List<int> list1 = new List<int>() { };         // no
    }
}";
            string arrayEmptySource = IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;

            VerifyCSharpUnsafeCode(badSource + arrayEmptySource, new[]
            {
                GetCSharpResultAt(8, 22, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<int>()"),
                GetCSharpResultAt(9, 23, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<byte>()"),
                GetCSharpResultAt(10, 20, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<C>()"),
                GetCSharpResultAt(14, 24, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<int[]>()"),
                GetCSharpResultAt(15, 28, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<int[][][]>()"),
                GetCSharpResultAt(17, 26, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<int[,]>()")
            });
            VerifyCSharpUnsafeCodeFix(
                arrayEmptySource + badSource,
                arrayEmptySource + fixedSource,
                allowNewCompilerDiagnostics: true);
            VerifyCSharpUnsafeCodeFix(
                "using System;\r\n" + arrayEmptySource + badSource,
                "using System;\r\n" + arrayEmptySource + fixedSource.Replace("System.Array.Empty", "Array.Empty"),
                allowNewCompilerDiagnostics: true);
        }

        [Fact]
        public void EmptyArrayCSharpError()
        {
            const string badSource = @"
// This is a compile error but we want to ensure analyzer doesn't complain for it.
[System.Runtime.CompilerServices.Dynamic(new bool[0])]
";

            VerifyCSharp(badSource, TestValidationMode.AllowCompileErrors);
        }

        [Fact]
        public void EmptyArrayVisualBasic()
        {
            const string arrayEmptySourceRaw = @"
Namespace System
    Public Class Array
       Public Shared Function Empty(Of T)() As T()
           Return Nothing
       End Function
    End Class
End Namespace
";
            const string badSource = @"
Imports System.Collections.Generic

<System.Runtime.CompilerServices.Dynamic(new Boolean(-1) {})> _
Class C
    Sub M1()
        Dim arr1 As Integer() = New Integer(-1) { }               ' yes
        Dim arr2 As Byte() = { }                                  ' yes
        Dim arr3 As C() = New C(-1) { }                           ' yes
        Dim arr4 As String() = New String() { Nothing }           ' no
        Dim arr5 As Double() = New Double(1) { }                  ' no
        Dim arr6 As Integer() = { -1 }                            ' no
        Dim arr7 as Integer()() = New Integer(-1)() { }           ' yes
        Dim arr8 as Integer()()()() = New Integer(  -1)()()() { } ' yes
        Dim arr9 as Integer(,) = New Integer(-1,-1) { }           ' no
        Dim arr10 as Integer()(,) = New Integer(-1)(,) { }        ' yes
        Dim arr11 as Integer()(,) = New Integer(1)(,) { }         ' no
        Dim arr12 as Integer(,)() = New Integer(-1,-1)() { }      ' no
        Dim arr13 as Integer() = New Integer(0) { }               ' no
        Dim list1 as List(Of Integer) = New List(Of Integer) From { }  ' no
    End Sub
End Class";

            const string fixedSource = @"
Imports System.Collections.Generic

<System.Runtime.CompilerServices.Dynamic(new Boolean(-1) {})> _
Class C
    Sub M1()
        Dim arr1 As Integer() = System.Array.Empty(Of Integer)()               ' yes
        Dim arr2 As Byte() = System.Array.Empty(Of Byte)()                                  ' yes
        Dim arr3 As C() = System.Array.Empty(Of C)()                           ' yes
        Dim arr4 As String() = New String() { Nothing }           ' no
        Dim arr5 As Double() = New Double(1) { }                  ' no
        Dim arr6 As Integer() = { -1 }                            ' no
        Dim arr7 as Integer()() = System.Array.Empty(Of Integer())()           ' yes
        Dim arr8 as Integer()()()() = System.Array.Empty(Of Integer()()())() ' yes
        Dim arr9 as Integer(,) = New Integer(-1,-1) { }           ' no
        Dim arr10 as Integer()(,) = System.Array.Empty(Of Integer(,))()        ' yes
        Dim arr11 as Integer()(,) = New Integer(1)(,) { }         ' no
        Dim arr12 as Integer(,)() = New Integer(-1,-1)() { }      ' no
        Dim arr13 as Integer() = New Integer(0) { }               ' no
        Dim list1 as List(Of Integer) = New List(Of Integer) From { }  ' no
    End Sub
End Class";

            string arrayEmptySource = IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;

            VerifyBasic(badSource + arrayEmptySource, new[]
            {
                GetBasicResultAt(7, 33, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty(Of Integer)()"),
                GetBasicResultAt(8, 30, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty(Of Byte)()"),
                GetBasicResultAt(9, 27, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty(Of C)()"),
                GetBasicResultAt(13, 35, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty(Of Integer())()"),
                GetBasicResultAt(14, 39, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty(Of Integer()()())()"),
                GetBasicResultAt(16, 37, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty(Of Integer(,))()")
            });
            VerifyBasicFix(
                arrayEmptySource + badSource,
                arrayEmptySource + fixedSource,
                allowNewCompilerDiagnostics: true);
            VerifyBasicFix(
                "Imports System\r\n" + arrayEmptySource + badSource,
                "Imports System\r\n" + arrayEmptySource + fixedSource.Replace("System.Array.Empty", "Array.Empty"),
                allowNewCompilerDiagnostics: true);
        }

        [Fact]
        public void EmptyArrayCSharp_DifferentTypeKind()
        {
            const string arrayEmptySourceRaw =
                @"namespace System { public class Array { public static T[] Empty<T>() { return null; } } }";

            const string badSource = @"
class C
{
    void M1()
    {
        int[] arr1 = new int[(long)0];                 // yes
        double[] arr2 = new double[(ulong)0];         // yes
        double[] arr3 = new double[(long)1];         // no
    }
}";

            const string fixedSource = @"
class C
{
    void M1()
    {
        int[] arr1 = System.Array.Empty<int>();                 // yes
        double[] arr2 = System.Array.Empty<double>();         // yes
        double[] arr3 = new double[(long)1];         // no
    }
}";
            string arrayEmptySource = IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;

            VerifyCSharp(badSource + arrayEmptySource, new[]
            {
                GetCSharpResultAt(6, 22, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<int>()"),
                GetCSharpResultAt(7, 25, AvoidZeroLengthArrayAllocationsAnalyzer.UseArrayEmptyDescriptor, "Array.Empty<double>()")
            });

            VerifyCSharpFix(
                arrayEmptySource + badSource,
                arrayEmptySource + fixedSource,
                allowNewCompilerDiagnostics: true);
            VerifyCSharpFix(
                "using System;\r\n" + arrayEmptySource + badSource,
                "using System;\r\n" + arrayEmptySource + fixedSource.Replace("System.Array.Empty", "Array.Empty"),
                allowNewCompilerDiagnostics: true);
        }

        [WorkItem(10214, "https://github.com/dotnet/roslyn/issues/10214")]
        [Fact]
        public void EmptyArrayVisualBasic_CompilerGeneratedArrayCreation()
        {
            const string arrayEmptySourceRaw = @"
Namespace System
    Public Class Array
       Public Shared Function Empty(Of T)() As T()
           Return Nothing
       End Function
    End Class
End Namespace
";
            const string source = @"
Class C
    Private Sub F(ParamArray args As String())
    End Sub

Private Sub G()
        F()     ' Compiler seems to generate a param array with size 0 for the invocation.
    End Sub
End Class
";

            string arrayEmptySource = IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;

            // Should we be flagging diagnostics on compiler generated code?
            // Should the analyzer even be invoked for compiler generated code?
            VerifyBasic(source + arrayEmptySource);
        }

        [WorkItem(1209, "https://github.com/dotnet/roslyn-analyzers/issues/1209")]
        [Fact]
        public void EmptyArrayCSharp_CompilerGeneratedArrayCreationInObjectCreation()
        {
            const string arrayEmptySourceRaw = @"
using System;
using System.Collections;
using System.Collections.Generic;

namespace System
{
	public class Array
	{
		public static T[] Empty<T>()
		{
			return null;
		}
	}
}
";
            const string source = @"
namespace N
{
    using Microsoft.CodeAnalysis;
    class C
    {
	    public static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            ""RuleId"",
            ""Title"",
            ""MessageFormat"",
            ""Dummy"",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: ""Description"");
    }
}
";

            string arrayEmptySource = IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;

            // Should we be flagging diagnostics on compiler generated code?
            // Should the analyzer even be invoked for compiler generated code?
            VerifyCSharp(source + arrayEmptySource, addLanguageSpecificCodeAnalysisReference: true);
        }
        
        [WorkItem(1209, "https://github.com/dotnet/roslyn-analyzers/issues/1209")]
        [Fact]
        public void EmptyArrayCSharp_CompilerGeneratedArrayCreationInIndexerAccess()
        {
            const string arrayEmptySourceRaw = @"
using System;
using System.Collections;
using System.Collections.Generic;

namespace System
{
	public class Array
	{
		public static T[] Empty<T>()
		{
			return null;
		}
	}
}
";
            const string source = @"
public abstract class C
{
    protected abstract int this[int p1, params int[] p2] {get; set;}
    public void M()
    {
        var x = this[0];
    }
}
";

            string arrayEmptySource = IsArrayEmptyDefined() ? string.Empty : arrayEmptySourceRaw;

            // Should we be flagging diagnostics on compiler generated code?
            // Should the analyzer even be invoked for compiler generated code?
            VerifyCSharp(source + arrayEmptySource, addLanguageSpecificCodeAnalysisReference: true);
        }
    }
}
