// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Filtering;

internal enum FilterToken
{
    None,
    Identifier,
    StringLiteral,
    Number,
    True,
    False,
    Null,
    Eq,
    Ne,
    Co,
    Sw,
    Ew,
    Pr,
    Gt,
    Ge,
    Lt,
    Le,
    And,
    Or,
    Not,
    LParen,
    RParen,
    LBracket,
    RBracket,
    Dot,
    Colon
}
