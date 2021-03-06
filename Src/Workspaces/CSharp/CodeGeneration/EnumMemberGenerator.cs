﻿// Copyright (c) Microsoft Open Technologies, Inc.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Extensions;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Shared.Utilities;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.CSharp.CodeGeneration
{
    internal class EnumMemberGenerator : AbstractCSharpCodeGenerator
    {
        internal static EnumDeclarationSyntax AddEnumMemberTo(EnumDeclarationSyntax destination, IFieldSymbol enumMember, CodeGenerationOptions options)
        {
            var members = new List<SyntaxNodeOrToken>();
            members.AddRange(destination.Members.GetWithSeparators());

            var member = GenerateEnumMemberDeclaration(enumMember, destination, options);

            if (members.Count == 0)
            {
                members.Add(member);
            }
            else if (members.LastOrDefault().CSharpKind() == SyntaxKind.CommaToken)
            {
                members.Add(member);
                members.Add(SyntaxFactory.Token(SyntaxKind.CommaToken));
            }
            else
            {
                var lastMember = members.Last();
                var trailingTrivia = lastMember.GetTrailingTrivia();
                members[members.Count - 1] = lastMember.WithTrailingTrivia();
                members.Add(SyntaxFactory.Token(SyntaxKind.CommaToken).WithTrailingTrivia(trailingTrivia));
                members.Add(member);
            }

            SyntaxToken openBrace;
            SyntaxToken closeBrace;
            GetBraceTokens(destination, out openBrace, out closeBrace);

            return destination.WithOpenBraceToken(openBrace)
                                  .WithMembers(SyntaxFactory.SeparatedList<EnumMemberDeclarationSyntax>(members))
                                  .WithCloseBraceToken(closeBrace);
        }

        public static EnumMemberDeclarationSyntax GenerateEnumMemberDeclaration(
            IFieldSymbol enumMember,
            EnumDeclarationSyntax destinationOpt,
            CodeGenerationOptions options)
        {
            var reusableSyntax = GetReuseableSyntaxNodeForSymbol<EnumMemberDeclarationSyntax>(enumMember, options);
            if (reusableSyntax != null)
            {
                return reusableSyntax;
            }

            var value = CreateEnumMemberValue(destinationOpt, enumMember);
            var member = SyntaxFactory.EnumMemberDeclaration(enumMember.Name.ToIdentifierToken())
                .WithEqualsValue(value == null ? null : SyntaxFactory.EqualsValueClause(value: value));

            return AddCleanupAnnotationsTo(
                ConditionallyAddDocumentationCommentTo(member, enumMember, options));
        }

        private static ExpressionSyntax CreateEnumMemberValue(EnumDeclarationSyntax destinationOpt, IFieldSymbol enumMember)
        {
            var valueOpt = enumMember.ConstantValue is IConvertible
                ? (long?)IntegerUtilities.ToInt64(enumMember.ConstantValue)
                : null;
            if (valueOpt == null)
            {
                return null;
            }

            var value = valueOpt.Value;
            if (destinationOpt != null)
            {
                if (destinationOpt.Members.Count == 0)
                {
                    if (value == 0)
                    {
                        return null;
                    }
                }
                else
                {
                    // Don't generate an initializer if no other members have them, and our value
                    // would be correctly inferred from our position.
                    if (destinationOpt.Members.Count == value &&
                        destinationOpt.Members.All(m => m.EqualsValue == null))
                    {
                        return null;
                    }

                    // Existing members, try to stay consistent with their style.
                    var lastMember = destinationOpt.Members.LastOrDefault(m => m.EqualsValue != null);
                    if (lastMember != null)
                    {
                        var lastExpression = lastMember.EqualsValue.Value;
                        if (lastExpression.CSharpKind() == SyntaxKind.LeftShiftExpression &&
                            IntegerUtilities.HasOneBitSet(value))
                        {
                            var binaryExpression = (BinaryExpressionSyntax)lastExpression;
                            if (binaryExpression.Left.CSharpKind() == SyntaxKind.NumericLiteralExpression)
                            {
                                var numericLiteral = (LiteralExpressionSyntax)binaryExpression.Left;
                                if (numericLiteral.Token.ValueText == "1")
                                {
                                    // The user is left shifting ones, stick with that pattern
                                    var shiftValue = IntegerUtilities.LogBase2(value);
                                    return SyntaxFactory.BinaryExpression(
                                        SyntaxKind.LeftShiftExpression,
                                        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal("1", 1)),
                                        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(shiftValue.ToString(), shiftValue)));
                                }
                            }
                        }
                        else if (lastExpression.CSharpKind() == SyntaxKind.NumericLiteralExpression)
                        {
                            var numericLiteral = (LiteralExpressionSyntax)lastExpression;
                            var numericToken = numericLiteral.Token;
                            var numericText = numericToken.ToString();

                            if (numericText.StartsWith("0x") || numericText.StartsWith("0X"))
                            {
                                // Hex
                                return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression,
                                    SyntaxFactory.Literal(numericText.Substring(0, 2) + value.ToString("X"), value));
                            }
                        }
                    }
                }
            }

            var namedType = enumMember.Type as INamedTypeSymbol;
            var underlyingType = namedType != null ? namedType.EnumUnderlyingType : null;

            return ExpressionGenerator.GenerateNonEnumValueExpression(
                underlyingType,
                enumMember.ConstantValue,
                canUseFieldReference: true);
        }
    }
}