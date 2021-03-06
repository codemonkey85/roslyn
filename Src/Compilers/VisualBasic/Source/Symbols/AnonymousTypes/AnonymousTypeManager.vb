﻿' Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.Collections.Concurrent
Imports System.Collections.Generic
Imports System.Collections.ObjectModel
Imports System.Text
Imports System.Threading
Imports Microsoft.CodeAnalysis.Collections
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Symbols
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.VisualBasic.Symbols

    ''' <summary> 
    ''' Manages anonymous types and delegates created on module level. All requests 
    ''' for anonymous type/delegate symbols go via the instance of this class.
    ''' 
    ''' Manager also is in charge of creating implementation types which are used in 
    ''' emit phase to substitute anonymous type/delegate public symbols.
    ''' </summary>
    Partial Friend NotInheritable Class AnonymousTypeManager

        ''' <summary> Source module </summary>
        Public ReadOnly Property ContainingModule As SourceModuleSymbol
            Get
                Return DirectCast(Compilation.SourceModule, SourceModuleSymbol)
            End Get
        End Property

        ''' <summary> Owning compilationSource module </summary>
        Public ReadOnly Compilation As VisualBasicCompilation

        Public Sub New(compilation As VisualBasicCompilation)
            Me.Compilation = compilation
        End Sub

        ''' <summary> 
        ''' Given anonymous type descriptor provided construct an anonymous type symbol
        ''' </summary>
        Public Function ConstructAnonymousTypeSymbol(typeDescr As AnonymousTypeDescriptor) As AnonymousTypePublicSymbol
            Return New AnonymousTypePublicSymbol(Me, typeDescr)
        End Function

        ''' <summary> 
        ''' Given anonymous delegate descriptor provided, construct an anonymous delegate symbol
        ''' </summary>
        Public Function ConstructAnonymousDelegateSymbol(delegateDescriptor As AnonymousTypeDescriptor) As AnonymousDelegatePublicSymbol
            Return New AnonymousDelegatePublicSymbol(Me, delegateDescriptor)
        End Function

        ''' <summary>
        ''' Compares anonymous types ignoring custom modifiers
        ''' </summary>
        Public Shared Function EqualsIgnoringCustomModifiers(left As TypeSymbol, right As TypeSymbol) As Boolean
            If left.TypeKind <> right.TypeKind Then
                Return False
            End If

            Dim leftDescr = DirectCast(left, AnonymousTypeOrDelegatePublicSymbol).TypeDescriptor
            Dim rightDescr = DirectCast(right, AnonymousTypeOrDelegatePublicSymbol).TypeDescriptor

            If leftDescr.Key <> rightDescr.Key Then
                Return False
            End If

            Dim count As Integer = leftDescr.Fields.Length
            Debug.Assert(count = rightDescr.Fields.Length)
            For i = 0 To count - 1
                If Not leftDescr.Fields(i).Type.IsSameTypeIgnoringCustomModifiers(rightDescr.Fields(i).Type) Then
                    Return False
                End If
            Next
            Return True
        End Function

    End Class

End Namespace
